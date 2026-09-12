/**
 * Flappy Crix — desktop
 *
 * Loads the live site, so pushing to the website updates every installed copy
 * on next launch. No rebuild, no reinstall, and crossplay works because it is
 * literally the same game.
 */

const { app, BrowserWindow, shell, Menu, dialog, ipcMain } = require('electron');
const path = require('path');

// Optional — the app runs fine without it, just without self-updating
let autoUpdater = null;
try { ({ autoUpdater } = require('electron-updater')); }
catch (e) { console.log('electron-updater not installed — auto-update off'); }

const GAME_URL = 'https://crixgamingvr.com/flappycrix';

// Only these load inside the window. Everything else opens in the real
// browser, so the app never turns into a general-purpose web browser.
const INTERNAL = ['crixgamingvr.com', 'www.crixgamingvr.com'];
const INTERNAL_PATHS = ['/flappycrix', '/game.html'];

let win = null;

// Domains involved in signing in. These must load in-app so the redirect
// can return, unlike ordinary external links.
const AUTH_HOSTS = [
    'accounts.google.com', 'apis.google.com', 'ssl.gstatic.com',
    'discord.com', 'discordapp.com',
    'flappy-crix.firebaseapp.com', 'app.crixgamingvr.com',
    'identitytoolkit.googleapis.com'
];
function isAuthUrl(url) {
    try { return AUTH_HOSTS.some(h => new URL(url).hostname.endsWith(h)); }
    catch (e) { return false; }
}

function isGameUrl(url) {
    try {
        const u = new URL(url);
        if (!INTERNAL.includes(u.hostname)) return false;
        return INTERNAL_PATHS.some(p => u.pathname.startsWith(p));
    } catch (e) { return false; }
}

// Google sign-in refuses to load in an unrecognised user agent, so present a
// normal Chrome string rather than the default Electron one.
const UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ' +
           '(KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36';

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
            backgroundThrottling: false   // keeps the game running when unfocused
        }
    });

    win.loadURL(GAME_URL, { userAgent: UA });
    // The page sets its own title; keep ours fixed
    win.on('page-title-updated', e => e.preventDefault());

    // Avoids a white flash before the page paints
    win.once('ready-to-show', () => win.show());

    // Anything that is not the game opens in the default browser
    win.webContents.setWindowOpenHandler(({ url }) => {
        // Everything opens in the default browser — including sign-in, which
        // Google refuses to do inside an embedded window.
        shell.openExternal(url);
        return { action: 'deny' };
    });


    win.webContents.on('will-navigate', (e, url) => {
        // Only the game itself loads in this window
        if (isGameUrl(url)) return;
        e.preventDefault();
        shell.openExternal(url);
    });

    // A blank window with no explanation is the worst failure mode
    win.webContents.on('did-fail-load', (e, code, desc, url, isMainFrame) => {
        if (!isMainFrame) return;
        dialog.showMessageBox(win, {
            type: 'warning',
            title: 'Could not connect',
            message: 'Flappy Crix could not reach the servers.',
            detail: `${desc}\n\nCheck your internet connection and try again.`,
            buttons: ['Retry', 'Quit']
        }).then(r => {
            if (r.response === 0) win.loadURL(GAME_URL, { userAgent: UA });
            else app.quit();
        });
    });

    win.on('closed', () => { win = null; });
}

// A minimal menu — the default one exposes devtools and page reload shortcuts
Menu.setApplicationMenu(Menu.buildFromTemplate([
    {
        label: 'Game',
        submenu: [
            { label: 'Reload', accelerator: 'F5',
              click: () => win?.loadURL(GAME_URL, { userAgent: UA }) },
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
            { label: 'Website', click: () => shell.openExternal('https://crixgamingvr.com') },
            { label: 'Discord', click: () => shell.openExternal('https://discord.com/invite/MbQvJGDAst') },
            { label: 'All links', click: () => shell.openExternal('https://crixgamingvr.com/bio') }
        ]
    },
    {
        label: 'Help',
        submenu: [
            { label: 'Game files', click: () => shell.openPath(app.getPath('userData')) },
            { label: 'Check for updates',
              click: () => autoUpdater
                ? autoUpdater.checkForUpdates().catch(() => {})
                : dialog.showMessageBox(win, { message: 'Auto-update is not available in this build.' }) },
            { type: 'separator' },
            { label: 'Terms of Service', click: () => shell.openExternal('https://crixgamingvr.com/terms') },
            { label: 'Privacy Policy', click: () => shell.openExternal('https://crixgamingvr.com/privacy') },
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
        createWindow();
        setupUpdates();
        watchGameVersion();
        watchGameVersion();
    });
}

/* ---------- GAME VERSION WATCH ----------
   The game is loaded from the website, so a push changes it underneath a
   running client. Rather than leaving someone on a stale build that may not
   match the servers, offer to restart. */
let knownVersion = null;

function watchGameVersion() {
    const https = require('https');
    const check = () => {
        https.get('https://crixgamingvr.com/flappycrix?cb=' + Date.now(), r => {
            let b = '';
            r.on('data', d => b += d);
            r.on('end', () => {
                const m = b.match(/const GAME_VERSION = '([^']+)'/);
                if (!m) return;
                const v = m[1];

                if (knownVersion === null) { knownVersion = v; return; }
                if (v === knownVersion) return;
                knownVersion = v;

                dialog.showMessageBox(win, {
                    type: 'info',
                    title: 'Update available',
                    message: `Flappy Crix ${v} is out.`,
                    detail: 'Restart to get the new version. Your progress is saved.',
                    buttons: ['Restart now', 'Later'],
                    defaultId: 0,
                    cancelId: 1
                }).then(res => {
                    if (res.response === 0) win.reload();
                });
            });
        }).on('error', () => {});
    };
    check();
    setInterval(check, 10 * 60 * 1000);
}

/* ---------- SELF-UPDATE ----------
   The GAME itself updates from the website every launch. This only handles
   the wrapper — permissions, the icon, native changes — which is rare. */
function setupUpdates() {
    if (!autoUpdater) return;
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
        webPreferences: { contextIsolation: true, nodeIntegration: false }
    });
    chatWin.loadURL('https://crixgamingvr.com/crixchat', { userAgent: UA });
    chatWin.on('page-title-updated', e => e.preventDefault());
    chatWin.on('closed', () => { chatWin = null; });
    chatWin.webContents.setWindowOpenHandler(({ url }) => {
        if (isAuthUrl(url)) return { action: 'allow' };
        shell.openExternal(url);
        return { action: 'deny' };
    });
}

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('activate', () => { if (!win) createWindow(); });
