# Board Games

Games for the [Board](https://board.fun) tabletop gaming platform.

## Games

| Game | Package | Status |
|------|---------|--------|
| [Pong](games/pong/) | `fun.board.pong` | Deployed and playable on Board hardware |

## Quick Start

```bash
make help              # show all commands
make bdb-status        # check Board connection
make build-android     # build APK (closes Unity, sets up scene automatically)
make deploy            # install + launch on Board
make logs              # stream device logs
make stop              # stop game on Board
```

## Setup (New Machine)

1. Install Unity Hub: `brew install --cask unity-hub`
2. Install Unity 6 with Android: `yes | Unity\ Hub -- --headless install --version 6000.3.8f1 --module android android-sdk-ndk-tools android-open-jdk`
3. Accept Android SDK licenses (see CLAUDE.md for full command)
4. Fix bdb: `make bdb-fix`
5. Update Board SDK path in `games/pong/Packages/manifest.json` (absolute path to `fun.board-3.2.1.tgz`)
6. Build: `make build-android`

## Board Connection

- Connect via **USB-C data cable** (not charge-only)
- No developer mode needed — dev service runs automatically
- `make bdb-status` to verify
- Games persist after disconnect (standard Android install)

## Docs

- `CLAUDE.md` — development conventions, CLI reference, gotchas
- `docs/BOARD_DEVELOPMENT_GUIDE.md` — comprehensive Board dev guide
- [Board Developer Docs](https://docs.dev.board.fun/) — official docs
- [Board Discord](https://discord.gg/KccHAYgykD) — developer community
