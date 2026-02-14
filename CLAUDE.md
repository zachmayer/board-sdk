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

- **Unity 6** (6000.3.6f1): Game engine. Games are C# Unity projects.
- **Board SDK** (3.2.1): Unity package providing `Board.Input` namespace for touch/glyph input.

## Working With Games

There's a root-level Makefile that handles build/test/deploy for all games.

```bash
make help              # show available commands
make test              # run unit tests (close Unity first)
make build-android     # build APK for Board
make deploy            # install and launch on Board
make logs              # stream device logs
make bdb-fix           # fix bdb macOS permissions on a new machine
```

## Board Hardware Deploy Workflow

1. Board must be in **developer mode** (check Board docs at docs.dev.board.fun)
2. Connect Board to computer
3. `make bdb-status` to verify connection
4. `make deploy`

## Conventions

- Games: Unity/C#, one folder per game under `games/`
- Tooling/scripts: Python (>=3.13)
- Board SDK input: use `BoardInput.GetActiveContacts()` from `Board.Input` namespace
- Build automation: Makefile per game project
- Package name for pong is currently `com.DefaultCompany.Myproject` (needs updating in Unity project settings)

## Current Status

- Pong game is feature-complete and working in Unity editor/simulator
- Next step: deploy to actual Board hardware (need dev mode setup)
- bdb CLI is working (signed and permissions fixed)
