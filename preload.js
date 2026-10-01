/**
 * Runs before the page loads, in an isolated context.
 * Tells the game it is running as the desktop app, and gives it the one
 * thing a page cannot do for itself: open a link in the real browser.
 *
 * 1.0.0 listed this file in the build but it was never in the folder, so
 * the game could not tell it was in the app at all.
 */
const { contextBridge, ipcRenderer } = require('electron');

let appVersion = '';
try { appVersion = ipcRenderer.sendSync('crix:version'); } catch (e) {}

contextBridge.exposeInMainWorld('CRIX_DESKTOP', {
    isDesktop: true,
    platform: process.platform,          // win32 | darwin | linux
    version: appVersion,                 // this app, e.g. 1.1.0
    electron: process.versions.electron,
    openExternal: url => ipcRenderer.send('crix:open-external', String(url || ''))
});
