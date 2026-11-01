# Running Cargo Jam in the iOS Simulator

The Simulator runs the real iOS build on the Mac: no iPhone, cable, Apple ID or signing needed.
Use it for layout, the notch and home-bar spacing, menus and flows, and to check that the iOS build
works. Don't use it to judge smoothness on a 1080p monitor (the phone is scaled down to fit) or
for frame rate, memory or battery (it runs on the Mac's chip). For those, use a real iPhone
([iphone-testing.md](iphone-testing.md)).

This Mac already has Xcode 26.6 with the iOS 26.5 Simulator (iPhone 17, 17 Pro, 17 Pro Max, Air, 17e).

## A. One-time setup

### A1. Add iOS support to Unity (10–15 minutes)

1. Save your work and quit the Unity editor (the Hub can install while it is open, but the editor
   only sees the new module after a restart).
2. Open **Unity Hub** and click **Installs** in the left sidebar.
3. Find **Unity 6000.6.7f1**, click its **⚙ gear** icon and choose **Add modules**.
4. Tick **iOS Build Support** (about 1–2 GB) and click **Install**. Accept the license if asked.
   Wait for the progress bar in the Hub to finish.
5. Open the CargoJam project again from the Hub.

Check: in Unity, **File → Build Profiles** lists **iOS** with no "Install with Unity Hub" button.

### A2. Point the Mac's developer tools at Xcode (1 minute)

Open Terminal and run (it asks for your Mac login password; nothing shows while you type):

    sudo xcode-select -s /Applications/Xcode.app/Contents/Developer

Check: `xcode-select -p` prints `/Applications/Xcode.app/Contents/Developer`.

### A3. Open Xcode once

Open **Xcode** from Applications. If it asks to accept a license or install components, accept and
let it finish, then quit Xcode.

## B. Build the Simulator version in Unity

### B1. Switch the project to iOS (5–15 minutes, first time only)

1. **File → Build Profiles**.
2. In the **Platforms** list on the left, click **iOS**.
3. Click **Switch Platform** (bottom right). Unity reimports every asset for iOS. Don't touch the
   editor until the progress bar is gone.

The editor now previews with the iPhone quality settings (Mobile), which is what the phone uses.

### B2. Tell Unity to build for the Simulator

1. In the Build Profiles window, with iOS selected, click **Player Settings…**
   (or **Edit → Project Settings → Player**).
2. Make sure the **iOS** tab (phone icon) is selected.
3. Open **Other Settings** and scroll to **Target SDK** (next to *Target minimum iOS Version*).
4. Change **Device SDK** to **Simulator SDK**. If a **Simulator Architecture** setting is shown,
   leave it on **Arm64** (this Mac has an M4).

This changes `ProjectSettings/ProjectSettings.asset`, so git shows it as modified. Set it back
to **Device SDK** before building for a real iPhone.

### B3. Build the Xcode project

1. Back in **File → Build Profiles** (iOS selected):
   - Tick **Development Build** to get the home screen's **Levels** button (and logs in Xcode).
   - Leave everything else as it is.
2. Click **Build** (not *Build And Run*).
3. In the save dialog, go to the Desktop, click **New Folder**, name it `CargoJam-iOS-Sim`, select it
   and click **Choose**. Keep it outside the repo, and separate from any phone build folder:
   a Simulator build can't run on a phone and the other way round.
4. Wait for the build to finish (2–5 minutes). Finder opens the folder; it contains
   `Unity-iPhone.xcodeproj` among other files.

Unity doesn't make the app itself. It writes an Xcode project, and Xcode compiles and runs it.

## C. Run it from Xcode

1. Double-click **`Unity-iPhone.xcodeproj`** in that folder. Xcode opens it.
2. Look at the bar at the top centre of the window. It reads something like
   **Unity-iPhone › Any iOS Simulator Device**.
   - The left half is the *scheme*: it must be **Unity-iPhone** (not *UnityFramework*). Click it to
     change it if needed.
   - Click the right half (the device) and choose **iPhone 17 Pro** under **iOS Simulators**.
3. Press **▶** (top left) or **⌘R**.
   - The first build compiles all of the game's code to native code: expect 3–8 minutes. The
     progress shows in the same top bar. Later builds take under a minute.
   - The **Simulator** app opens, boots the iPhone (the first boot takes a minute) and launches
     Cargo Jam.
4. Stop the game with **■** (or **⌘.**) in Xcode.

### Using the Simulator

| To | Do |
|---|---|
| Tap | Click |
| Drag or scroll | Click and drag |
| Go to the home screen | **⇧⌘H**, or Device → Home |
| See the phone at its real size | Window → **Physical Size** (⌘1) |
| Fit it on your screen | Window → **Fit Screen** |
| Save a full-resolution screenshot | **⌘S** (saved to the Desktop) |
| Start again with a fresh save | Long-press the app icon → Remove App, then ▶ in Xcode |
| Try another phone size | Pick another iPhone in Xcode's device menu and ▶ again |

Logs from the game (`Debug.Log`, errors) appear in Xcode's console at the bottom (**⇧⌘C** if hidden).

## D. After you change code

1. In Unity, **File → Build Profiles → Build** again and pick the same `CargoJam-iOS-Sim` folder.
2. When asked, choose **Append** (keeps Xcode's settings). Choose **Replace** if a build fails oddly.
3. In Xcode, press **▶**.

## E. If something goes wrong

| What you see | Fix |
|---|---|
| iOS shows "Install with Unity Hub" in Build Profiles | A1 isn't done, or Unity wasn't restarted after it |
| Xcode: "Building for iOS Simulator, but linking in object file built for iOS" | Target SDK is still *Device SDK*: do B2, then build with **Replace** |
| No simulators in Xcode's device menu | Xcode → Settings → **Components** → download the iOS Simulator |
| Xcode asks for a Team or signing | Make sure a *simulator* is selected as the device, not *Any iOS Device* |
| "Unable to boot the Simulator" | In Simulator: Device → **Erase All Content and Settings**; else restart the Mac |
| The app opens to a black screen or closes | Read the red lines in Xcode's console (⇧⌘C) and send them to Claude |
| Build fails with a code error | Send the first red error from Xcode's Issue navigator (⌘5) to Claude |

## Going back to the Mac editor setup

The editor stays on iOS after B1, which is fine for everyday work. To build for a real iPhone, set
**Target SDK** back to **Device SDK** and follow [iphone-testing.md](iphone-testing.md).
