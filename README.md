# Board Games

Games for the [Board](https://board.fun) tabletop gaming platform.

## Games

| Game | Description | Status |
|------|-------------|--------|
| [Pong](games/pong/) | Classic 2-player Pong with touch controls | Working in editor, ready for hardware deploy |

## Setup

1. Install [Unity Hub](https://unity.com/download) and Unity 6 (6000.3.6f1) with Android support
2. Fix bdb permissions: `xattr -cr bin/bdb && codesign --force --deep --sign - bin/bdb`
3. Open a game project in Unity (e.g. `games/pong/`)
4. Import Board SDK via Package Manager (tarball: `fun.board-3.2.1.tgz`)

## Deploying to Board

```bash
bin/bdb status           # check connection
cd games/pong
make build-android       # build APK (close Unity first)
make deploy              # install + launch on Board
make logs                # stream device logs
```

## Docs

See `docs/BOARD_DEVELOPMENT_GUIDE.md` for comprehensive Board development documentation.
