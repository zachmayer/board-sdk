using UnityEngine;
using UnityEngine.UI;
using Board.Input;
using Board.Core;

namespace GolfWall
{
    public class GolfWallGame : MonoBehaviour
    {
        [SerializeField] private GolfWallSettings settings;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text messageText;

        private GolfBall ball;
        private Wall wall;

        private int score;
        private float playAreaWidth;
        private float playAreaHeight;
        private Camera mainCamera;

        private enum GameState { WaitingToStart, ReadyToSwing, BallInFlight }
        private GameState state = GameState.WaitingToStart;

        // Piece tracking
        private GameObject pieceIndicator;
        private SpriteRenderer pieceIndicatorRenderer;
        private Vector3 pieceWorldPos;
        private bool pieceTracked;

        // Angular velocity tracking
        private float lastOrientation;
        private double lastTimestamp;
        private bool hasLastOrientation;
        private float peakAngularVelocity;
        private float peakDecayTimer;

        // Landing zone visual
        private GameObject landingZone;

        // Audio
        private AudioSource audioSource;
        private AudioClip swingSound;
        private AudioClip thudSound;
        private AudioClip scoreSound;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 60f;

            mainCamera = Camera.main;

            playAreaHeight = mainCamera.orthographicSize * 2f;
            playAreaWidth = playAreaHeight * mainCamera.aspect;

            CreateGameObjects();

            if (scoreText == null || messageText == null)
                CreateUI();

            CreateAudio();

            Debug.Log($"[GolfWall] Camera ortho={mainCamera.orthographicSize} aspect={mainCamera.aspect:F2} " +
                $"playArea={playAreaWidth:F1}x{playAreaHeight:F1}");
            Debug.Log($"[GolfWall] Wall: leftX={wall.WallLeftX:F2} rightX={wall.WallRightX:F2} " +
                $"topY={wall.WallTopY:F2} scale={wall.transform.lossyScale}");

            // Configure Board pause screen
            BoardApplication.SetPauseScreenContext(applicationName: "Golf Wall");
            BoardApplication.pauseScreenActionReceived += OnPauseAction;

            UpdateScoreDisplay();
            ShowMessage("Place robot piece\non the left side");
        }

        private void OnPauseAction(BoardPauseAction action, BoardPauseAudioTrack[] audioTracks)
        {
            switch (action)
            {
                case BoardPauseAction.Resume:
                    Time.timeScale = 1f;
                    break;
                case BoardPauseAction.ExitGameUnsaved:
                case BoardPauseAction.ExitGameSaved:
                    BoardApplication.Exit();
                    break;
            }
        }

        private void OnDestroy()
        {
            BoardApplication.pauseScreenActionReceived -= OnPauseAction;
        }

        private void CreateGameObjects()
        {
            // Create ball
            GameObject ballObj = new GameObject("Ball");
            ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            // Create vertical wall at x=0
            GameObject wallObj = new GameObject("Wall");
            wall = wallObj.AddComponent<Wall>();
            wall.Initialize(settings, playAreaWidth, playAreaHeight);

            // Create piece indicator (translucent ring)
            pieceIndicator = new GameObject("PieceIndicator");
            pieceIndicatorRenderer = pieceIndicator.AddComponent<SpriteRenderer>();
            pieceIndicatorRenderer.sprite = CreateRingSprite();
            pieceIndicatorRenderer.color = settings.pieceIndicatorColor;
            pieceIndicator.transform.localScale = Vector3.one * settings.hitDetectionRadius * 2f;
            pieceIndicator.SetActive(false);

            // Create landing zone visual on right side
            CreateLandingZone();

            // Create background
            CreateBackground();
        }

        private void CreateLandingZone()
        {
            landingZone = new GameObject("LandingZone");
            var renderer = landingZone.AddComponent<SpriteRenderer>();
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            renderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            renderer.color = settings.landingZoneColor;
            renderer.sortingOrder = -1;

            // Right half of screen (past the wall)
            float rightWidth = playAreaWidth / 2f - settings.wallThickness / 2f;
            float centerX = settings.wallThickness / 2f + rightWidth / 2f;
            landingZone.transform.localScale = new Vector3(rightWidth, playAreaHeight, 1);
            landingZone.transform.position = new Vector3(centerX, 0, 0);
        }

        private void CreateBackground()
        {
            mainCamera.backgroundColor = settings.backgroundColor;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void Update()
        {
            ProcessGlyphInput();

            switch (state)
            {
                case GameState.WaitingToStart:
                    if (pieceTracked)
                    {
                        state = GameState.ReadyToSwing;
                        ShowMessage("Spin the piece\nto swing!");
                    }
                    break;

                case GameState.ReadyToSwing:
                    if (!pieceTracked)
                    {
                        state = GameState.WaitingToStart;
                        ShowMessage("Place robot piece\non the left side");
                        break;
                    }
                    CheckForSwing();
                    break;

                case GameState.BallInFlight:
                    // Physics runs in FixedUpdate
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (state != GameState.BallInFlight) return;

            ball.PhysicsStep();
            CheckCollisions();
        }

        private void ProcessGlyphInput()
        {
            BoardContact[] contacts = BoardInput.GetActiveContacts(BoardContactType.Glyph);

            if (contacts.Length == 0)
            {
                pieceTracked = false;
                pieceIndicator.SetActive(false);
                ResetAngularTracking();
                return;
            }

            // Track first glyph
            BoardContact contact = contacts[0];
            pieceWorldPos = mainCamera.ScreenToWorldPoint(
                new Vector3(contact.screenPosition.x, contact.screenPosition.y, 10));
            pieceWorldPos.z = 0;

            pieceTracked = true;
            pieceIndicator.SetActive(true);
            pieceIndicator.transform.position = pieceWorldPos;

            // Compute instantaneous angular velocity from SDK orientation (already in radians)
            float orientation = contact.orientation;
            double timestamp = contact.timestamp;

            if (hasLastOrientation && timestamp > lastTimestamp)
            {
                float dt = (float)(timestamp - lastTimestamp);
                float dAngle = AngleWrapDelta(orientation - lastOrientation);
                float angVel = dAngle / dt;

                // Track peak angular velocity (decays over time)
                if (Mathf.Abs(angVel) > Mathf.Abs(peakAngularVelocity))
                    peakAngularVelocity = angVel;
            }

            lastOrientation = orientation;
            lastTimestamp = timestamp;
            hasLastOrientation = true;

            // Decay peak over time so stale spins don't trigger
            peakDecayTimer += Time.deltaTime;
            if (peakDecayTimer > 0.15f)
            {
                peakAngularVelocity *= 0.5f;
                peakDecayTimer = 0f;
            }
        }

        /// <summary>
        /// Wraps an angle delta to [-PI, PI] using atan2 for robustness.
        /// </summary>
        public static float AngleWrapDelta(float delta)
        {
            return Mathf.Atan2(Mathf.Sin(delta), Mathf.Cos(delta));
        }

        private void ResetAngularTracking()
        {
            hasLastOrientation = false;
            peakAngularVelocity = 0f;
            peakDecayTimer = 0f;
        }

        private void CheckForSwing()
        {
            float absAngVel = Mathf.Abs(peakAngularVelocity);
            if (absAngVel >= settings.angularVelocityThreshold)
            {
                Debug.Log($"[GolfWall] Swing detected! angVel={peakAngularVelocity:F2} rad/s " +
                    $"(threshold={settings.angularVelocityThreshold})");
                LaunchBall(absAngVel);
            }
        }

        private void LaunchBall(float angularSpeed)
        {
            float speed = Mathf.Clamp(angularSpeed * settings.powerMultiplier,
                settings.minLaunchSpeed, settings.maxLaunchSpeed);

            float halfWidth = playAreaWidth / 2f;

            // Map piece X position to launch angle:
            // Closer to wall (x near 0) → steeper angle (70°) to pop over
            // Far from wall (x near -halfWidth) → shallower angle (40°) for longer arc
            float t = Mathf.InverseLerp(-halfWidth, wall.WallLeftX, pieceWorldPos.x);
            t = Mathf.Clamp01(t);
            float angleDeg = Mathf.Lerp(40f, 70f, t);
            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Velocity: rightward (+X) and upward (+Y) for parabolic arc
            Vector2 launchVelocity = new Vector2(
                speed * Mathf.Cos(angleRad),
                speed * Mathf.Sin(angleRad));

            // Launch from just above the piece position
            Vector3 launchPos = pieceWorldPos + Vector3.up * (settings.ballSize * 0.5f);

            Debug.Log($"[GolfWall] Launch: speed={speed:F1} angle={angleDeg:F0}° " +
                $"vel=({launchVelocity.x:F1},{launchVelocity.y:F1}) from={launchPos} " +
                $"wallTopY={wall.WallTopY:F2}");

            ball.Launch(launchPos, launchVelocity);
            state = GameState.BallInFlight;
            ShowMessage("");

            PlaySound(swingSound);
            ResetAngularTracking();
        }

        private void CheckCollisions()
        {
            Vector3 ballPos = ball.CurrentPosition;
            float halfBall = settings.ballSize / 2f;
            float halfWidth = playAreaWidth / 2f;
            float halfHeight = playAreaHeight / 2f;

            // --- Vertical wall collision (check first) ---
            // Wall spans from screen bottom to WallTopY at x=0
            // Ball hits wall face if moving rightward into the wall while below wall top
            if (ball.Velocity.x > 0 &&
                ballPos.x + halfBall >= wall.WallLeftX &&
                ballPos.x - halfBall < wall.WallRightX &&
                ballPos.y - halfBall < wall.WallTopY)
            {
                ball.BounceOffWall();
                ball.SnapPosition(new Vector3(wall.WallLeftX - halfBall - 0.01f, ballPos.y, 0));
                PlaySound(thudSound);
                Debug.Log($"[GolfWall] Ball hit wall face at y={ballPos.y:F2} (wallTop={wall.WallTopY:F2})");
                return;
            }

            // Ball coming back from right side hits wall
            if (ball.Velocity.x < 0 &&
                ballPos.x - halfBall <= wall.WallRightX &&
                ballPos.x + halfBall > wall.WallLeftX &&
                ballPos.y - halfBall < wall.WallTopY)
            {
                ball.BounceOffWall();
                ball.SnapPosition(new Vector3(wall.WallRightX + halfBall + 0.01f, ballPos.y, 0));
                PlaySound(thudSound);
                return;
            }

            // --- Scoring: ball cleared the wall ---
            // Ball's left edge is past wall's right edge = it went over
            if (ballPos.x - halfBall > wall.WallRightX)
            {
                Score();
                return;
            }

            // --- Screen bounds ---
            // Left edge bounce
            if (ballPos.x - halfBall <= -halfWidth)
            {
                ball.BounceOffWall();
                ball.SnapPosition(new Vector3(-halfWidth + halfBall + 0.01f, ballPos.y, 0));
            }

            // Right edge (ball went far right — already scored above, but safety)
            if (ballPos.x + halfBall >= halfWidth)
            {
                Score();
                return;
            }

            // Top edge bounce
            if (ballPos.y + halfBall >= halfHeight)
            {
                ball.BounceOffTopBottom();
                ball.SnapPosition(new Vector3(ballPos.x, halfHeight - halfBall - 0.01f, 0));
            }

            // Bottom edge — ball fell off screen
            if (ballPos.y - halfBall <= -halfHeight)
            {
                BallMissed();
                return;
            }
        }

        private void Score()
        {
            score++;
            UpdateScoreDisplay();
            wall.SetWallForScore(score);
            ball.Stop();
            state = GameState.ReadyToSwing;
            ShowMessage($"Nice shot!\nScore: {score}");
            PlaySound(scoreSound);

            Debug.Log($"[GolfWall] Score! Now {score}. Wall height growing.");
        }

        private void BallMissed()
        {
            ball.Stop();
            state = GameState.ReadyToSwing;
            ShowMessage("Missed! Try again!");

            Debug.Log("[GolfWall] Ball missed (fell off screen).");
        }

        private void UpdateScoreDisplay()
        {
            if (scoreText != null)
                scoreText.text = $"Score: {score}";
        }

        private void ShowMessage(string message)
        {
            if (messageText != null)
                messageText.text = message;
        }

        private void CreateUI()
        {
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();

            // Score text at top center
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(canvasObj.transform, false);
            scoreText = scoreObj.AddComponent<Text>();
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreText.fontSize = 48;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = Color.white;
            RectTransform scoreRect = scoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(0.3f, 0.9f);
            scoreRect.anchorMax = new Vector2(0.7f, 1f);
            scoreRect.offsetMin = Vector2.zero;
            scoreRect.offsetMax = Vector2.zero;

            // Message text at center
            GameObject msgObj = new GameObject("MessageText");
            msgObj.transform.SetParent(canvasObj.transform, false);
            messageText = msgObj.AddComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 32;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            RectTransform msgRect = messageText.rectTransform;
            msgRect.anchorMin = new Vector2(0.2f, 0.4f);
            msgRect.anchorMax = new Vector2(0.8f, 0.6f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;
        }

        private void CreateAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();

            // Swing whoosh: frequency sweep 200→800Hz over 0.15s
            swingSound = CreateFrequencySweep(200f, 800f, 0.15f);

            // Wall thud: 110Hz, short
            thudSound = CreateBeep(110f, 0.1f);

            // Score ding: 880Hz
            scoreSound = CreateBeep(880f, 0.2f);
        }

        private AudioClip CreateBeep(float frequency, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = 1f - (float)i / sampleCount;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.3f;
            }

            AudioClip clip = AudioClip.Create("beep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateFrequencySweep(float startFreq, float endFreq, float duration)
        {
            int sampleRate = 44100;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                float envelope = 1f - t;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                phase += 2f * Mathf.PI * freq / sampleRate;
                samples[i] = Mathf.Sin(phase) * envelope * 0.3f;
            }

            AudioClip clip = AudioClip.Create("sweep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void PlaySound(AudioClip clip)
        {
            if (settings.enableSound && audioSource != null)
                audioSource.PlayOneShot(clip);
        }

        private Sprite CreateRingSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            float radius = size / 2f;
            float innerRadius = radius * 0.75f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    bool inRing = dist < radius - 1 && dist > innerRadius;
                    texture.SetPixel(x, y, inRing ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
