# Handoff Prompt for Claude

Copy and paste this into Claude on your other laptop:

---

I have a Board game development repo at `~/source/board-sdk`. Please read the CLAUDE.md and docs/BOARD_DEVELOPMENT_GUIDE.md to get up to speed.

The Pong game is feature-complete and working in the Unity editor. My Board hardware has updated firmware. I need help with:

1. **Put the Board into developer mode.** Check the Board developer docs at https://docs.dev.board.fun for how to enable dev mode. I may need to walk you through what's on screen.

2. **Deploy the Pong game to the Board.** The workflow is:
   - Close Unity if it's open
   - `make build-android` to build the APK
   - `make bdb-status` to verify the Board is connected
   - `make deploy` to install and launch
   - `make logs` to watch for errors

3. **If bdb doesn't run**, fix macOS permissions with: `make bdb-fix`

The bdb CLI tool is at `bin/bdb`. The game source is in `games/pong/`. The Android package name is currently `com.DefaultCompany.Myproject`.

---
