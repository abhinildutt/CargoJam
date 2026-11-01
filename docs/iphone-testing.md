# Testing Cargo Jam on an iPhone

## One-time setup

1. **Unity iOS module.** Unity Hub → Installs → 6000.6.7f1 → ⚙ → Add modules → **iOS Build Support**.
2. **Point the command-line tools at Xcode** (it is installed, but the system uses the standalone tools):
   `sudo xcode-select -s /Applications/Xcode.app/Contents/Developer`, then open Xcode once and let it
   install the iOS platform if it asks (Xcode → Settings → Components).
3. **Apple ID in Xcode.** Xcode → Settings → Accounts → + → Apple ID. A free account works
   (apps it signs expire after 7 days; reinstall to renew).
4. **iPhone.** Connect it with a cable and tap *Trust*. Turn on Settings → Privacy & Security →
   **Developer Mode** (the phone restarts).

## Each build

1. Unity → File → Build Profiles → **iOS** → *Switch Platform* (the first switch reimports for a while).
2. Tick **Development Build** to get the home screen's *Levels* button; leave it off to see what players see.
3. *Build*, into a folder outside the repo (e.g. `~/Desktop/CargoJam-iOS`).
4. Open `Unity-iPhone.xcodeproj`. Select the **Unity-iPhone** target → *Signing & Capabilities* →
   tick *Automatically manage signing*, pick your Team. If the bundle id `com.abhinildutt.cargojam`
   is refused, change it to anything unique.
5. Pick the iPhone in the toolbar and press **Run** (⌘R).
6. First install with a free account: on the phone, Settings → General → VPN & Device Management →
   trust your developer certificate, then open the app.

Later builds: in Unity choose *Build* → *Append* to the same folder, then Run again in Xcode.

## Measuring on the phone

- While it runs from Xcode, the **Debug navigator** (⌘7) shows CPU, Memory, Energy Impact and FPS live.
- Product → Profile → **Game Performance** (Instruments) for frame pacing, GPU time and thermal state.
- Settings → Developer → **Graphics HUD** on the phone shows FPS, GPU time and memory over the game.

## Benchmark on the Mac (no phone needed)

A development build started with `-perfbench <report.txt> [-perfbench-level N] [-perfbench-no-saver]`
renders at 1170×2532 (iPhone 13–15) with the iPhone render settings, samples the home screen for
10 s, plays level N by itself for 25 s, sits idle on it for 15 s and writes a report (see `PerfBench.cs`). The build must use the Mobile
quality level and have Frame Timing Stats on.

Results on an M4 Mac (2026-11-01, level 38):

| | Home | Level 38 |
|---|---|---|
| Frame rate | 59.3 fps (capped at 60) | 59.8 fps |
| CPU main thread | 1.5 ms avg | 1.8 ms avg, 2.5 ms p95 |
| GPU frame | 2.1 ms avg | 2.3 ms avg, 3.3 ms p95 |
| Draw calls / SetPass | 154 / 24 | 190 (p95 252) / 24 |
| Triangles | 32k | 39k |
| Managed heap | 12 MB | 12 MB, no per-frame garbage at p95 |
| Meshes | 1.4 MB | 4.3 MB |

On a phone, graphics memory is about 57 MB for the 4× MSAA color target, 7 MB of bloom buffers,
2 MB of shadow map and about 8 MB of fonts and textures (the benchmark's own capture targets are left out).

## Battery saver

`FramePacer` draws every other frame (30 fps) once nothing has moved for 2.5 s, and full rate
again on the next frame after a touch or any animation (input is still read 60 times a second).
The home screen's convoy keeps it at 60 there, so the saving is on the board between moves.

Idle on level 38 for 15 s, M4 (2026-11-01), including the 2.5 s at full rate after the last move:

| | Saver off | Saver on |
|---|---|---|
| Frames drawn | 59.7 fps | 37.0 fps (30 once settled) |
| GPU busy | 169 ms/s | 107 ms/s (−37%) |
| CPU busy, all threads | 271 ms/s | 192 ms/s (−29%) |
