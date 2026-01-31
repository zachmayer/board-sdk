# Pong for Board

A minimal Pong game for the [Board](https://board.fun) tabletop gaming platform.

## Status

- [x] Basic Pong gameplay (ball, paddles, scoring)
- [x] Board SDK touch input integration
- [x] Tweakable settings via ScriptableObject
- [x] Unit tests (edit mode + play mode)
- [ ] Test on actual Board hardware
- [ ] Add sound effects
- [ ] Polish visuals

## Quick Start

```bash
# In Unity
1. Open project in Unity 6
2. Menu: Board > Pong > Setup Scene
3. Press Play
4. Menu: Board > Input > Simulator
5. Enable simulation, place fingers, drag to move paddles
```

## Development

```bash
make help       # Show all commands
make test       # Run tests
make build      # Verify compilation
make clean      # Remove artifacts
```

## Tweaking

Menu: **Board > Pong > Open Settings** to adjust:
- Ball speed, max speed
- Paddle responsiveness, size
- Winning score
- Serve delay

Changes apply in real-time during play.

## Files

```
Assets/Scripts/Pong/
├── PongSettings.cs      # Tweakable game settings
├── PongGame.cs          # Main controller, Board SDK input
├── PongBall.cs          # Ball movement
├── PongPaddle.cs        # Paddle that follows touch
├── Editor/
│   └── PongEditorSetup.cs
└── Tests/
    ├── PongTests.cs           # Edit mode tests
    └── PongPlayModeTests.cs   # Play mode tests
```

## Board SDK

- Touch input: `BoardInput.GetActiveContacts(BoardContactType.Finger)`
- Screen: 1920x1080, coordinates in pixels
- Simulator: Board > Input > Simulator
