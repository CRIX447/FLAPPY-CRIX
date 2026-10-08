/**
 * Flappy Crix — desktop
 *
 * Loads the live site, so pushing to the website updates every installed copy
 * on next launch. No rebuild, no reinstall, and crossplay works because it is
 * literally the same game.
 */

const { app, BrowserWindow, shell, Menu, dialog, ipcMain, net, session } = require('electron');
const path = require('path');
const fs = require('fs');
const { pathToFileURL } = require('url');

/* A copy someone downloaded will not start with a debugger or a remote
   control port switched on. Either would let another program drive the game
   from outside, or read the signed-in session out of it. The fuses set at
   build time (package.json → electronFuses) already stop --inspect and
   running the exe as plain Node; this covers Chromium's own switches.
   Development runs (npm start) are not affected. */
const DEBUG_SWITCHES = ['remote-debugging-port', 'remote-debugging-pipe', 'remote-debugging-address',
                        'remote-allow-origins', 'inspect', 'inspect-brk', 'inspect-port', 'js-flags'];
if (app.isPackaged && (DEBUG_SWITCHES.some(s => app.commandLine.hasSwitch(s)) ||
    process.argv.some(a => /^--(remote-debugging|remote-allow-origins|inspect|js-flags)/i.test(a)))) {
    process.exit(1);
}
const DEV_TOOLS = !app.isPackaged;      // no developer tools in a built copy

// Optional — the app runs fine without it, just without self-updating
let autoUpdater = null;
try { ({ autoUpdater } = require('electron-updater')); }
catch (e) { console.log('electron-updater not installed — auto-update off'); }

// CRIX_GAME_URL points a development run (npm start) at a local copy of the
// site. A built exe always loads the real one.
const GAME_URL = (!app.isPackaged && process.env.CRIX_GAME_URL) ||
                 'https://crixgamingvr.com/flappycrix';
const SITE = new URL(GAME_URL).origin;

// Only these load inside the window. Everything else opens in the real
// browser, so the app never turns into a general-purpose web browser.
const INTERNAL = ['crixgamingvr.com', 'www.crixgamingvr.com', new URL(GAME_URL).hostname];
const INTERNAL_PATHS = ['/flappycrix', '/game'];   // /flappycrix-og is the birthday season

// The site the game comes from — the only one allowed to ask for anything
function isGameOrigin(url) {
    try {
        const u = new URL(url);
        return INTERNAL.includes(u.hostname) &&
               (u.protocol === 'https:' || (!app.isPackaged && u.origin === SITE));
    } catch (e) { return false; }
}

// The "can't reach the servers" page that ships inside the app
const OFFLINE_FILE = path.join(__dirname, 'offline.html');
const OFFLINE_URL = pathToFileURL(OFFLINE_FILE).href;
const isOfflinePage = url => typeof url === 'string' && url.split(/[?#]/)[0] === OFFLINE_URL;

function isGameUrl(url) {
    try {
        const u = new URL(url);
        if (!INTERNAL.includes(u.hostname)) return false;
        return INTERNAL_PATHS.some(p => u.pathname.startsWith(p));
    } catch (e) { return false; }
}

/* Signing in. Firebase's Google sign-in opens a pop-up on
   app.crixgamingvr.com/__/auth/..., which then hops through Google; linking
   Discord navigates the game itself to Discord and back. Both have to stay
   inside the app, or the answer comes back to a browser the game cannot
   hear. 1.0.0 sent all of them to the default browser, which is why signing
   in and linking Discord never finished in the exe. */
function isAuthUrl(url) {
    try {
        const u = new URL(url);
        const h = u.hostname;
        if (u.protocol !== 'https:') return false;
        if (h === 'app.crixgamingvr.com' || h.endsWith('.firebaseapp.com')) return u.pathname.startsWith('/__/auth');
        if (h === 'accounts.google.com' || h === 'accounts.youtube.com') return true;
        if (h === 'discord.com' || h === 'discordapp.com') return /^\/(api\/)?(oauth2|login)/.test(u.pathname);
        return false;
    } catch (e) { return false; }
}

/* Google refuses to sign anyone in from a browser it does not recognise, so
   every request — not only the first page, which is all 1.0.0 covered —
   presents itself as the Chrome this Electron is built on. */
const CHROME = (process.versions.chrome || '128.0.0.0').split('.')[0];
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ' +
           `(KHTML, like Gecko) Chrome/${CHROME}.0.0.0 Safari/537.36`;
app.userAgentFallback = UA;

function openExternalSafe(url) {
    try {
        const u = new URL(url);
        if (['https:', 'http:', 'mailto:'].includes(u.protocol)) shell.openExternal(u.href);
    } catch (e) { /* not a URL — ignore */ }
}

// The page asks for these through preload.js. Only the game (or the offline
// page) may open links: a sign-in page shown in the same window must not.
ipcMain.on('crix:open-external', (e, url) => {
    const from = (e.senderFrame && e.senderFrame.url) || '';
    if (isGameUrl(from) || isOfflinePage(from)) openExternalSafe(String(url || ''));
});
ipcMain.on('crix:version', e => { e.returnValue = app.getVersion(); });

/* ---------- PERMISSIONS ----------
   Electron says yes to everything a page asks for unless it is told
   otherwise, so 1.1 let the game — or anything loaded into it — switch the
   microphone on without asking. Now the microphone and camera are asked
   about once and the answer is remembered (Help → Microphone & camera to
   change it). Only the game's own site can ask at all, and anything a game
   has no use for is refused. */
const PERMS_FILE = () => path.join(app.getPath('userData'), 'permissions.json');
function readPerms() {
    try { return JSON.parse(fs.readFileSync(PERMS_FILE(), 'utf8')) || {}; } catch (e) { return {}; }
}
function savePerms(p) {
    try { fs.writeFileSync(PERMS_FILE(), JSON.stringify(p, null, 2)); } catch (e) { console.warn('[perms]', e.message); }
}
const ALWAYS_OK = ['fullscreen', 'clipboard-sanitized-write', 'notifications'];
const DEVICE = { audio: { key: 'microphone', what: 'your microphone', why: 'for voice chat' },
                 video: { key: 'camera',     what: 'your camera',     why: 'for video calls in CRIX Chat' } };

let asking = Promise.resolve();          // one question on screen at a time
function askDevice(type, parent) {
    const d = DEVICE[type];
    asking = asking.then(async () => {
        const saved = readPerms()[d.key];
        if (saved === 'allow' || saved === 'deny') return saved === 'allow';   // answered while queued
        const r = await dialog.showMessageBox(parent && !parent.isDestroyed() ? parent : undefined, {
            type: 'question', title: 'Flappy Crix',
            message: `Let Flappy Crix use ${d.what}?`,
            detail: `It is only used ${d.why}, and only while you have it switched on.`,
            buttons: ['Allow', "Don't allow"], defaultId: 0, cancelId: 1,
            checkboxLabel: 'Remember my choice', checkboxChecked: true
        });
        const ok = r.response === 0;
        if (r.checkboxChecked) savePerms(Object.assign(readPerms(), { [d.key]: ok ? 'allow' : 'deny' }));
        return ok;
    });
    return asking;
}

function setupPermissions(ses) {
    ses.setPermissionRequestHandler(async (contents, permission, callback, details) => {
        const from = (details && (details.requestingUrl || details.securityOrigin)) || (contents && contents.getURL()) || '';
        if (!isGameOrigin(from)) return callback(false);
        if (ALWAYS_OK.includes(permission)) return callback(true);
        if (permission !== 'media') return callback(false);
        const types = ((details && details.mediaTypes) || []).filter(t => DEVICE[t]);
        if (!types.length) return callback(false);       // screen capture goes elsewhere
        const parent = contents ? BrowserWindow.fromWebContents(contents) : null;
        for (const t of types) {
            const saved = readPerms()[DEVICE[t].key];
            const ok = saved === 'allow' ? true : saved === 'deny' ? false : await askDevice(t, parent);
            if (!ok) return callback(false);
        }
        callback(true);
    });
    // What a page sees when it checks without asking. A device not yet
    // decided reads as allowed, so the game goes on to ask (and we prompt);
    // one the player said no to reads as blocked.
    ses.setPermissionCheckHandler((contents, permission, origin, details) => {
        if (!isGameOrigin(origin)) return false;
        if (ALWAYS_OK.includes(permission)) return true;
        if (permission === 'media') {
            const d = DEVICE[details && details.mediaType];
            return !d || readPerms()[d.key] !== 'deny';
        }
        return false;
    });
}

function resetDevicePermissions() {
    const p = readPerms();
    delete p.microphone; delete p.camera;
    savePerms(p);
    dialog.showMessageBox(win, { type: 'info', title: 'Flappy Crix',
        message: 'Microphone and camera reset.',
        detail: 'The game will ask again next time it needs them.' });
}

// Sign-in pop-ups: a small window of their own, still able to hand the
// result back to the game that opened it.
const AUTH_POPUP = {
    action: 'allow',
    overrideBrowserWindowOptions: {
        width: 500, height: 700,
        autoHideMenuBar: true,
        backgroundColor: '#FFFFFF',
        title: 'Sign in',
        icon: path.join(__dirname, 'icon.ico'),
        webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true, devTools: DEV_TOOLS }
    }
};

let win = null;

function loadGame() {
    if (win) win.loadURL(GAME_URL);
}

/* When the servers cannot be reached, a page that says so and keeps trying
   on its own — rather than 1.0.0's warning box, which could open behind a
   window that had never been shown, so it looked like nothing happened. */
function showOffline(reason) {
    if (!win) return;
    win.loadFile(path.join(__dirname, 'offline.html'), {
        query: { reason: reason || '', url: GAME_URL }
    });
    if (!win.isVisible()) win.show();
}

function createWindow() {
    win = new BrowserWindow({
        width: 520,
        height: 900,
        minWidth: 380,
        minHeight: 640,
        backgroundColor: '#000000',
        title: 'Flappy Crix',   // plain, no suffix
        icon: path.join(__dirname, 'icon.ico'),
        autoHideMenuBar: true,
        show: false,
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false,
            sandbox: true,
            devTools: DEV_TOOLS,
            backgroundThrottling: false   // keeps the game running when unfocused
        }
    });

    loadGame();
    // The page sets its own title; keep ours fixed
    win.on('page-title-updated', e => e.preventDefault());

    // Avoids a white flash before the page paints — and if it never paints
    // (a very slow connection), show the window anyway rather than nothing.
    win.once('ready-to-show', () => win.show());
    setTimeout(() => { if (win && !win.isVisible()) win.show(); }, 4000);

    win.webContents.setWindowOpenHandler(({ url }) => {
        if (isAuthUrl(url)) return AUTH_POPUP;
        openExternalSafe(url);
        return { action: 'deny' };
    });

    win.webContents.on('will-navigate', (e, url) => {
        // The game, the sign-in round trip and the waiting page stay here.
        // No other file on the computer can be opened in the window.
        if (isGameUrl(url) || isAuthUrl(url) || isOfflinePage(url)) return;
        e.preventDefault();
        if (!url.startsWith('file:')) openExternalSafe(url);
    });

    win.webContents.on('did-fail-load', (e, code, desc, url, isMainFrame) => {
        if (!isMainFrame) return;
        // -3 is ERR_ABORTED: this load was replaced by another one (a
        // redirect, a reload, the game moving to /flappycrix-og). Nothing
        // failed. 1.0.0 told players it could not reach the servers.
        if (code === -3) return;
        // A sign-in page that failed is the sign-in's problem, not the game's
        if (!isGameUrl(url)) return;
        showOffline(desc);
    });

    // The page crashed or was killed — bring the game back
    win.webContents.on('render-process-gone', (e, details) => {
        if (details.reason !== 'clean-exit') loadGame();
    });

    win.on('closed', () => { win = null; });
}

// Pop-ups (sign-in) follow the same rules as the main window
app.on('web-contents-created', (e, contents) => {
    // Nothing gets to embed another page with its own rules
    contents.on('will-attach-webview', ev => ev.preventDefault());
    if (contents.getType() !== 'window') return;
    contents.on('did-create-window', child => {
        child.webContents.setWindowOpenHandler(({ url }) => {
            if (isAuthUrl(url)) return AUTH_POPUP;
            openExternalSafe(url);
            return { action: 'deny' };
        });
    });
});

// A minimal menu — the default one exposes devtools and page reload shortcuts
Menu.setApplicationMenu(Menu.buildFromTemplate([
    {
        label: 'Game',
        submenu: [
            { label: 'Reload', accelerator: 'F5', click: loadGame },
            { label: 'Fullscreen', accelerator: 'F11',
              click: () => win?.setFullScreen(!win.isFullScreen()) },
            { type: 'separator' },
            { label: 'CRIX Chat', accelerator: 'Ctrl+Shift+C', click: openChat },
            { type: 'separator' },
            { role: 'quit' }
        ]
    },
    {
        label: 'Links',
        submenu: [
            { label: 'Website', click: () => openExternalSafe('https://crixgamingvr.com') },
            { label: 'Discord', click: () => openExternalSafe('https://discord.com/invite/MbQvJGDAst') },
            { label: 'All links', click: () => openExternalSafe('https://crixgamingvr.com/bio') }
        ]
    },
    {
        label: 'Help',
        submenu: [
            { label: 'Game files', click: () => shell.openPath(app.getPath('userData')) },
            { label: 'Microphone & camera: ask again', click: resetDevicePermissions },
            { label: 'Check for updates',
              click: () => autoUpdater
                ? autoUpdater.checkForUpdates().catch(() => {})
                : dialog.showMessageBox(win, { message: 'Auto-update is not available in this build.' }) },
            { type: 'separator' },
            { label: 'Terms of Service', click: () => openExternalSafe('https://crixgamingvr.com/terms') },
            { label: 'Privacy Policy', click: () => openExternalSafe('https://crixgamingvr.com/privacy') },
            { type: 'separator' },
            { label: 'About', click: () => dialog.showMessageBox(win, {
                type: 'info', title: 'Flappy Crix',
                message: 'Flappy Crix ' + app.getVersion(),
                detail: 'CRIX STUDIOS\n\nInspired by Flappy Bird by Dong Nguyen.\n' +
                        'The game updates itself — you never need to reinstall.'
            }) }
        ]
    }
]));

// One window only
if (!app.requestSingleInstanceLock()) {
    app.quit();
} else {
    app.on('second-instance', () => {
        if (win) { if (win.isMinimized()) win.restore(); win.focus(); }
    });
    app.whenReady().then(() => {
        setupPermissions(session.defaultSession);
        createWindow();
        smokeTest();
        setupUpdates();
        watchGameVersion();     // 1.0.0 started this twice
    });
}

/* ---------- GAME VERSION WATCH ----------
   The game is loaded from the website, so a push changes it underneath a
   running client. Rather than leaving someone on a stale build that may not
   match the servers, offer to restart. net.fetch goes through Chromium's
   network stack, so it uses the same proxy and certificates as the game —
   Node's https module, which 1.0.0 used, does not. */
let knownVersion = null;

function watchGameVersion() {
    const check = async () => {
        try {
            const r = await net.fetch(GAME_URL + '?cb=' + Date.now(), { cache: 'no-store' });
            if (!r.ok) return;
            const m = (await r.text()).match(/const GAME_VERSION = '([^']+)'/);
            if (!m) return;
            const v = m[1];

            if (knownVersion === null) { knownVersion = v; return; }
            if (v === knownVersion) return;
            knownVersion = v;

            const res = await dialog.showMessageBox(win, {
                type: 'info',
                title: 'Update available',
                message: `Flappy Crix ${v} is out.`,
                detail: 'Restart to get the new version. Your progress is saved.',
                buttons: ['Restart now', 'Later'],
                defaultId: 0,
                cancelId: 1
            });
            if (res.response === 0) loadGame();
        } catch (e) { /* offline — try again next time */ }
    };
    check();
    setInterval(check, 10 * 60 * 1000);
}

/* ---------- SELF-UPDATE ----------
   The GAME itself updates from the website every launch. This only handles
   the wrapper — permissions, the icon, native changes — which is rare. */
function setupUpdates() {
    if (!autoUpdater || !app.isPackaged) return;
    autoUpdater.autoDownload = true;
    autoUpdater.autoInstallOnAppQuit = true;

    autoUpdater.on('update-downloaded', info => {
        dialog.showMessageBox(win, {
            type: 'info',
            title: 'Update ready',
            message: `Flappy Crix ${info.version} is ready to install.`,
            detail: 'It will be applied next time you close the game, or restart now.',
            buttons: ['Restart now', 'Later'],
            defaultId: 0
        }).then(r => { if (r.response === 0) autoUpdater.quitAndInstall(); });
    });

    autoUpdater.on('error', e => console.warn('[update]', e?.message));

    autoUpdater.checkForUpdates().catch(() => {});
    setInterval(() => autoUpdater.checkForUpdates().catch(() => {}), 60 * 60 * 1000);
}

/* ---------- CRIX CHAT ---------- */
// Its own window, sharing the session so you are already signed in
let chatWin = null;
function openChat() {
    if (chatWin) { chatWin.focus(); return; }
    chatWin = new BrowserWindow({
        width: 440, height: 800, backgroundColor: '#0A0A0F',
        title: 'CRIX Chat', autoHideMenuBar: true,
        icon: path.join(__dirname, 'icon.ico'),
        webPreferences: { contextIsolation: true, nodeIntegration: false, sandbox: true, devTools: DEV_TOOLS }
    });
    chatWin.loadURL(SITE + '/crixchat');
    // Chat and its sign-in stay here; any other link opens in the browser
    chatWin.webContents.on('will-navigate', (e, url) => {
        try {
            const u = new URL(url);
            if ((INTERNAL.includes(u.hostname) && u.pathname.startsWith('/crixchat')) || isAuthUrl(url)) return;
        } catch (err) {}
        e.preventDefault();
        if (!url.startsWith('file:')) openExternalSafe(url);
    });
    chatWin.on('page-title-updated', e => e.preventDefault());
    chatWin.on('closed', () => { chatWin = null; });
    chatWin.webContents.setWindowOpenHandler(({ url }) => {
        if (isAuthUrl(url)) return AUTH_POPUP;
        openExternalSafe(url);
        return { action: 'deny' };
    });
}

/* ---------- SMOKE TEST ----------
   The build checks that the finished exe really starts: it runs it with
   CRIX_SMOKE_TEST set to a file name, and the app writes down what it
   loaded there and quits. Nothing happens unless that is set. */
function smokeTest() {
    const out = process.env.CRIX_SMOKE_TEST;
    if (!out || !win) return;
    const finish = (ok, url) => {
        try { fs.writeFileSync(out, JSON.stringify({ ok, url, version: app.getVersion(),
              electron: process.versions.electron, devTools: DEV_TOOLS })); } catch (e) {}
        app.exit(ok ? 0 : 3);
    };
    win.webContents.on('did-finish-load', () => {
        const url = win.webContents.getURL();
        if (isGameUrl(url) || isOfflinePage(url)) setTimeout(() => finish(true, url), 1500);
    });
    setTimeout(() => finish(false, win ? win.webContents.getURL() : ''), 90000);
}

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (!win) createWindow(); });
