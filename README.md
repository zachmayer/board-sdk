# Pong for Board

Minimal Pong for the [Board](https://board.fun) tabletop gaming platform.

## Quick Start

```bash
# In Unity 6
1. Board > Pong > Setup Scene
2. Press Play
3. Board > Input > Simulator
4. Enable simulation, drag fingers to move paddles
```

## Development

```bash
make help        # show commands
make build       # verify compilation (close Unity first)
make test        # run unit tests (close Unity first)
```

## Tweaking

**Board > Pong > Open Settings** - changes apply in real-time:

| Setting | Effect |
|---------|--------|
| Ball Speed | starting speed |
| Ball Speed Increase | acceleration per hit |
| Paddle Responsiveness | how fast paddle follows finger |
| Paddle Angle Influence | 0 = straight, 1 = steep angles |
| Winning Score | points to win |
| Enable Sound | toggle audio |

## Files

```
Assets/Scripts/Pong/
├── PongGame.cs      # game loop, input, audio
├── PongBall.cs      # ball physics
├── PongPaddle.cs    # paddle movement
├── PongSettings.cs  # tweakable settings
└── Editor/
    └── PongEditorSetup.cs  # menu commands
```

## How It Works

- Touch input via `BoardInput.GetActiveContacts(BoardContactType.Finger)`
- Left side touches → blue paddle, right side → orange paddle
- Ball bounces continuously, scores on back wall hits
- First to winning score wins

## Board SDK

- Screen: 1920×1080
- Simulator: Board > Input > Simulator
- Docs: https://docs.dev.board.fun
