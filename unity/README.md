# Flappy Crix — Unity version

CRIX STUDIOS · the next Flappy Crix app for Windows, Mac, Linux, Android and iOS.

## Open it

1. Install **Unity Hub**, then **Unity 6 LTS** (any `6000.0.x` version, the newest is best) with the
   build support you want (Windows, Mac, Linux, Android, iOS).
2. Unity Hub → **Add** → **Add project from disk** → pick this `unity` folder.
   If the Hub says the version is different, pick your Unity 6 version and press **Continue**.
3. The first time it opens, the project sets itself up: you'll see
   `[Flappy Crix] Project set up. Press Play to run the game.` in the Console.
4. Press **Play** ▶. (Nothing needs to be in the scene: the game builds itself when it starts.)

If something looks wrong after an update: **Flappy Crix → Set Up Project**.

## Build it

**Flappy Crix → Build → Windows / Mac / Linux / Android (APK) / iOS (Xcode project) / Web (WebGL).**
The finished game appears in `unity/Builds/…` and its folder opens. (Mac and iOS builds need a Mac.)

## Build it on GitHub (no Unity needed on your PC)

GitHub can build every version for you and put the downloads on a draft release.
It needs your free Unity account, once:

1. **Get your Unity licence file.** Open Unity Hub, sign in, go to *Settings → Licenses* and make
   sure there's a **Personal** licence (Add → Get a free personal licence). The licence file is then at
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`

   Open it with Notepad and copy **everything** in it.
2. **Add three secrets** on GitHub: the FLAPPY-CRIX repo → *Settings → Secrets and variables →
   Actions → New repository secret*:
   - `UNITY_LICENSE`: paste the whole licence file
   - `UNITY_EMAIL`: the email you sign in to Unity with
   - `UNITY_PASSWORD`: your Unity password

   (Secrets can't be seen again by anyone, not even in the build logs.)
3. **Build:** *Actions → Build Unity game → Run workflow* (leave "all", or type e.g. `windows,android`).
   The first build takes about 30-60 minutes; later ones are faster.
4. **Download:** *Releases* → the new **draft** → `FlappyCrix-windows.zip`, `FlappyCrix-mac.zip`,
   `FlappyCrix-linux.zip`, `FlappyCrix-Android.apk`, `FlappyCrix-webgl.zip`. Press **Publish** when you're happy to share it.
   Once it's published, the **Flappy Crix launcher** (the Windows app) offers it to everyone as an update.
5. **Web version (HD):** turn on *Settings → Pages → Source: GitHub Actions* once, and every build also
   puts the browser version online at your GitHub Pages address. It plays solo; online stays in the main
   web game for now (browsers don't allow the network connections the online code uses).

If a build goes red, open the run, click the red job and copy the error to Claude.

## Phase 1: the game

| | |
|---|---|
| Boot | Your CRIX STUDIOS screen instead of Unity's (Unity's own splash is switched off) |
| Loading | A real loading screen: progress bar, what's loading, tips |
| Title | Giant glitchy FLAPPY CRIX logo over the game playing itself, PRESS ANY BUTTON |
| Main menu | Laid out like the website: the menu card in the middle, you on the left, score and controls on the right. Its own layout for computers, phones held sideways and phones held upright |
| The game | The website's exact rules (60 ticks a second), drawn with sprites: night sky, three layers of city, neon pipes, spinning coins, glowing bird and trail, particles on flaps, coins and crashes, screen shake |
| Screen effects | Glow (bloom), colour split, glitch, vignette, scanlines and film grain. Settings → Effects: High / Low / Off |
| Wider screens | You see further ahead instead of black bars; tall phones see more sky |
| Display | Fullscreen by default. Settings → Display: Fullscreen / Windowed / Exclusive, plus resolution, V-sync and frame rate. **F11** or **Alt+Enter** switches at any time |
| Controls | Space / W / Up / click / tap / gamepad A to flap · Esc / P / Start to pause · menus work with mouse, keyboard, touch and gamepad |
| Clips | **F9** records a 60 fps clip with sound, **F12** saves a screenshot (computers). Clips use **FFmpeg** (free, open source); on Windows the game downloads it the first time you press F9 |
| Saves | Best, coins, games, level and XP on this device (`Flappy Crix → Open Save Folder`) |

## Phase 2: online, shop, daily, awards, account

| | |
|---|---|
| Account | **ACCOUNT** → LINK MY ACCOUNT shows a code and a QR code: open flappycrix.vercel.app/link on a phone or PC, sign in, type the code. The game links by itself. Coins, level, best, hats, trails and awards are then the same here and on the website (saved to your account) |
| Play online | **PLAY ONLINE** plays with website players in the same Photon rooms: room list, join with a 4-letter code, create a room (Free Play, Race, Last One Standing, Coin Rush; anyone or code-only) |
| Room | Players with their colours and levels, the host picks the mode and starts, website players' chat shows up (filtered like on the site) |
| Match | Other players' birds fly next to you with their names; live standings, race target, Coin Rush clock, who's left; respawns like on the site; the host can end Free Play; results with the winner (+10 XP for a win, Coin Rush pays double coins) |
| Connection | Runs in the background and keeps itself up: if it drops it reconnects by itself (2 s, 4 s ... up to 30 s) |
| Shop | Power-ups (2X coins, shield, magnet), hats and trails at the website's prices; WEAR / WEARING doubles as the locker. Hats sit on the bird, trails change colour |
| Daily | 7-day reward streak (100 → 600 coins) and the seasonal calendar doors with their prizes |
| Awards | All 16 of the website's awards with progress bars (the secret ones stay secret) |

Not yet: ranked and the website-only event modes (Candy Hunt, Presents, Egg Hunt) stay on the website,
typing in chat (the website checks chat on its server first), friends and voice.

## Where things are

```
Assets/FlappyCrix/
  Scripts/Core     the website's game rules (shared with the Gorilla Tag mod)
  Scripts/Game     GameRoot (screens and flow), World (drawing), PostFx, Sfx, Settings, ClipRecorder
  Scripts/UI       the menus, built in code
  Scripts/Editor   project setup and the Build menu
  Resources        the site's art, sounds, music and font, the CRIX STUDIOS logo, the shaders
```

Inspired by Flappy Bird by Dong Nguyen. Not affiliated with or endorsed by Dong Nguyen or .Gears.
Made with AI (Claude by Anthropic).
