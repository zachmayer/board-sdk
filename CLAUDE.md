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

1. Board does NOT need a separate "developer mode" — the dev service runs automatically. Just connect via USB-C accessory port.
2. Connect Board to computer
3. `make bdb-status` to verify connection
4. `make deploy`

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
- Pause screen integration implemented (BoardApplication.Exit() + resume handling)
- bdb CLI is working (signed and permissions fixed)
- Board does NOT need developer mode — the dev service runs automatically on all retail Boards
