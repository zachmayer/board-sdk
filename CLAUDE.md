# Board Games Development Repo

## What This Is

A monorepo for developing games for the [Board](https://board.fun) tabletop gaming platform. Board is a large touchscreen tabletop that runs Android and tracks both finger touches and physical game pieces (glyphs).

## Repo Structure

```
board-sdk/
├── bin/                    # Shared tools
│   └── bdb                 # Board Developer Bridge CLI (legacy, USB-only)
├── docs/                   # Board platform development docs
│   └── BOARD_DEVELOPMENT_GUIDE.md  # Feb 2026 session guide (deploy sections predate board-connect)
├── games/                  # Game projects (each is a Unity project)
│   ├── pong/               # Pong (Unity 6, C#) — fun.board.pong
│   └── golf-wall/          # Golf Wall (Unity 6, C#) — fun.board.golfwall
├── fun.board-3.2.1.tgz     # Board SDK package (imported by Unity via relative path)
└── pyproject.toml          # Python config (for tooling/scripts)
```

## Key Tools

- **board-connect** (`~/.local/bin/board-connect`): Board's official deploy CLI — works over WiFi (HTTP API, port 8843), built for scripting and coding agents
  - `board-connect ls` - discover Boards on the network
  - `board-connect pair <host>` - one-time pairing (tap Approve on the Board)
  - `board-connect install <apk> --launch` - install and launch over WiFi
  - `board-connect logs <package> --follow` - stream logs
  - `board-connect screenshot --out shot.png` - capture the Board's screen (great for agentic visual verification)
  - `--json` flag for machine-readable output
  - Install/update: `make connect-install` (official installer, SHA256-verified)

- **bdb** (`bin/bdb`): legacy Board Developer Bridge — USB serial only, superseded by board-connect but kept as fallback
  - `bdb status` / `bdb install <apk>` / `bdb launch <pkg>` / `bdb logs <pkg>` / `bdb list`
  - If bdb won't run on macOS: `xattr -cr bin/bdb && codesign --force --deep --sign - bin/bdb`

- **Unity 6** (6000.3.8f1): Game engine. Games are C# Unity projects.
- **Board SDK** (3.2.1): Unity package providing `Board.Input` namespace for touch/glyph input. v3.3.0 is available on the dev portal (login required) — see README To Do.

## The Headless Dev Loop

Everything runs from the CLI — never open the Unity GUI:

```bash
make help              # show available commands
make test              # run unit tests (close Unity first)
make sim               # build + launch Pong on this Mac; mouse = touch
make gw-sim            # same for Golf Wall (click-drag from ball to swing)
make build-android     # build APK for Board (auto-runs setup-scene)
make deploy-wifi       # install + launch on Board over WiFi (board-connect)
make logs-wifi         # stream device logs over WiFi
make screenshot        # PNG of the Board's screen → ~/claude/scratch/board-shot.png
make deploy            # USB fallback via legacy bdb
```

`gw-` prefix = Golf Wall targets; unprefixed = Pong.

### Local Mac testing (`make sim` / `make gw-sim`)

The Board SDK's input simulator only exists inside the Unity Editor GUI, and
`BoardInput.GetActiveContacts()` compiles to return an empty array on desktop
platforms. So each game implements a small **runtime mouse fallback** (Board's own
Godot docs recommend exactly this pattern):

- Gated at runtime on `Application.isEditor || OSXPlayer/WindowsPlayer/LinuxPlayer` — never `#if UNITY_EDITOR`
- Uses the Input System package (`Mouse.current`) — legacy `UnityEngine.Input` throws because `activeInputHandler = 1`
- Inert on Board hardware (no mouse device exists there)
- Limitation: mouse can't exercise real glyph orientation/multi-touch — final validation happens on hardware via `deploy-wifi` + `screenshot` + `logs-wifi`

## Unity CLI Reference

```bash
# Run any static editor method in batch mode
UNITY -batchmode -nographics -quit -projectPath <path> -executeMethod Namespace.Class.Method -logFile -

# Key flags
-batchmode              # no GUI
-nographics             # no GPU (faster)
-quit                   # exit when done
-projectPath <path>     # Unity project root
-executeMethod <method> # call a static C# method
-buildTarget Android    # set build platform (or StandaloneOSX)
-runTests               # run test framework
-testPlatform EditMode  # or PlayMode
-logFile -              # log to stdout (use - for stdout)
```

**Critical gotchas:**
- **NEVER use `#if UNITY_EDITOR` fallbacks** — code that only runs in editor will silently fail on device. Use runtime platform checks so every code path ships. This burned us: score display worked in editor but was invisible on Board.
- **Games use Input System only** (`activeInputHandler: 1`) — legacy `UnityEngine.Input` calls throw at runtime. Use `Mouse.current` etc. from `UnityEngine.InputSystem`. Asmdef-based games (Pong) must reference `Unity.InputSystem`.
- Close Unity GUI before running CLI commands (Unity locks project files)
- Scene changes in batch mode are NOT auto-saved — must call `EditorSceneManager.SaveOpenScenes()`
- Scenes need explicit setup (game objects aren't auto-added) — run `make setup-scene` before first build
- Board SDK package path in `manifest.json` is **relative** (`file:../../../fun.board-3.2.1.tgz`) — works from clones and git worktrees without edits
- Android SDK licenses must be accepted: `yes | sdkmanager --licenses`
- If a batch build fails with "No valid Unity Editor license found" (entitlement 404s), the Hub's access token expired: run `open -ga "Unity Hub"`, wait ~30s for the licensing daemon, and retry
- Always test on device, not just the Mac sim — touch latency, glyph tracking, and GPU perf only show up on hardware

## Pause Screen Integration (Required for Board)

Games MUST implement the Board pause menu to allow users to exit:
- Call `BoardApplication.SetPauseScreenContext()` to configure
- Handle `pauseScreenActionReceived` events (Resume, ExitGameSaved, ExitGameUnsaved)
- Call `BoardApplication.Exit()` when user exits — this is fire-and-forget
- Without this, users cannot return to the Board library (must restart Board)
- All `BoardApplication` APIs are compiled no-ops off-device, so they're safe in the Mac sim build

## Board Hardware Deploy Workflow

### WiFi Deploy (default — board-connect)

One-time setup per machine/Board:
1. Board powered on, same LAN as this machine
2. `make connect-install` (installs the CLI to `~/.local/bin`)
3. `make connect-ls` to find the Board's address
4. `make connect-pair HOST=<address>` → tap **Approve** on the Board's screen

Then forever after:
```bash
make gw-build-android && make gw-deploy-wifi   # build + ship, no cable
make gw-logs-wifi                              # watch logs
make screenshot                                # see the Board's screen
```

Notes:
- Pairing token lives at `~/.config/board-connect/tokens.json`, survives reboots
- No Android developer options, no adb, no USB required — the old adb-over-TCP workflow is obsolete
- Browser alternative: `http://<board-address>:8843/` accepts drag-and-drop APK installs

### USB Deploy (legacy fallback)

1. Connect Board via USB-C accessory port (data-capable cable, not charge-only)
2. `make bdb-status` to verify connection
3. `make deploy` (or `make gw-deploy`)

## Conventions

- Games: Unity/C#, one folder per game under `games/`
- Tooling/scripts: Python (>=3.13)
- Board SDK input: use `BoardInput.GetActiveContacts()` from `Board.Input` namespace
- Desktop mouse fallback per game, runtime-gated (see Headless Dev Loop section)
- Build automation: root Makefile, per-game target prefix
- Package names: `fun.board.pong`, `fun.board.golfwall`

## New Machine Setup

1. Install Unity Hub: `brew install --cask unity-hub`
2. Install Unity with Android support: `yes | /Applications/Unity\ Hub.app/Contents/MacOS/Unity\ Hub -- --headless install --version 6000.3.8f1 --module android android-sdk-ndk-tools android-open-jdk`
3. Accept Android SDK licenses: `yes | <Unity>/PlaybackEngines/AndroidPlayer/SDK/cmdline-tools/16.0/bin/sdkmanager --licenses --sdk_root=<Unity>/PlaybackEngines/AndroidPlayer/SDK` (set JAVA_HOME to Unity's OpenJDK)
4. `make connect-install` then pair with the Board (see deploy workflow)
5. Run `make setup-scene` then `make build-android` (SDK package path is relative — no manifest edits)
6. (USB fallback only) `make bdb-fix`

## Official Docs

- Developer docs: https://docs.dev.board.fun/ (Unity SDK v3.3.0, Godot + Web SDKs in beta)
- board-connect: https://docs.dev.board.fun/tools/board-connect
- Unity simulator (editor-only): https://docs.dev.board.fun/unity/simulator
- AI assistant context for the SDK: https://docs.dev.board.fun/unity/ai-assistant
- FAQ: https://docs.dev.board.fun/faq — Developer portal: https://dev.board.fun — Discord: https://discord.gg/KccHAYgykD

## Current Status

- Pong and Golf Wall deployed and running on Board hardware (Harris_Hill_Products B5438, OS 1.4.7)
- Pause screen integration implemented (BoardApplication.Exit() + resume handling)
- Both games have desktop mouse fallbacks → `make sim` / `make gw-sim` run on the Mac
- board-connect is the deploy path (WiFi); CLI install + pairing pending first run at the Board (see README To Do)
- bdb (USB) still works as fallback; old adb-over-TCP WiFi workflow retired
- Board does NOT need developer mode — the dev service runs automatically on all retail Boards
- SDK 3.2.1 in use; 3.3.0 available on the portal (upgrade tracked in README To Do)
