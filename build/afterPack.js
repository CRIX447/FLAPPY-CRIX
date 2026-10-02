/**
 * Puts the Flappy Crix icon (and name) into the Windows exe.
 *
 * electron-builder normally does this itself, but only when
 * "signAndEditExecutable" is on — and that makes it download its code
 * signing toolkit, which will not unpack on Windows without Developer Mode
 * ("Cannot create symbolic link"). With it off, the exe kept Electron's own
 * icon: on the desktop shortcut, in the taskbar and in Task Manager.
 *
 * This runs right after the app is packed, before the installer and the
 * portable exe are made, so both carry the icon. rcedit is the same tool
 * electron-builder would have used, without the toolkit around it.
 */
const path = require('path');

exports.default = async function afterPack(context) {
    if (context.electronPlatformName !== 'win32') return;
    const rcedit = require('rcedit');
    const info = context.packager.appInfo;
    const exe = path.join(context.appOutDir, `${info.productFilename}.exe`);
    const icon = path.join(context.packager.projectDir, 'icon.ico');
    await rcedit(exe, {
        icon,
        'file-version': info.shortVersion || info.version,
        'product-version': info.shortVersion || info.version,
        'version-string': {
            ProductName: info.productName,
            FileDescription: info.productName,
            CompanyName: 'CRIX STUDIOS',
            LegalCopyright: '© CRIX STUDIOS',
            OriginalFilename: `${info.productFilename}.exe`
        }
    });
    console.log(`  • icon set on ${path.basename(exe)}`);
};
