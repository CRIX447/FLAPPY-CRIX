/**
 * Flappy Crix launcher — installs, updates and starts the Unity version of the game.
 *
 * The game builds are the "Flappy Crix (Unity)" releases on this repository's GitHub page
 * (tags starting unity-v). Only PUBLISHED releases count: a draft is invisible here, so nobody
 * gets a build until it is published.
 *
 * Files:  <userData>/game/            the installed game (FlappyCrix.exe and its data)
 *         <userData>/game/version.json which release that is
 *
 * Downloads only come from GitHub over https, are written to a temporary folder, unpacked with
 * the tar.exe built into Windows 10 and 11 (which refuses paths that try to escape the folder),
 * and only then swapped in, so a failed or cancelled download never breaks the installed game.
 */
'use strict';
const { app, net, shell } = require('electron');
const fs = require('fs');
const path = require('path');
const { spawn, execFile } = require('child_process');

const REPO = 'CRIX447/FLAPPY-CRIX';
const TAG_PREFIX = 'unity-v';
const ASSET = {
    win32: 'FlappyCrix-windows.zip',
    darwin: 'FlappyCrix-mac.zip',
    linux: 'FlappyCrix-linux.zip'
}[process.platform];
const EXE = {
    win32: 'FlappyCrix.exe',
    darwin: 'FlappyCrix.app',
    linux: 'FlappyCrix.x86_64'
}[process.platform];
const DOWNLOAD_HOSTS = ['github.com', 'objects.githubusercontent.com', 'release-assets.githubusercontent.com'];
const MAX_BYTES = 3 * 1024 * 1024 * 1024;     // 3 GB: anything bigger is not our game

const gameDir = () => path.join(app.getPath('userData'), 'game');
const versionFile = () => path.join(gameDir(), 'version.json');

let busy = false;
let running = null;           // the game process while it runs
let cancelWanted = false;

function installed() {
    try {
        const v = JSON.parse(fs.readFileSync(versionFile(), 'utf8'));
        if (v && v.tag && fs.existsSync(path.join(gameDir(), EXE))) return v;
    } catch (e) { /* not installed */ }
    return null;
}

/** The newest published game build, or null (no internet, or nothing published yet). */
async function latest() {
    const r = await net.fetch(`https://api.github.com/repos/${REPO}/releases?per_page=30`, {
        headers: { Accept: 'application/vnd.github+json', 'User-Agent': 'FlappyCrix-Launcher' },
        cache: 'no-store'
    });
    if (!r.ok) throw new Error('GitHub answered ' + r.status);
    const list = await r.json();
    for (const rel of Array.isArray(list) ? list : []) {
        if (rel.draft || rel.prerelease || !String(rel.tag_name || '').startsWith(TAG_PREFIX)) continue;
        const a = (rel.assets || []).find(x => x.name === ASSET);
        if (a) return { tag: rel.tag_name, name: rel.name || rel.tag_name, url: a.browser_download_url, size: a.size || 0, notes: rel.body || '' };
    }
    return null;
}

async function status() {
    const have = installed();
    let newest = null, error = null;
    try { newest = await latest(); } catch (e) { error = e.message; }
    return {
        supported: !!ASSET,
        installed: have ? have.tag : null,
        latest: newest ? newest.tag : null,
        latestName: newest ? newest.name : null,
        size: newest ? newest.size : 0,
        update: !!(have && newest && newest.tag !== have.tag),
        running: !!running,
        busy,
        error
    };
}

function okDownloadUrl(u) {
    try {
        const x = new URL(u);
        return x.protocol === 'https:' && DOWNLOAD_HOSTS.includes(x.hostname);
    } catch (e) { return false; }
}

/** Downloads to a file, following GitHub's redirect, reporting progress (0..1). */
async function download(url, file, total, progress) {
    if (!okDownloadUrl(url)) throw new Error('Refusing to download from ' + url);
    const r = await net.fetch(url, { headers: { 'User-Agent': 'FlappyCrix-Launcher' }, redirect: 'follow' });
    if (!r.ok || !r.body) throw new Error('Download failed (' + r.status + ')');
    if (r.url && !okDownloadUrl(r.url)) throw new Error('The download was sent somewhere unexpected');
    const size = Number(r.headers.get('content-length')) || total || 0;
    const out = fs.createWriteStream(file);
    const reader = r.body.getReader();
    let got = 0;
    try {
        for (;;) {
            if (cancelWanted) throw new Error('cancelled');
            const { done, value } = await reader.read();
            if (done) break;
            got += value.length;
            if (got > MAX_BYTES) throw new Error('The download is far bigger than expected');
            if (!out.write(Buffer.from(value))) await new Promise(ok => out.once('drain', ok));
            progress(size ? got / size : 0, got, size);
        }
    } finally {
        await new Promise(ok => out.end(ok));
        try { reader.releaseLock(); } catch (e) { }
    }
}

function unzip(zip, dir) {
    return new Promise((ok, fail) => {
        fs.mkdirSync(dir, { recursive: true });
        // Windows 10+ and macOS ship bsdtar, which unpacks zips and refuses ".." and absolute paths.
        const tool = process.platform === 'linux' ? 'unzip' : 'tar';
        const args = tool === 'unzip' ? ['-q', zip, '-d', dir] : ['-xf', zip, '-C', dir];
        execFile(tool, args, { windowsHide: true }, err => err ? fail(new Error('Could not unpack the game: ' + err.message)) : ok());
    });
}

function removeDir(dir) {
    try { fs.rmSync(dir, { recursive: true, force: true, maxRetries: 3 }); } catch (e) { }
}

/** Installs or updates to the newest build. progress({ step, p, got, size }). */
async function install(progress) {
    if (busy) throw new Error('Already busy');
    if (running) throw new Error('Close the game first');
    if (!ASSET) throw new Error('The launcher can install the game on Windows, Mac and Linux only');
    busy = true; cancelWanted = false;
    const tmp = path.join(app.getPath('temp'), 'flappycrix-' + Date.now());
    try {
        progress({ step: 'Checking for the newest version…', p: 0 });
        const rel = await latest();
        if (!rel) throw new Error('No game build has been published yet. Check back soon!');
        fs.mkdirSync(tmp, { recursive: true });
        const zip = path.join(tmp, ASSET);
        await download(rel.url, zip, rel.size, (p, got, size) => progress({ step: 'Downloading ' + rel.name, p: p * 0.9, got, size }));
        progress({ step: 'Installing…', p: 0.92 });
        const unpacked = path.join(tmp, 'game');
        await unzip(zip, unpacked);
        if (!fs.existsSync(path.join(unpacked, EXE))) throw new Error('The download did not contain ' + EXE);
        fs.writeFileSync(path.join(unpacked, 'version.json'), JSON.stringify({ tag: rel.tag, name: rel.name, at: new Date().toISOString() }));
        // swap: the old game is only removed once the new one is complete
        const old = gameDir() + '-old';
        removeDir(old);
        if (fs.existsSync(gameDir())) fs.renameSync(gameDir(), old);
        try { fs.renameSync(unpacked, gameDir()); }
        catch (e) {
            // a different drive: copy instead of moving
            fs.cpSync(unpacked, gameDir(), { recursive: true });
        }
        removeDir(old);
        if (process.platform !== 'win32') { try { fs.chmodSync(path.join(gameDir(), EXE), 0o755); } catch (e) { } }
        progress({ step: 'Ready to play!', p: 1 });
        return rel.tag;
    } finally {
        busy = false;
        removeDir(tmp);
    }
}

function cancel() { cancelWanted = true; }

/** Starts the game. onExit(code) when it closes. */
function play(onExit) {
    const have = installed();
    if (!have) throw new Error('Install the game first');
    if (running) return;
    const exe = path.join(gameDir(), EXE);
    const p = process.platform === 'darwin'
        ? spawn('open', ['-W', exe], { cwd: gameDir(), detached: false })
        : spawn(exe, [], { cwd: gameDir(), detached: false, windowsHide: false });
    running = p;
    p.on('exit', code => { running = null; if (onExit) onExit(code); });
    p.on('error', e => { running = null; if (onExit) onExit(-1, e.message); });
}

function uninstall() {
    if (running) throw new Error('Close the game first');
    if (busy) throw new Error('Already busy');
    removeDir(gameDir());
}

function openFolder() {
    fs.mkdirSync(gameDir(), { recursive: true });
    shell.openPath(gameDir());
}

module.exports = { status, install, cancel, play, uninstall, openFolder, isRunning: () => !!running };
