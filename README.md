# Board Games

Games for the [Board](https://board.fun) tabletop gaming platform.

## Games

| Game | Package | Status |
|------|---------|--------|
| [Pong](games/pong/) | `fun.board.pong` | Deployed and playable on Board hardware |
| [Golf Wall](games/golf-wall/) | `fun.board.golfwall` | Deployed — glyph-piece swing input |

## The Dev Loop (fully headless)

No Unity GUI, no cables:

```bash
make gw-test           # run tests (close Unity first)
make gw-sim            # build + launch on this Mac — click and drag to play
make gw-build-android  # build the APK
make gw-deploy-wifi    # install + launch on Board over WiFi
make gw-logs-wifi      # stream device logs
make screenshot        # capture the Board's screen to a PNG
```

Pong uses the same targets without the `gw-` prefix (`make sim`, `make deploy-wifi`, ...).
`make help` lists everything.

## WiFi Deploy Setup (one-time)

[board-connect](https://docs.dev.board.fun/tools/board-connect) is Board's official LAN
deploy tool (HTTP API on port 8843 — no adb, no cables, no Android developer options):

```bash
make connect-install               # install the board-connect CLI
make connect-ls                    # discover Boards on the network
make connect-pair HOST=<address>   # then tap "Approve" on the Board's screen
```

Pairing stores a bearer token in `~/.config/board-connect/tokens.json` and persists
across reboots. If the Board's DHCP address changes, rediscover with `make connect-ls`.

## Setup (New Machine)

1. Install Unity Hub: `brew install --cask unity-hub`
2. Install Unity 6 with Android: `yes | Unity\ Hub -- --headless install --version 6000.3.8f1 --module android android-sdk-ndk-tools android-open-jdk`
3. Accept Android SDK licenses (see CLAUDE.md for full command)
4. Install and pair board-connect (see WiFi Deploy Setup above)
5. Build: `make build-android` (the Board SDK package path is repo-relative — no manifest edits needed)

## To Do

- [ ] Power the Board on, then run `make connect-install`, `make connect-ls`, `make connect-pair HOST=<addr>` and verify `make gw-deploy-wifi` end to end (CLI not yet installed/paired — needs a human to approve on-screen)
- [ ] Upgrade Board SDK 3.2.1 → 3.3.0 (download `fun.board-3.3.0.tgz` from [dev.board.fun](https://dev.board.fun) — requires portal login; 3.3.0 auto-enforces Landscape Left orientation and adds simulator overlays)
- [ ] Delete the stray `package/` directory at repo root (partial extraction of the SDK tgz — regenerable, safe to remove)
- [ ] Decide whether to commit `uv.lock`

## USB Fallback (legacy)

`bdb` (`bin/bdb`) is Board's legacy USB-serial tool, superseded by board-connect but
still functional: `make deploy`, `make logs`, `make bdb-status`. Requires a
data-capable USB-C cable.

## Docs

- `CLAUDE.md` — development conventions, CLI reference, gotchas
- `docs/BOARD_DEVELOPMENT_GUIDE.md` — Feb 2026 session guide (deploy sections predate board-connect)
- [Board Developer Docs](https://docs.dev.board.fun/) — official: [Unity quick start](https://docs.dev.board.fun/unity/getting-started/quick-start), [simulator](https://docs.dev.board.fun/unity/simulator), [board-connect](https://docs.dev.board.fun/tools/board-connect), [AI assistant context](https://docs.dev.board.fun/unity/ai-assistant)
- [Board Discord](https://discord.gg/KccHAYgykD) — developer community
