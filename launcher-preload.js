/**
 * The launcher page's only way to reach the computer: a handful of named requests.
 * It cannot run programs, read files or open pages by itself.
 */
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('CRIX_LAUNCHER', {
    status: () => ipcRenderer.invoke('launcher:status'),
    news: () => ipcRenderer.invoke('launcher:news'),
    install: () => ipcRenderer.invoke('launcher:install'),
    cancel: () => ipcRenderer.invoke('launcher:cancel'),
    play: () => ipcRenderer.invoke('launcher:play'),
    playWeb: () => ipcRenderer.invoke('launcher:web'),
    uninstall: () => ipcRenderer.invoke('launcher:uninstall'),
    folder: () => ipcRenderer.invoke('launcher:folder'),
    link: which => ipcRenderer.invoke('launcher:link', String(which || '')),
    version: () => ipcRenderer.sendSync('crix:version'),
    onProgress: cb => ipcRenderer.on('launcher:progress', (e, p) => cb(p)),
    onGame: cb => ipcRenderer.on('launcher:game', (e, g) => cb(g))
});
