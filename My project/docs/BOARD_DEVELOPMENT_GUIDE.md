# Board Game Development Guide

**Created:** February 11, 2026
**Unity Version:** 6000.3.6f1 (Unity 6 LTS)
**Board SDK Version:** 3.2.1
**macOS:** Darwin 25.2.0

This document captures everything learned from a Board development session, including setup, tooling, code architecture, and lessons learned.

---

## Table of Contents

1. [What is Board?](#what-is-board)
2. [Development Environment Setup](#development-environment-setup)
3. [Board SDK Integration](#board-sdk-integration)
4. [Understanding Board Input](#understanding-board-input)
5. [Build & Deploy Workflow](#build--deploy-workflow)
6. [Complete Game Code: Pong](#complete-game-code-pong)
7. [Testing Strategy](#testing-strategy)
8. [Lessons Learned](#lessons-learned)
9. [Ideas for Future Games](#ideas-for-future-games)
10. [Resources & Links](#resources--links)

---

## What is Board?

Board is a tabletop gaming platform that combines physical game pieces with a digital touchscreen surface. Key features:

- **Large touchscreen surface** - acts as the game board
- **Multi-touch input** - tracks multiple finger contacts simultaneously
- **Glyph recognition** - can identify and track special game pieces (called "glyphs") placed on the surface
- **Runs Android** - games are built as APKs and deployed like Android apps

### Hardware Specs (from SDK docs)

- Display resolution: varies by model
- Touch points: supports many simultaneous contacts
- Glyph tracking: identifies piece orientation and unique IDs

---

## Development Environment Setup

### Prerequisites

1. **Unity Hub** - Download from [unity.com/download](https://unity.com/download)
2. **Unity 6000.x** (Unity 6 LTS) with:
   - Android Build Support
   - Android SDK & NDK
   - OpenJDK
3. **Board SDK** - Download from [dev.board.fun](https://dev.board.fun)

### Installing Unity

```bash
# Unity Hub handles installation
# Add Android build support during install or via Hub > Installs > Add Modules
```

Required modules:
- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

### Installing the Board SDK

1. Download the SDK package from the developer portal: `fun.board-X.X.X.tgz`
2. In Unity: **Window > Package Manager**
3. Click **+** dropdown > **Add package from tarball**
4. Select the downloaded `.tgz` file

**Package manifest entry** (after installation):
```json
{
  "dependencies": {
    "fun.board": "file:/path/to/fun.board-3.2.1.tgz"
  }
}
```

### Installing bdb (Board Developer Bridge)

`bdb` is the command-line tool for deploying and debugging apps on Board hardware.

1. Download from: [dev.board.fun/downloads/bdb/macos-universal/bdb](https://dev.board.fun/downloads/bdb/macos-universal/bdb)
2. Place in your project's `bin/` directory

**macOS Gatekeeper Fix** (IMPORTANT):

The downloaded binary is quarantined and unsigned. Fix with:

```bash
# Remove extended attributes and ad-hoc sign
xattr -cr /path/to/bdb
codesign --force --deep --sign - /path/to/bdb

# Verify it works
./bdb help
```

If you still get Gatekeeper errors:
1. System Settings > Privacy & Security
2. Scroll to bottom, find "bdb was blocked"
3. Click "Allow Anyway"
4. Run bdb again, click "Open" in dialog

### bdb Commands Reference

```bash
bdb help                    # Show all commands
bdb version                 # Get Board OS version
bdb status                  # Check device connection
bdb install <apk>           # Install APK to device
bdb launch <package>        # Launch installed app
bdb stop <package>          # Stop running app
bdb logs <package>          # Stream logs (Ctrl+C to stop)
bdb remove <package>        # Uninstall app
bdb cleanup                 # Remove all dev-installed apps
bdb list                    # List non-system apps
bdb list-ports              # Debug: show serial ports
```

---

## Board SDK Integration

### Project Structure

```
My project/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity
│   ├── Scripts/
│   │   └── Pong/
│   │       ├── PongGame.cs
│   │       ├── PongBall.cs
│   │       ├── PongPaddle.cs
│   │       ├── PongSettings.cs
│   │       ├── Editor/
│   │       │   ├── BuildScript.cs
│   │       │   └── PongEditorSetup.cs
│   │       └── Tests/
│   │           ├── PongTests.cs
│   │           └── PongPlayModeTests.cs
│   ├── Resources/
│   │   └── PongSettings.asset
│   └── Samples/
│       └── Board SDK/3.2.1/Input/    # SDK samples
├── Build/
│   └── Pong.apk
├── bin/
│   └── bdb
├── Makefile
└── Packages/
    └── manifest.json
```

### Importing SDK Samples

After installing the SDK package:
1. **Window > Package Manager**
2. Find "Board SDK" in the list
3. Expand **Samples**
4. Import **Input** sample (critical for understanding the input system)

The Input sample includes:
- `BoardInputManager.cs` - demonstrates contact handling
- `BoardContactDebugInfo.cs` - visualizes touch/glyph contacts
- Demo scene with prefabs

---

## Understanding Board Input

### Core Concepts

The Board SDK provides input through the `Board.Input` namespace:

```csharp
using Board.Input;
```

### BoardContact Structure

Each contact (finger touch or glyph) is represented as a `BoardContact`:

```csharp
BoardContact contact = ...;

contact.contactId       // Unique ID for this contact instance
contact.type            // BoardContactType.Finger or BoardContactType.Glyph
contact.phase           // BoardContactPhase (Began, Moved, Stationary, Ended, Canceled)
contact.screenPosition  // Vector2 in screen coordinates
contact.orientation     // Float, rotation in radians (glyphs only)
contact.glyphId         // String, unique glyph identifier (glyphs only)
```

### Contact Types

```csharp
BoardContactType.Finger  // Human finger touch
BoardContactType.Glyph   // Recognized game piece
```

### Contact Phases

```csharp
BoardContactPhase.Began      // Contact just started
BoardContactPhase.Moved      // Contact position changed
BoardContactPhase.Stationary // Contact unchanged since last frame
BoardContactPhase.Ended      // Contact lifted normally
BoardContactPhase.Canceled   // Contact interrupted (system event, etc.)
```

### Getting Active Contacts

```csharp
// Get all finger touches
BoardContact[] fingers = BoardInput.GetActiveContacts(BoardContactType.Finger);

// Get all glyphs on the surface
BoardContact[] glyphs = BoardInput.GetActiveContacts(BoardContactType.Glyph);
```

### Processing Contacts Pattern

```csharp
private Dictionary<int, int> contactToPaddle = new Dictionary<int, int>();

private void ProcessTouchInput()
{
    BoardContact[] contacts = BoardInput.GetActiveContacts(BoardContactType.Finger);

    foreach (var contact in contacts)
    {
        // Convert screen position to world position
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(
            new Vector3(contact.screenPosition.x, contact.screenPosition.y, 10));

        switch (contact.phase)
        {
            case BoardContactPhase.Began:
                // New touch - assign to game element
                contactToPaddle[contact.contactId] = DetermineAssignment(worldPos);
                break;

            case BoardContactPhase.Moved:
            case BoardContactPhase.Stationary:
                // Update position
                if (contactToPaddle.TryGetValue(contact.contactId, out int assigned))
                {
                    UpdateElement(assigned, worldPos);
                }
                break;

            case BoardContactPhase.Ended:
            case BoardContactPhase.Canceled:
                // Clean up
                contactToPaddle.Remove(contact.contactId);
                break;
        }
    }
}
```

### Testing Without Hardware: Board Simulator

1. In Unity, go to **Board > Input > Simulator**
2. Click **Enable Simulation** in the Simulator window
3. In Play mode:
   - Select finger/glyph tool from palette
   - Click in Game view to place contacts
   - **Drag** placed contacts to move them
   - Click contact again to remove (lift finger)

---

## Build & Deploy Workflow

### Makefile

A Makefile automates the build/test/deploy cycle:

```makefile
# Pong for Board - Makefile
# Run `make` or `make help` to see available targets

UNITY := /Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity
PROJECT := $(shell pwd)
BDB := $(PROJECT)/bin/bdb
APK := $(PROJECT)/Build/Pong.apk
RESULTS_DIR := /tmp/pong-tests

.PHONY: help test test-edit test-play build build-mac build-android deploy logs stop bdb-status clean

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

test: test-edit ## Run all tests (alias for test-edit)

test-edit: ## Run edit mode tests (fast, no game running)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PROJECT)" \
		-runTests -testPlatform EditMode \
		-testResults $(RESULTS_DIR)/edit.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/edit.xml"

test-play: ## Run play mode tests (slower, runs game objects)
	@mkdir -p $(RESULTS_DIR)
	$(UNITY) -batchmode -nographics -projectPath "$(PROJECT)" \
		-runTests -testPlatform PlayMode \
		-testResults $(RESULTS_DIR)/play.xml \
		-logFile -
	@echo "Results: $(RESULTS_DIR)/play.xml"

build: ## Verify project compiles (close Unity first)
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-logFile - 2>&1 | tee /tmp/build.log
	@! grep -qi "error" /tmp/build.log || (echo "Build failed" && exit 1)
	@echo "Build OK"

build-mac: ## Build macOS app for testing (close Unity first)
	@mkdir -p "$(PROJECT)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-buildTarget StandaloneOSX \
		-buildOSXUniversalPlayer "$(PROJECT)/Build/Pong.app" \
		-logFile -
	@echo "Built: $(PROJECT)/Build/Pong.app"

build-android: ## Build Android APK for Board hardware (close Unity first)
	@mkdir -p "$(PROJECT)/Build"
	$(UNITY) -batchmode -nographics -quit -projectPath "$(PROJECT)" \
		-buildTarget Android \
		-executeMethod Pong.Editor.BuildScript.Build \
		-logFile -
	@echo "Built: $(APK)"

PACKAGE := com.DefaultCompany.Myproject

deploy: $(APK) ## Install and launch on Board
	$(BDB) install $(APK)
	$(BDB) launch $(PACKAGE)

logs: ## Stream logs from Board (Ctrl+C to stop)
	$(BDB) logs $(PACKAGE)

stop: ## Stop app on Board
	$(BDB) stop $(PACKAGE)

bdb-status: ## Check Board connection
	$(BDB) status

clean: ## Remove test results and build artifacts
	rm -rf $(RESULTS_DIR) "$(PROJECT)/Build"
	rm -f /tmp/build.log
```

### Common Workflow

```bash
# 1. Run tests
make test

# 2. Build for hardware (CLOSE UNITY FIRST)
make build-android

# 3. Deploy to connected Board
make deploy

# 4. Watch logs
make logs

# 5. Stop the app
make stop
```

**CRITICAL:** Unity must be closed before running CLI builds. Unity locks project files.

---

## Complete Game Code: Pong

### Architecture Overview

```
PongGame (MonoBehaviour)
├── Manages game state (WaitingToStart, Playing, GameOver)
├── Processes Board SDK input
├── Handles scoring
├── Creates and orchestrates all game objects
│
├── PongBall (MonoBehaviour)
│   ├── Movement
│   ├── Wall bouncing
│   └── Paddle bouncing with angle calculation
│
├── PongPaddle (MonoBehaviour) x2
│   ├── Follows touch position
│   ├── Clamped to play area
│   └── Hit detection helpers
│
└── PongSettings (ScriptableObject)
    └── Tweakable parameters (speed, sizes, etc.)
```

### PongSettings.cs

ScriptableObject for tweakable game parameters:

```csharp
using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Tweakable settings for the Pong game.
    /// Create an instance via Assets > Create > Pong > Settings
    /// </summary>
    [CreateAssetMenu(fileName = "PongSettings", menuName = "Pong/Settings")]
    public class PongSettings : ScriptableObject
    {
        [Header("Ball Settings")]
        [Tooltip("Initial speed of the ball in units per second")]
        [Range(1f, 20f)]
        public float ballSpeed = 8f;

        [Tooltip("How much the ball speeds up after each hit")]
        [Range(0f, 1f)]
        public float ballSpeedIncrease = 0.1f;

        [Tooltip("Maximum ball speed")]
        [Range(5f, 30f)]
        public float maxBallSpeed = 15f;

        [Tooltip("Size of the ball")]
        [Range(0.1f, 1f)]
        public float ballSize = 0.3f;

        [Header("Paddle Settings")]
        [Tooltip("How quickly the paddle follows the finger (higher = more responsive)")]
        [Range(1f, 50f)]
        public float paddleResponsiveness = 20f;

        [Tooltip("Width of the paddle")]
        [Range(0.5f, 3f)]
        public float paddleWidth = 2f;

        [Tooltip("Height/thickness of the paddle")]
        [Range(0.1f, 0.5f)]
        public float paddleHeight = 0.3f;

        [Tooltip("How far from the edge the paddles are positioned")]
        [Range(0.5f, 2f)]
        public float paddleEdgeOffset = 1f;

        [Header("Game Settings")]
        [Tooltip("Points needed to win")]
        [Range(1, 21)]
        public int winningScore = 5;

        [Tooltip("How much the ball angle changes based on where it hits the paddle")]
        [Range(0f, 1f)]
        public float paddleAngleInfluence = 0.5f;

        [Header("Audio")]
        [Tooltip("Enable sound effects")]
        public bool enableSound = true;
    }
}
```

### PongBall.cs

Ball movement and collision response:

```csharp
using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Controls the ball movement and collision in Pong.
    /// </summary>
    public class PongBall : MonoBehaviour
    {
        private PongSettings settings;
        private Vector2 velocity;
        private float currentSpeed;
        private bool isActive;

        private SpriteRenderer spriteRenderer;

        public Vector2 Velocity => velocity;

        public void Initialize(PongSettings gameSettings)
        {
            settings = gameSettings;
            currentSpeed = settings.ballSpeed;

            // Set up visual
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a circle sprite
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = Color.white;
            transform.localScale = Vector3.one * settings.ballSize;
        }

        public void Serve(int direction)
        {
            // Reset position to center
            transform.position = Vector3.zero;

            // Reset speed
            currentSpeed = settings.ballSpeed;

            // Launch at random angle toward the specified direction
            float angle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
            velocity = new Vector2(
                Mathf.Cos(angle) * direction,
                Mathf.Sin(angle)
            ).normalized * currentSpeed;

            isActive = true;
        }

        public void Stop()
        {
            isActive = false;
            velocity = Vector2.zero;
        }

        private void Update()
        {
            if (!isActive) return;

            // Move ball
            Vector3 newPos = transform.position + (Vector3)(velocity * Time.deltaTime);
            transform.position = newPos;
        }

        public void BounceOffWall()
        {
            velocity.y = -velocity.y;
        }

        public void BounceOffSideWall()
        {
            velocity.x = -velocity.x;
        }

        public void BounceOffPaddle(float hitPosition, int direction)
        {
            // hitPosition is -1 to 1 (where on the paddle it hit)
            // direction is 1 for right, -1 for left

            // Increase speed
            currentSpeed = Mathf.Min(currentSpeed + settings.ballSpeedIncrease, settings.maxBallSpeed);

            // Curved paddle physics:
            // - Center hit (0) = straight back
            // - Edge hit (+/-1) = max angle (75 degrees)
            float maxAngle = 75f * settings.paddleAngleInfluence;
            float angle = hitPosition * maxAngle * Mathf.Deg2Rad;

            // Calculate new velocity
            float vx = Mathf.Cos(angle) * direction;
            float vy = Mathf.Sin(angle);

            // Enforce minimum horizontal speed (prevent vertical bouncing)
            float minHorizontal = 0.4f;
            if (Mathf.Abs(vx) < minHorizontal)
            {
                vx = minHorizontal * direction;
                vy = Mathf.Sqrt(1f - vx * vx) * Mathf.Sign(vy);
            }

            velocity = new Vector2(vx, vy).normalized * currentSpeed;
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, dist < radius - 1 ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
```

### PongPaddle.cs

Paddle that follows touch input:

```csharp
using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Controls a paddle that follows touch input.
    /// </summary>
    public class PongPaddle : MonoBehaviour
    {
        private PongSettings settings;
        private float targetY;
        private float minY;
        private float maxY;
        private bool isControlled;

        private SpriteRenderer spriteRenderer;

        public int PlayerIndex { get; private set; }
        public float XPosition => transform.position.x;

        public void Initialize(PongSettings gameSettings, int playerIndex, float xPosition, float playAreaHeight)
        {
            settings = gameSettings;
            PlayerIndex = playerIndex;

            // Set up bounds
            float halfPaddleWidth = settings.paddleWidth / 2f;
            float halfArea = playAreaHeight / 2f;
            minY = -halfArea + halfPaddleWidth;
            maxY = halfArea - halfPaddleWidth;

            // Position the paddle
            transform.position = new Vector3(xPosition, 0, 0);
            targetY = 0;

            // Set up visual
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = CreateRectSprite();
            spriteRenderer.color = playerIndex == 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.5f, 0.3f);
            transform.localScale = new Vector3(settings.paddleHeight, settings.paddleWidth, 1);
        }

        public void SetTargetPosition(float worldY)
        {
            targetY = Mathf.Clamp(worldY, minY, maxY);
            isControlled = true;
        }

        public void ReleaseControl()
        {
            isControlled = false;
        }

        public bool IsControlled => isControlled;

        private void Update()
        {
            // Smoothly move toward target position
            Vector3 pos = transform.position;
            float newY = Mathf.Lerp(pos.y, targetY, settings.paddleResponsiveness * Time.deltaTime);
            transform.position = new Vector3(pos.x, newY, pos.z);
        }

        public float GetHitPosition(float ballY)
        {
            // Returns -1 to 1 based on where the ball hit the paddle
            float paddleY = transform.position.y;
            float halfWidth = settings.paddleWidth / 2f;
            return Mathf.Clamp((ballY - paddleY) / halfWidth, -1f, 1f);
        }

        public bool IsWithinReach(float ballY)
        {
            float paddleY = transform.position.y;
            float halfWidth = settings.paddleWidth / 2f;
            return ballY >= paddleY - halfWidth && ballY <= paddleY + halfWidth;
        }

        private Sprite CreateRectSprite()
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
```

### PongGame.cs

Main game controller:

```csharp
using UnityEngine;
using UnityEngine.UI;
using Board.Input;
using System.Collections.Generic;

namespace Pong
{
    /// <summary>
    /// Main game controller for Pong.
    /// Handles Board SDK touch input, game state, and scoring.
    /// </summary>
    public class PongGame : MonoBehaviour
    {
        [SerializeField] private PongSettings settings;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text messageText;

        private PongBall ball;
        private PongPaddle leftPaddle;
        private PongPaddle rightPaddle;

        private int[] scores = new int[2];
        private float playAreaWidth;
        private float playAreaHeight;

        private Camera mainCamera;
        private Dictionary<int, int> contactToPaddle = new Dictionary<int, int>();

        private enum GameState { WaitingToStart, Playing, GameOver }
        private GameState state = GameState.WaitingToStart;

        // Audio
        private AudioSource audioSource;
        private AudioClip hitSound;
        private AudioClip scoreSound;
        private AudioClip winSound;

        private void Awake()
        {
            mainCamera = Camera.main;

            // Calculate play area from camera
            playAreaHeight = mainCamera.orthographicSize * 2f;
            playAreaWidth = playAreaHeight * mainCamera.aspect;

            // Create game objects
            CreateGameObjects();

            // Initialize audio
            CreateAudio();

            // Initialize UI
            UpdateScoreDisplay();
            ShowMessage("Touch to Start");
        }

        private void CreateAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            hitSound = CreateBeep(440f, 0.05f);   // A4, short
            scoreSound = CreateBeep(220f, 0.15f); // A3, longer
            winSound = CreateBeep(880f, 0.3f);    // A5, longest
        }

        private AudioClip CreateBeep(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount; // fade out
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
            }

            AudioClip clip = AudioClip.Create("beep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySound(AudioClip clip)
        {
            if (settings.enableSound && audioSource != null)
                audioSource.PlayOneShot(clip);
        }

        private void CreateGameObjects()
        {
            // Create ball
            GameObject ballObj = new GameObject("Ball");
            ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            // Create paddles
            float paddleX = (playAreaWidth / 2f) - settings.paddleEdgeOffset;

            GameObject leftPaddleObj = new GameObject("LeftPaddle");
            leftPaddle = leftPaddleObj.AddComponent<PongPaddle>();
            leftPaddle.Initialize(settings, 0, -paddleX, playAreaHeight);

            GameObject rightPaddleObj = new GameObject("RightPaddle");
            rightPaddle = rightPaddleObj.AddComponent<PongPaddle>();
            rightPaddle.Initialize(settings, 1, paddleX, playAreaHeight);

            // Create walls (visual only - we handle collision in code)
            CreateWalls();
        }

        private void CreateWalls()
        {
            // Top and bottom walls
            float wallThickness = 0.2f;
            float halfHeight = playAreaHeight / 2f;
            float halfWidth = playAreaWidth / 2f;

            CreateWall("TopWall", new Vector3(0, halfHeight + wallThickness / 2f, 0),
                new Vector3(playAreaWidth, wallThickness, 1));
            CreateWall("BottomWall", new Vector3(0, -halfHeight - wallThickness / 2f, 0),
                new Vector3(playAreaWidth, wallThickness, 1));

            // Goal zones (behind paddles)
            float goalWidth = 0.3f;
            Color blueGoal = new Color(0.2f, 0.4f, 0.6f, 0.5f);
            Color orangeGoal = new Color(0.6f, 0.35f, 0.2f, 0.5f);

            CreateGoalZone("LeftGoal", new Vector3(-halfWidth + goalWidth / 2f, 0, 1),
                new Vector3(goalWidth, playAreaHeight, 1), blueGoal);
            CreateGoalZone("RightGoal", new Vector3(halfWidth - goalWidth / 2f, 0, 1),
                new Vector3(goalWidth, playAreaHeight, 1), orangeGoal);
        }

        private void CreateGoalZone(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject zone = new GameObject(name);
            zone.transform.position = position;
            zone.transform.localScale = scale;

            SpriteRenderer sr = zone.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            sr.color = color;
            sr.sortingOrder = -1; // behind everything else
        }

        private void CreateWall(string name, Vector3 position, Vector3 scale)
        {
            GameObject wall = new GameObject(name);
            wall.transform.position = position;
            wall.transform.localScale = scale;

            SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            sr.color = new Color(0.5f, 0.5f, 0.5f);
        }

        private void Update()
        {
            // Handle Board SDK touch input
            ProcessTouchInput();

            // Update game state
            switch (state)
            {
                case GameState.WaitingToStart:
                    if (leftPaddle.IsControlled || rightPaddle.IsControlled)
                    {
                        StartGame();
                    }
                    break;

                case GameState.Playing:
                    UpdateBallCollisions();
                    CheckForScore();
                    break;

                case GameState.GameOver:
                    // Wait for touch to restart
                    if (leftPaddle.IsControlled || rightPaddle.IsControlled)
                    {
                        ResetGame();
                    }
                    break;
            }
        }

        private void ProcessTouchInput()
        {
            // Get all active finger contacts from Board SDK
            BoardContact[] contacts = BoardInput.GetActiveContacts(BoardContactType.Finger);

            // Track which paddles are being controlled this frame
            HashSet<int> activePaddles = new HashSet<int>();

            foreach (var contact in contacts)
            {
                // Convert screen position to world position
                Vector3 worldPos = mainCamera.ScreenToWorldPoint(
                    new Vector3(contact.screenPosition.x, contact.screenPosition.y, 10));

                // Determine which side of the screen the touch is on
                int paddleIndex = worldPos.x < 0 ? 0 : 1;
                PongPaddle paddle = paddleIndex == 0 ? leftPaddle : rightPaddle;

                switch (contact.phase)
                {
                    case BoardContactPhase.Began:
                        // Assign this contact to a paddle
                        contactToPaddle[contact.contactId] = paddleIndex;
                        paddle.SetTargetPosition(worldPos.y);
                        activePaddles.Add(paddleIndex);
                        break;

                    case BoardContactPhase.Moved:
                    case BoardContactPhase.Stationary:
                        // Update paddle position if this contact is controlling it
                        if (contactToPaddle.TryGetValue(contact.contactId, out int assignedPaddle))
                        {
                            PongPaddle assignedPaddleObj = assignedPaddle == 0 ? leftPaddle : rightPaddle;
                            assignedPaddleObj.SetTargetPosition(worldPos.y);
                            activePaddles.Add(assignedPaddle);
                        }
                        break;

                    case BoardContactPhase.Ended:
                    case BoardContactPhase.Canceled:
                        // Release the paddle
                        if (contactToPaddle.TryGetValue(contact.contactId, out int releasedPaddle))
                        {
                            contactToPaddle.Remove(contact.contactId);
                        }
                        break;
                }
            }

            // Release control for paddles not being touched
            if (!activePaddles.Contains(0)) leftPaddle.ReleaseControl();
            if (!activePaddles.Contains(1)) rightPaddle.ReleaseControl();
        }

        private void UpdateBallCollisions()
        {
            Vector3 ballPos = ball.transform.position;
            float halfBallSize = settings.ballSize / 2f;
            float halfHeight = playAreaHeight / 2f;

            // Wall collisions (top/bottom)
            if (ballPos.y + halfBallSize >= halfHeight || ballPos.y - halfBallSize <= -halfHeight)
            {
                ball.BounceOffWall();
                // Clamp position
                float clampedY = Mathf.Clamp(ballPos.y, -halfHeight + halfBallSize, halfHeight - halfBallSize);
                ball.transform.position = new Vector3(ballPos.x, clampedY, ballPos.z);
            }

            // Paddle collisions
            CheckPaddleCollision(leftPaddle, ballPos, halfBallSize, 1);   // ball goes right
            CheckPaddleCollision(rightPaddle, ballPos, halfBallSize, -1); // ball goes left
        }

        private void CheckPaddleCollision(PongPaddle paddle, Vector3 ballPos, float halfBallSize, int bounceDirection)
        {
            float paddleX = paddle.XPosition;
            float halfPaddleThickness = settings.paddleHeight / 2f;

            bool isLeftPaddle = paddleX < 0;

            // Check if ball overlaps paddle's X range
            bool atPaddleX = isLeftPaddle
                ? ballPos.x - halfBallSize <= paddleX + halfPaddleThickness
                : ballPos.x + halfBallSize >= paddleX - halfPaddleThickness;

            if (!atPaddleX) return;

            // Check if ball is moving toward this paddle
            bool movingToward = isLeftPaddle
                ? ball.Velocity.x < 0
                : ball.Velocity.x > 0;

            if (!movingToward) return;

            // Check if paddle can reach the ball
            if (paddle.IsWithinReach(ballPos.y))
            {
                float hitPosition = paddle.GetHitPosition(ballPos.y);
                ball.BounceOffPaddle(hitPosition, bounceDirection);
                PlaySound(hitSound);

                // Push ball away from paddle to prevent multiple hits
                float newX = isLeftPaddle
                    ? paddleX + halfPaddleThickness + halfBallSize + 0.05f
                    : paddleX - halfPaddleThickness - halfBallSize - 0.05f;
                ball.transform.position = new Vector3(newX, ballPos.y, ballPos.z);
            }
        }

        private void CheckForScore()
        {
            Vector3 ballPos = ball.transform.position;
            float halfWidth = playAreaWidth / 2f;
            float halfBallSize = settings.ballSize / 2f;

            if (ballPos.x - halfBallSize <= -halfWidth)
            {
                // Ball hit left wall - right player scores
                ScorePoint(1);
                ball.BounceOffSideWall();
                ball.transform.position = new Vector3(-halfWidth + halfBallSize + 0.05f, ballPos.y, ballPos.z);
            }
            else if (ballPos.x + halfBallSize >= halfWidth)
            {
                // Ball hit right wall - left player scores
                ScorePoint(0);
                ball.BounceOffSideWall();
                ball.transform.position = new Vector3(halfWidth - halfBallSize - 0.05f, ballPos.y, ballPos.z);
            }
        }

        private void ScorePoint(int playerIndex)
        {
            scores[playerIndex]++;
            UpdateScoreDisplay();

            if (scores[playerIndex] >= settings.winningScore)
            {
                ball.Stop();
                state = GameState.GameOver;
                string winner = playerIndex == 0 ? "Blue" : "Orange";
                ShowMessage($"{winner} Wins!\nTouch to Play Again");
                PlaySound(winSound);
            }
            else
            {
                PlaySound(scoreSound);
            }
        }

        private void StartGame()
        {
            state = GameState.Playing;
            ShowMessage("");
            int direction = Random.value > 0.5f ? 1 : -1;
            ball.Serve(direction);
        }

        private void ResetGame()
        {
            scores[0] = 0;
            scores[1] = 0;
            UpdateScoreDisplay();
            StartGame();
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText != null)
            {
                scoreText.text = $"{scores[0]}  -  {scores[1]}";
            }
        }

        private void ShowMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        // For editor testing without Board SDK
#if UNITY_EDITOR
        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            if (scoreText == null)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontSize = 48;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.white;

                GUI.Label(new Rect(Screen.width / 2 - 100, 20, 200, 60),
                    $"{scores[0]}  -  {scores[1]}", style);
            }

            if (messageText == null && state != GameState.Playing)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.fontSize = 32;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.white;

                string msg = state == GameState.WaitingToStart ? "Touch to Start" :
                             state == GameState.GameOver ? $"{(scores[0] > scores[1] ? "Blue" : "Orange")} Wins!\nTouch to Play Again" : "";

                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height / 2 - 40, 300, 80), msg, style);
            }
        }
#endif
    }
}
```

---

## Testing Strategy

### Two Types of Tests

1. **Edit Mode Tests** - Fast, pure logic tests, no Unity runtime
2. **Play Mode Tests** - Slower, test actual GameObjects and behavior

### PongTests.cs (Edit Mode)

```csharp
using NUnit.Framework;
using UnityEngine;

namespace Pong.Tests
{
    [TestFixture]
    public class PongTests
    {
        private PongSettings CreateTestSettings()
        {
            var settings = ScriptableObject.CreateInstance<PongSettings>();
            settings.ballSpeed = 10f;
            settings.paddleWidth = 2f;
            settings.paddleHeight = 0.3f;
            settings.paddleResponsiveness = 20f;
            settings.winningScore = 5;
            return settings;
        }

        [Test]
        public void Settings_DefaultValues_AreReasonable()
        {
            var settings = CreateTestSettings();

            Assert.Greater(settings.ballSpeed, 0, "Ball speed should be positive");
            Assert.Greater(settings.paddleWidth, 0, "Paddle width should be positive");
            Assert.Greater(settings.winningScore, 0, "Winning score should be positive");
            Assert.LessOrEqual(settings.ballSpeed, settings.maxBallSpeed, "Initial speed should not exceed max");
        }

        [Test]
        public void Settings_SpeedIncrease_StaysWithinMax()
        {
            var settings = CreateTestSettings();
            float currentSpeed = settings.ballSpeed;

            for (int i = 0; i < 100; i++)
            {
                currentSpeed = Mathf.Min(currentSpeed + settings.ballSpeedIncrease, settings.maxBallSpeed);
            }

            Assert.LessOrEqual(currentSpeed, settings.maxBallSpeed, "Speed should never exceed max");
        }

        [Test]
        public void Paddle_HitPosition_ReturnsValidRange()
        {
            float paddleY = 0;
            float paddleWidth = 2f;
            float halfWidth = paddleWidth / 2f;

            // Ball at center of paddle
            float ballY = 0;
            float hitPos = Mathf.Clamp((ballY - paddleY) / halfWidth, -1f, 1f);
            Assert.AreEqual(0, hitPos, 0.001f, "Center hit should return 0");

            // Ball at top edge
            ballY = halfWidth;
            hitPos = Mathf.Clamp((ballY - paddleY) / halfWidth, -1f, 1f);
            Assert.AreEqual(1, hitPos, 0.001f, "Top edge hit should return 1");

            // Ball at bottom edge
            ballY = -halfWidth;
            hitPos = Mathf.Clamp((ballY - paddleY) / halfWidth, -1f, 1f);
            Assert.AreEqual(-1, hitPos, 0.001f, "Bottom edge hit should return -1");
        }
    }
}
```

### PongPlayModeTests.cs (Play Mode)

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pong.Tests
{
    [TestFixture]
    public class PongPlayModeTests
    {
        private PongSettings settings;
        private Camera testCamera;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<PongSettings>();
            settings.ballSpeed = 10f;
            settings.paddleWidth = 2f;
            settings.paddleHeight = 0.3f;
            settings.paddleResponsiveness = 20f;
            settings.winningScore = 3;
            settings.ballSize = 0.3f;
            settings.paddleEdgeOffset = 1f;

            var camObj = new GameObject("TestCamera");
            testCamera = camObj.AddComponent<Camera>();
            testCamera.orthographic = true;
            testCamera.orthographicSize = 5f;
            testCamera.tag = "MainCamera";
        }

        [TearDown]
        public void TearDown()
        {
            if (testCamera != null)
                Object.Destroy(testCamera.gameObject);
            if (settings != null)
                Object.Destroy(settings);

            foreach (var obj in Object.FindObjectsOfType<PongBall>())
                Object.Destroy(obj.gameObject);
            foreach (var obj in Object.FindObjectsOfType<PongPaddle>())
                Object.Destroy(obj.gameObject);
        }

        [UnityTest]
        public IEnumerator Ball_Moves_WhenServed()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            Vector3 startPos = ballObj.transform.position;
            ball.Serve(1);

            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Vector3 endPos = ballObj.transform.position;
            Assert.AreNotEqual(startPos, endPos, "Ball should have moved after serving");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Paddle_FollowsTargetPosition_Smoothly()
        {
            var paddleObj = new GameObject("TestPaddle");
            var paddle = paddleObj.AddComponent<PongPaddle>();
            paddle.Initialize(settings, 0, 0f, 10f);

            paddle.SetTargetPosition(2f);

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            float paddleY = paddleObj.transform.position.y;
            Assert.Greater(paddleY, 0.5f, "Paddle should have moved toward target");

            Object.Destroy(paddleObj);
        }
    }
}
```

### Running Tests

```bash
# Edit mode tests (fast)
make test-edit

# Play mode tests (slower, more comprehensive)
make test-play

# From Unity GUI
# Window > General > Test Runner
```

---

## Lessons Learned

### What Worked Well

1. **ScriptableObjects for Settings**
   - Hot-reloadable during play mode
   - Easy to tweak without recompiling
   - Can create multiple presets (easy mode, hard mode, etc.)

2. **Programmatic Sprite Creation**
   - No external asset dependencies
   - All game visuals created in code
   - Fast iteration, easy to modify

3. **Dictionary-based Contact Tracking**
   - Clean mapping from contact IDs to game elements
   - Handles multiple simultaneous touches correctly
   - Easy to extend for more complex games

4. **Procedural Audio**
   - Simple beeps generated mathematically
   - No audio files needed
   - Easily tweakable frequency and duration

5. **Editor Menu Integration**
   - `Board > Pong > Setup Scene` for one-click setup
   - `Board > Pong > Open Settings` for quick access
   - `Board > Pong > Quick Start Guide` for help

6. **Makefile Automation**
   - Consistent, repeatable builds
   - Self-documenting with `make help`
   - Works from terminal without Unity GUI

### What Could Be Better

1. **Unity CLI Build Issues**
   - Must close Unity GUI before running CLI builds
   - Error messages often cryptic
   - Build times still slow even in batchmode

2. **macOS Gatekeeper Friction**
   - Downloaded bdb binary gets quarantined
   - Requires manual codesigning workaround
   - Could be pre-signed by vendor

3. **Board Simulator Learning Curve**
   - Not intuitive that you need to DRAG placed contacts
   - Easy to forget to "Enable Simulation"
   - Would benefit from better onboarding

4. **Package Naming**
   - Default package name `com.DefaultCompany.Myproject` is confusing
   - Should set proper package name early
   - Affects bdb commands

### Key Insights

1. **Board-Specific Design Considerations**
   - Large screen = big touch targets needed
   - Multiple simultaneous touches are the norm
   - Consider "opposite sides of table" ergonomics
   - Glyphs open up physical+digital hybrid games

2. **Testing on Simulator vs Hardware**
   - Simulator is good for basic logic
   - Real hardware has different touch characteristics
   - Always test on actual Board before shipping

3. **Collision Without Physics Engine**
   - Simple AABB collision works fine for Pong
   - Manual collision gives more control
   - Avoids Unity physics complexity

---

## Ideas for Future Games

### Games That Would Work Well on Board

1. **Air Hockey**
   - Two players, opposite ends of table
   - Pucks, mallets, goals
   - Add power-ups with glyphs

2. **Chess/Checkers with Physical Pieces**
   - Use glyphs as game pieces
   - Board validates legal moves
   - Tracks captures automatically

3. **Cooperative Puzzle Games**
   - Players work together to solve
   - Each controls different elements
   - Communication required

4. **Drawing/Art Games**
   - Collaborative canvas
   - Different tools (fingers = colors)
   - Export finished artwork

5. **Card Games with Virtual Deck**
   - Physical table, digital cards
   - Each player's "hand" on their side
   - Automatic shuffling and dealing

6. **Tower Defense**
   - Place towers by touching
   - Use glyphs for special abilities
   - Cooperative or competitive

### Technical Ideas to Explore

1. **Glyph-Based Power-Ups**
   - Place a physical piece to activate ability
   - Different glyphs = different powers
   - Rotation affects power

2. **Persistent Game State**
   - Save/load games
   - Cross-session progression
   - Leaderboards

3. **Network Multiplayer**
   - Multiple Boards connected
   - Shared game world
   - Async turn-based

4. **Audio Spatial**
   - Sound follows action
   - Directional audio based on touch location
   - Immersive feedback

---

## Resources & Links

### Official Documentation

- **Board Developer Portal**: [dev.board.fun](https://dev.board.fun)
- **bdb Download**: [dev.board.fun/downloads/bdb/macos-universal/bdb](https://dev.board.fun/downloads/bdb/macos-universal/bdb)
- **Unity Documentation**: [docs.unity3d.com](https://docs.unity3d.com)

### Unity Specific

- **Unity Download**: [unity.com/download](https://unity.com/download)
- **Package Manager Docs**: [docs.unity3d.com/Manual/upm-ui.html](https://docs.unity3d.com/Manual/upm-ui.html)
- **Test Framework**: [docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html](https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/manual/index.html)

### Build & Deploy

- **Unity CLI Reference**: [docs.unity3d.com/Manual/CommandLineArguments.html](https://docs.unity3d.com/Manual/CommandLineArguments.html)
- **Android Build Setup**: [docs.unity3d.com/Manual/android-sdksetup.html](https://docs.unity3d.com/Manual/android-sdksetup.html)

### Code References

- **Board.Input namespace**: Core input handling
- **BoardContact struct**: Individual touch/glyph data
- **BoardContactType enum**: Finger vs Glyph
- **BoardContactPhase enum**: Began, Moved, Stationary, Ended, Canceled

---

## Quick Reference Card

```bash
# Setup
unity-hub                            # Install Unity with Android support
# Import Board SDK via Package Manager (add tarball)

# Development
make test                            # Run tests
make build                           # Verify compilation

# Build (CLOSE UNITY FIRST)
make build-android                   # Build APK

# Deploy
make bdb-status                      # Check Board connection
make deploy                          # Install + launch
make logs                            # Stream logs (Ctrl+C to stop)
make stop                            # Stop app

# Unity Editor
Board > Pong > Setup Scene           # Initialize scene
Board > Pong > Open Settings         # Edit game parameters
Board > Input > Simulator            # Test without hardware

# bdb commands
bdb help                             # Show all commands
bdb install <apk>                    # Install app
bdb launch <package>                 # Start app
bdb logs <package>                   # Stream logs
bdb list                             # Show installed apps
```

---

*Document generated: February 11, 2026*
