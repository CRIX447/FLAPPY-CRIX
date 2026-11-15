// Flappy Crix - the desktop game. CRIX STUDIOS. Made with AI (Claude by Anthropic).
//
// The app IS the website game (an exact copy, never behind). It is a small Rust program that
// shows the live site in WebView2, the browser engine built into Windows, instead of carrying a
// whole copy of Chrome the way the old Electron app did. That is where the RAM saving comes from.
//
//  * a CRIX STUDIOS splash while the game loads, then the game
//  * Google, Discord and ItIsNow sign-in windows open inside the app
//  * any other link opens in the player's normal browser
//  * a "can't reach the servers" page that keeps retrying by itself
//  * no developer tools and no remote-debugging switches in a built copy
//  * checks GitHub for a newer app and offers it

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use tauri::webview::{NewWindowResponse, PageLoadEvent};
use tauri::{AppHandle, Manager, WebviewUrl, WebviewWindow, WebviewWindowBuilder};
use tauri_plugin_dialog::{DialogExt, MessageDialogButtons, MessageDialogKind};
use tauri_plugin_opener::OpenerExt;
use url::Url;

const RELEASES: &str = "https://github.com/CRIX447/FLAPPY-CRIX/releases/latest";

/// The splash stays at least this long, so it never just flickers.
const SPLASH_MIN: Duration = Duration::from_millis(1800);
/// Show the game window after this long even if the page never says it finished.
const REVEAL_ANYWAY: Duration = Duration::from_secs(8);

// ------------------------------------------------------------------ which addresses go where

/// The game's own sites. Only these load in the main window (plus sign-in, below).
const INTERNAL: &[&str] = &[
    "crixgamingvr.com", "www.crixgamingvr.com", "flappycrix.vercel.app", "flappycrix.io", "www.flappycrix.io",
];
/// /flappycrix-og is the birthday season.
const INTERNAL_PATHS: &[&str] = &["/flappycrix", "/game", "/index"];

fn is_game_url(u: &Url) -> bool {
    let Some(host) = u.host_str() else { return false };
    if u.scheme() != "https" || !INTERNAL.contains(&host) {
        return false;
    }
    // the front page of the old game-only sites is the game; crixgamingvr.com's front page is not
    if u.path() == "/" && !host.ends_with("crixgamingvr.com") {
        return true;
    }
    INTERNAL_PATHS.iter().any(|p| u.path().starts_with(p))
}

/// Sign-in pages. Firebase's Google sign-in pops up on app.crixgamingvr.com/__/auth and hops
/// through Google; Discord linking goes to Discord and back; ItIsNow opens api.itisnow.net/popup.
/// They all hand the answer back to the game that opened them, so they must open inside the app.
fn is_auth_url(u: &Url) -> bool {
    let Some(h) = u.host_str() else { return false };
    if u.scheme() != "https" {
        return false;
    }
    let p = u.path();
    if h == "app.crixgamingvr.com" || h.ends_with(".firebaseapp.com") {
        return p.starts_with("/__/auth");
    }
    if h == "accounts.google.com" || h == "accounts.youtube.com" {
        return true;
    }
    if h == "discord.com" || h == "discordapp.com" {
        return ["/oauth2", "/login", "/api/oauth2"].iter().any(|x| p.starts_with(x));
    }
    h == "api.itisnow.net"
}

/// The app's own pages (the splash and the start / "can't reach the servers" page).
fn is_app_page(u: &Url) -> bool {
    u.scheme() == "tauri" || (matches!(u.scheme(), "http" | "https") && u.host_str() == Some("tauri.localhost"))
}

/// Opens a link in the player's own browser. Only web links and email.
fn open_outside(app: &AppHandle, u: &Url) {
    if matches!(u.scheme(), "https" | "http" | "mailto") {
        let _ = app.opener().open_url(u.as_str(), None::<&str>);
    }
}

// ------------------------------------------------------------------ "crixapp:" messages
// The game page and the app's own pages ask the app for a few harmless things by navigating to
// crixapp://<what>. The app catches that and the page never actually goes there, so no
// connection between web pages and the app's insides (Tauri IPC) is needed at all.

fn handle_crixapp(app: &AppHandle, u: &Url) {
    match u.host_str().unwrap_or("") {
        "ready" => reveal(app),
        "reload" => load_start(app),
        "fullscreen" => {
            if let Some(w) = app.get_webview_window("main") {
                let on = w.is_fullscreen().unwrap_or(false);
                let _ = w.set_fullscreen(!on);
            }
        }
        "about" => about(app),
        "update" => {
            // the start page found a newer app on GitHub: crixapp://update?v=3.1.0
            let v = u.query_pairs().find(|(k, _)| k == "v").map(|(_, v)| v.to_string()).unwrap_or_default();
            offer_update(app, v);
        }
        _ => {}
    }
}

// ------------------------------------------------------------------ windows

struct Splash {
    shown_at: Instant,
    revealed: AtomicBool,
}

fn reveal(app: &AppHandle) {
    let st = app.state::<Arc<Splash>>().inner().clone();
    if st.revealed.swap(true, Ordering::SeqCst) {
        return;
    }
    let wait = SPLASH_MIN.saturating_sub(st.shown_at.elapsed());
    let app = app.clone();
    std::thread::spawn(move || {
        std::thread::sleep(wait);
        let a = app.clone();
        let _ = app.run_on_main_thread(move || {
            if let Some(w) = a.get_webview_window("main") {
                let _ = w.show();
                let _ = w.set_focus();
            }
            if let Some(s) = a.get_webview_window("splash") {
                let _ = s.close();
            }
        });
    });
}

/// The app's own start page: it checks the servers can be reached, then opens the game
/// (or shows "can't reach the servers" and keeps retrying).
fn start_url() -> Url {
    Url::parse(if cfg!(windows) { "http://tauri.localhost/index.html" } else { "tauri://localhost/index.html" }).unwrap()
}

fn load_start(app: &AppHandle) {
    if let Some(w) = app.get_webview_window("main") {
        let _ = w.navigate(start_url());
    }
}

fn about(app: &AppHandle) {
    let v = app.package_info().version.to_string();
    app.dialog()
        .message(format!(
            "Flappy Crix {v}\n\nCRIX STUDIOS\nInspired by Flappy Bird by Dong Nguyen.\n\nThe game updates itself from the website: you never need to reinstall.\n\nF5 reload  ·  F11 full screen"
        ))
        .title("Flappy Crix")
        .kind(MessageDialogKind::Info)
        .show(|_| {});
}

fn offer_update(app: &AppHandle, version: String) {
    static ASKED: AtomicBool = AtomicBool::new(false);
    if ASKED.swap(true, Ordering::SeqCst) {
        return;
    }
    let clean: String = version.chars().filter(|c| c.is_ascii_digit() || *c == '.').take(20).collect();
    let handle = app.clone();
    app.dialog()
        .message(format!(
            "A new version of the Flappy Crix app ({clean}) is out.\n\nDownload it now? Your progress is saved on your account."
        ))
        .title("Update available")
        .kind(MessageDialogKind::Info)
        .buttons(MessageDialogButtons::OkCancelCustom("Download".into(), "Later".into()))
        .show(move |yes| {
            if yes {
                if let Ok(u) = Url::parse(RELEASES) {
                    open_outside(&handle, &u);
                }
            }
        });
}

/// Small script put into the game page (and every frame in it): tells the game it is in the
/// desktop app, and gives it the one thing a page can't do for itself, opening the real browser.
fn bridge_script(version: &str) -> String {
    format!(
        r#"(function () {{
  if (window.CRIX_DESKTOP) return;
  if (location.hostname === 'tauri.localhost' || location.protocol === 'tauri:') {{ window.CRIX_APP_VERSION = {version:?}; return; }}
  var game = /^https:\/\/(www\.)?crixgamingvr\.com$|^https:\/\/flappycrix\.(vercel\.app|io)$/.test(location.origin);
  if (!game) return;
  function say(what) {{ try {{ location.href = 'crixapp://' + what; }} catch (e) {{}} }}
  Object.defineProperty(window, 'CRIX_DESKTOP', {{ value: Object.freeze({{
    isDesktop: true, platform: 'win32', version: {version:?}, shell: 'tauri',
    openExternal: function (url) {{ try {{ var u = new URL(String(url), location.href); if (/^(https?|mailto):$/.test(u.protocol)) window.open(u.href, '_blank', 'noopener'); }} catch (e) {{}} }}
  }}) }});
  if (window.top !== window) return;
  // F5 reloads (through the start page, so a dead connection shows the right page), F11 full screen
  addEventListener('keydown', function (e) {{
    if (e.key === 'F5' || (e.ctrlKey && (e.key === 'r' || e.key === 'R'))) {{ e.preventDefault(); say('reload'); }}
    else if (e.key === 'F11') {{ e.preventDefault(); say('fullscreen'); }}
    else if (e.key === 'F1') {{ e.preventDefault(); say('about'); }}
  }}, true);
  // A new website version came out while playing: offer to load it (checked every 10 minutes)
  var known = null;
  function check() {{
    fetch(location.origin + '/flappycrix?cb=' + Date.now(), {{ cache: 'no-store' }}).then(function (r) {{ return r.ok ? r.text() : ''; }}).then(function (t) {{
      var m = /const GAME_VERSION = '([^']+)'/.exec(t); if (!m) return;
      if (known === null) {{ known = m[1]; return; }}
      if (m[1] !== known) {{ known = m[1]; if (confirm('Flappy Crix ' + m[1] + ' is out.\n\nRestart to get the new version? Your progress is saved.')) say('reload'); }}
    }}).catch(function () {{}});
  }}
  if (document.readyState === 'loading') addEventListener('DOMContentLoaded', check); else check();
  setInterval(check, 10 * 60 * 1000);
}})();"#
    )
}

fn build_windows(app: &AppHandle) -> tauri::Result<()> {
    // the splash: a small frameless window with the CRIX STUDIOS picture
    WebviewWindowBuilder::new(app, "splash", WebviewUrl::App("splash.html".into()))
        .title("Flappy Crix")
        .inner_size(520.0, 520.0)
        .resizable(false)
        .decorations(false)
        .center()
        .background_color(tauri::window::Color(0, 0, 0, 255))
        .on_navigation(is_app_page)
        .on_new_window(|_, _| NewWindowResponse::Deny)
        .build()?;

    let version = app.package_info().version.to_string();
    let nav_app = app.clone();
    let win_app = app.clone();
    let load_app = app.clone();
    let main = WebviewWindowBuilder::new(app, "main", WebviewUrl::App("index.html".into()))
        .title("Flappy Crix")
        .inner_size(1280.0, 820.0) // the website's computer layout (three columns) fits
        .min_inner_size(380.0, 640.0)
        .center()
        .visible(false)
        .background_color(tauri::window::Color(0, 0, 0, 255))
        .initialization_script_for_all_frames(bridge_script(&version))
        // every page change in the main window
        .on_navigation(move |u| {
            if u.scheme() == "crixapp" {
                let a = nav_app.clone();
                let u = u.clone();
                let _ = nav_app.run_on_main_thread(move || handle_crixapp(&a, &u));
                return false;
            }
            if is_game_url(u) || is_auth_url(u) || is_app_page(u) || u.as_str() == "about:blank" {
                return true;
            }
            // anything else: the player's own browser, and the game stays where it is
            open_outside(&nav_app, u);
            false
        })
        // pop-ups (window.open): sign-in opens a small window of its own that can still hand
        // the answer back to the game; everything else goes to the normal browser
        .on_new_window(move |u, _features| {
            if is_auth_url(&u) {
                NewWindowResponse::Allow
            } else {
                open_outside(&win_app, &u);
                NewWindowResponse::Deny
            }
        })
        .on_page_load(move |w, p| {
            if p.event() == PageLoadEvent::Finished && is_game_url(p.url()) {
                reveal(&load_app);
                smoke_test_done(&w, true);
            }
        })
        .build()?;

    restrict_permissions(&main);

    // if nothing has said it's ready by now (a very slow connection), show the window anyway
    let a = app.clone();
    std::thread::spawn(move || {
        std::thread::sleep(REVEAL_ANYWAY);
        let b = a.clone();
        let _ = a.run_on_main_thread(move || reveal(&b));
    });
    Ok(())
}

// ------------------------------------------------------------------ microphone / camera
// Only the game's own site may ask, and only for the microphone (voice chat), clipboard and
// notifications. WebView2 then asks the player and remembers the answer. The camera and
// everything else a game has no use for is refused without asking.
#[cfg(windows)]
fn restrict_permissions(w: &WebviewWindow) {
    let _ = w.with_webview(|pv| unsafe {
        use webview2_com::Microsoft::Web::WebView2::Win32::*;
        use webview2_com::{take_pwstr, PermissionRequestedEventHandler};
        let Ok(core) = pv.controller().CoreWebView2() else { return };
        let mut token = Default::default();
        let _ = core.add_PermissionRequested(
            &PermissionRequestedEventHandler::create(Box::new(|_, args| {
                let Some(args) = args else { return Ok(()) };
                let mut uri = windows::core::PWSTR::null();
                args.Uri(&mut uri)?;
                let from = Url::parse(&take_pwstr(uri)).ok();
                let mut kind = COREWEBVIEW2_PERMISSION_KIND::default();
                args.PermissionKind(&mut kind)?;
                let game = from
                    .as_ref()
                    .map(|u| u.scheme() == "https" && u.host_str().is_some_and(|h| INTERNAL.contains(&h)))
                    .unwrap_or(false);
                let ok_kind = kind == COREWEBVIEW2_PERMISSION_KIND_MICROPHONE
                    || kind == COREWEBVIEW2_PERMISSION_KIND_CLIPBOARD_READ
                    || kind == COREWEBVIEW2_PERMISSION_KIND_NOTIFICATIONS;
                if !game || !ok_kind {
                    args.SetState(COREWEBVIEW2_PERMISSION_STATE_DENY)?;
                }
                Ok(())
            })),
            &mut token,
        );
    });
}
#[cfg(not(windows))]
fn restrict_permissions(_w: &WebviewWindow) {}

// ------------------------------------------------------------------ build check
// The GitHub build runs the finished exe with CRIX_SMOKE_TEST set to a file name. The app writes
// down what it loaded and quits. Nothing happens unless that is set.
static SMOKE: Mutex<Option<String>> = Mutex::new(None);

fn smoke_test_done(w: &WebviewWindow, ok: bool) {
    let Some(out) = SMOKE.lock().ok().and_then(|mut s| s.take()) else { return };
    let url = w.url().map(|u| u.to_string()).unwrap_or_default();
    let body = serde_json::json!({
        "ok": ok, "url": url, "version": w.app_handle().package_info().version.to_string(),
        "shell": "tauri", "devTools": cfg!(debug_assertions),
        "debugEnv": std::env::var_os("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS").is_some()
    });
    let _ = std::fs::write(&out, body.to_string());
    let app = w.app_handle().clone();
    // a few seconds on the game first, so the build can measure the RAM it really uses
    let hold = std::env::var("CRIX_SMOKE_HOLD").ok().and_then(|s| s.parse().ok()).unwrap_or(1500);
    std::thread::spawn(move || {
        std::thread::sleep(Duration::from_millis(hold));
        app.exit(if ok { 0 } else { 3 });
    });
}

fn main() {
    // A downloaded copy must not start with a remote-control / debugging port open: either would
    // let another program drive the game or read the signed-in session out of it. WebView2 takes
    // such switches from these settings, so they are cleared before anything starts.
    for k in [
        "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
        "WEBVIEW2_BROWSER_EXECUTABLE_FOLDER",
        "WEBVIEW2_RELEASE_CHANNEL_PREFERENCE",
        "WEBVIEW2_PIPE_FOR_SCRIPT_DEBUGGER",
        "WEBVIEW2_WAIT_FOR_SCRIPT_DEBUGGER",
    ] {
        std::env::remove_var(k);
    }
    if let Ok(v) = std::env::var("CRIX_SMOKE_TEST") {
        if !v.is_empty() {
            *SMOKE.lock().unwrap() = Some(v);
        }
    }

    tauri::Builder::default()
        // one copy at a time: starting it again just brings the game to the front
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(w) = app.get_webview_window("main").or_else(|| app.get_webview_window("splash")) {
                let _ = w.unminimize();
                let _ = w.set_focus();
            }
        }))
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_dialog::init())
        .manage(Arc::new(Splash { shown_at: Instant::now(), revealed: AtomicBool::new(false) }))
        .setup(|app| {
            build_windows(app.handle())?;
            // the build check gives up after 90 seconds
            if SMOKE.lock().map(|s| s.is_some()).unwrap_or(false) {
                let h = app.handle().clone();
                std::thread::spawn(move || {
                    std::thread::sleep(Duration::from_secs(90));
                    if let Some(w) = h.get_webview_window("main") {
                        smoke_test_done(&w, false);
                    }
                });
            }
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("Flappy Crix could not start");
}

#[cfg(test)]
mod tests {
    use super::*;
    fn u(s: &str) -> Url {
        Url::parse(s).unwrap()
    }

    #[test]
    fn game_pages() {
        assert!(is_game_url(&u("https://crixgamingvr.com/flappycrix")));
        assert!(is_game_url(&u("https://crixgamingvr.com/flappycrix-og")));
        assert!(is_game_url(&u("https://flappycrix.vercel.app/")));
        assert!(!is_game_url(&u("https://crixgamingvr.com/")));
        assert!(!is_game_url(&u("https://crixgamingvr.com/donate")));
        assert!(!is_game_url(&u("http://crixgamingvr.com/flappycrix")));
        assert!(!is_game_url(&u("https://crixgamingvr.com.evil.com/flappycrix")));
        assert!(!is_game_url(&u("https://evil.com/flappycrix")));
    }

    #[test]
    fn sign_in_pages() {
        assert!(is_auth_url(&u("https://app.crixgamingvr.com/__/auth/handler?x=1")));
        assert!(is_auth_url(&u("https://flappy-crix.firebaseapp.com/__/auth/handler")));
        assert!(is_auth_url(&u("https://accounts.google.com/o/oauth2/auth")));
        assert!(is_auth_url(&u("https://discord.com/oauth2/authorize?client_id=1")));
        assert!(is_auth_url(&u("https://discord.com/api/oauth2/authorize")));
        assert!(is_auth_url(&u("https://api.itisnow.net/popup?app=x")));
        assert!(!is_auth_url(&u("http://api.itisnow.net/popup")));
        assert!(!is_auth_url(&u("https://itisnow.net/")));
        assert!(!is_auth_url(&u("https://discord.com/channels/1")));
        assert!(!is_auth_url(&u("https://app.crixgamingvr.com/other")));
        assert!(!is_auth_url(&u("https://evil.com/api.itisnow.net")));
    }

    #[test]
    fn app_pages() {
        assert!(is_app_page(&u("http://tauri.localhost/index.html")));
        assert!(is_app_page(&u("tauri://localhost/index.html")));
        assert!(!is_app_page(&u("http://localhost/index.html")));
        assert!(!is_app_page(&u("https://crixgamingvr.com/")));
    }
}
