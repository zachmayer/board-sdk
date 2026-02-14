# Board Games Development Repo

## What This Is

A monorepo for developing games for the [Board](https://board.fun) tabletop gaming platform. Board is a large touchscreen tabletop that runs Android and tracks both finger touches and physical game pieces (glyphs).

## Repo Structure

```
board-sdk/
├── bin/                    # Shared tools
│   └── bdb                 # Board Developer Bridge CLI
├── docs/                   # Board platform development docs
│   └── BOARD_DEVELOPMENT_GUIDE.md  # Comprehensive dev guide
├── games/                  # Game projects (each is a Unity project)
│   └── pong/               # Pong game (Unity 6, C#)
│       ├── Assets/Scripts/Pong/  # Game source code
│       ├── Makefile         # Build/test/deploy automation
│       └── README.md        # Game-specific docs
├── fun.board-3.2.1.tgz     # Board SDK package (imported by Unity)
└── pyproject.toml           # Python config (for tooling/scripts)
```

## Key Tools

- **bdb** (`bin/bdb`): Board Developer Bridge - deploys and manages apps on Board hardware
  - `bdb status` - check Board connection
  - `bdb install <apk>` - install APK to Board
  - `bdb launch <package>` - launch app
  - `bdb logs <package>` - stream logs
  - `bdb list` - list installed apps
  - If bdb won't run on macOS: `xattr -cr bin/bdb && codesign --force --deep --sign - bin/bdb`

- **Unity 6** (6000.3.8f1): Game engine. Games are C# Unity projects.
- **Board SDK** (3.2.1): Unity package providing `Board.Input` namespace for touch/glyph input.

## Working With Games

There's a root-level Makefile that handles build/test/deploy for all games.

```bash
make help              # show available commands
make test              # run unit tests (close Unity first)
make setup-scene       # setup scene objects (run before first build)
make build-android     # build APK for Board (auto-runs setup-scene)
make deploy            # install and launch on Board
make logs              # stream device logs
make bdb-status        # check Board connection
make bdb-fix           # fix bdb macOS permissions on a new machine
```

## Unity CLI Reference

Almost everything can be done via CLI — avoid the Unity GUI when possible.

```bash
# Run any static editor method in batch mode
UNITY -batchmode -nographics -quit -projectPath <path> -executeMethod Namespace.Class.Method -logFile -

# Key flags
-batchmode              # no GUI
-nographics             # no GPU (faster)
-quit                   # exit when done
-projectPath <path>     # Unity project root
-executeMethod <method> # call a static C# method
-buildTarget Android    # set build platform
-runTests               # run test framework
-testPlatform EditMode  # or PlayMode
-logFile -              # log to stdout (use - for stdout)
```

**Critical gotchas:**
- **NEVER use `#if UNITY_EDITOR` fallbacks** — code that only runs in editor will silently fail on device. Always create real UI/objects that work everywhere. This burned us: score display worked in editor but was invisible on Board.
- Close Unity GUI before running CLI commands (Unity locks project files)
- Scene changes in batch mode are NOT auto-saved — must call `EditorSceneManager.SaveOpenScenes()`
- Scenes need explicit setup (game objects aren't auto-added) — run `make setup-scene` before first build
- Board SDK package path in `manifest.json` is absolute — must update when switching machines
- Android SDK licenses must be accepted: `yes | sdkmanager --licenses`
- USB cable must be data-capable (not charge-only). If Board loses power, restart it and reconnect.
- Always test on device, not just simulator — things that look fine in editor can fail on hardware

## Pause Screen Integration (Required for Board)

Games MUST implement the Board pause menu to allow users to exit:
- Call `BoardApplication.SetPauseScreenContext()` to configure
- Handle `pauseScreenActionReceived` events (Resume, ExitGameSaved, ExitGameUnsaved)
- Call `BoardApplication.Exit()` when user exits — this is fire-and-forget
- Without this, users cannot return to the Board library (must restart Board)

## Board Hardware Deploy Workflow

### USB Deploy (default)
1. Connect Board via USB-C accessory port
2. `make bdb-status` to verify connection
3. `make deploy`

### WiFi Deploy (no cable needed)

Once set up, you can build and deploy without a USB cable:
```bash
make gw-build-android    # build the APK
make gw-deploy-wifi      # install and launch over WiFi
make adb-status          # check WiFi connection
make adb-connect         # reconnect after Board reboot
```

**One-time setup** (requires USB cable the first time):

1. Connect Board via USB-C cable
2. `bdb launch com.android.settings` to open Android settings on the Board's screen
3. Scroll to the bottom, tap **System**
4. Tap **About phone**
5. Tap **Build number** 7 times — you'll see "You are now a developer"
6. Go back to **System** → **Developer options** (new menu item)
7. Turn ON:
   - **USB debugging**
   - **Wireless debugging**
   - **Disable ADB authorization timeout** (so you don't have to re-auth)
8. A prompt will appear on the Board screen: "Allow USB debugging?" — tap **Always allow from this computer**, then **Allow**
9. From your Mac, enable TCP mode and connect:
   ```bash
   ADB=/Applications/Unity/Hub/Editor/6000.3.8f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
   $ADB tcpip 5555
   $ADB connect 192.168.1.203:5555   # Board IP (may change with DHCP)
   ```
10. Unplug the USB cable — WiFi deploy now works

**Notes:**
- `bdb` only works over USB serial. `adb` works over USB or WiFi. They don't conflict.
- After Board reboot, re-run `make adb-connect` to reconnect WiFi.
- Board IP is currently `192.168.1.203` but may change if DHCP assigns a new address.
- To find the Board's current IP: plug in USB, run `$ADB shell ip addr show wlan0`.

## Conventions

- Games: Unity/C#, one folder per game under `games/`
- Tooling/scripts: Python (>=3.13)
- Board SDK input: use `BoardInput.GetActiveContacts()` from `Board.Input` namespace
- Build automation: Makefile per game project
- Package name for pong: `fun.board.pong`

## New Machine Setup

When setting up on a new machine:
1. Install Unity Hub: `brew install --cask unity-hub`
2. Install Unity with Android support: `yes | /Applications/Unity\ Hub.app/Contents/MacOS/Unity\ Hub -- --headless install --version 6000.3.8f1 --module android android-sdk-ndk-tools android-open-jdk`
3. Accept Android SDK licenses: `yes | <Unity>/PlaybackEngines/AndroidPlayer/SDK/cmdline-tools/16.0/bin/sdkmanager --licenses --sdk_root=<Unity>/PlaybackEngines/AndroidPlayer/SDK` (set JAVA_HOME to Unity's OpenJDK)
4. Fix bdb: `make bdb-fix`
5. Update Board SDK path in `games/pong/Packages/manifest.json` (absolute path to `fun.board-3.2.1.tgz`)
6. Run `make setup-scene` then `make build-android`

## Current Status

- Pong deployed and running on Board hardware (Harris_Hill_Products B5438, OS 1.4.7)
- Golf Wall deployed — golf game using glyph piece detection for swing input
- Pause screen integration implemented (BoardApplication.Exit() + resume handling)
- bdb CLI is working (signed and permissions fixed)
- USB debugging and WiFi debugging enabled on Board (Developer options unlocked)
- WiFi ADB confirmed working at 192.168.1.203:5555
- Board does NOT need developer mode — the dev service runs automatically on all retail Boards
