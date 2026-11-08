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

**Flappy Crix → Build → Windows / Mac / Linux / Android (APK) / iOS (Xcode project).**
The finished game appears in `unity/Builds/…` and its folder opens. (Mac and iOS builds need a Mac.)

## What's in this version (phase 1)

| | |
|---|---|
| Boot | Your CRIX STUDIOS screen instead of Unity's (Unity's own splash is switched off) |
| Loading | A real loading screen: progress bar, what's loading, tips |
| Title | Giant glitchy FLAPPY CRIX logo over the game playing itself, PRESS ANY BUTTON |
| Main menu | Big slanted buttons (PLAY, PLAY ONLINE, SETTINGS, CREDITS, QUIT) with your level, XP, best, coins and games on the right |
| The game | The website's exact rules (60 ticks a second), drawn with sprites: night sky, three layers of city, neon pipes, spinning coins, glowing bird and trail, particles on flaps, coins and crashes, screen shake |
| Screen effects | Glow (bloom), colour split, glitch, vignette, scanlines and film grain. Settings → Effects: High / Low / Off |
| Wider screens | You see further ahead instead of black bars; tall phones see more sky |
| Display | Fullscreen by default. Settings → Display: Fullscreen / Windowed / Exclusive, plus resolution, V-sync and frame rate. **F11** or **Alt+Enter** switches at any time |
| Controls | Space / W / Up / click / tap / gamepad A to flap · Esc / P / Start to pause · menus work with mouse, keyboard, touch and gamepad |
| Clips | **F9** records a 60 fps clip with sound, **F12** saves a screenshot (computers). Clips use **FFmpeg** (free, open source); on Windows the game downloads it the first time you press F9 |
| Saves | Best, coins, games, level and XP on this device (`Flappy Crix → Open Save Folder`) |

## Coming next (phase 2)

Signing in to your crixgamingvr.com account (same progress as the website), **online play with
website players** over the same Photon servers (the C# connection already exists in the Gorilla
Tag mod and comes over), the store, locker, daily rewards, awards, seasonal events and the
menu style you pick.

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
