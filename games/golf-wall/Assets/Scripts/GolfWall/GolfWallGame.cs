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
        private bool ballClearedWall;

        // Piece tracking (raw, no smoothing for minimum latency)
        private GameObject pieceIndicator;
        private Vector3 pieceWorldPos;
        private float pieceOrientationDeg; // Unity Z degrees
        private bool pieceTracked;

        // Club visual (child of pieceIndicator)
        private GameObject clubObject;
        private Vector2 prevClubTipPos;
        private bool hasPrevClubTip;

        // Tee
        private GameObject teeObject;
        private Vector3 teePosition; // ball center when on tee
        private Vector3 teeBasePosition;

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
        private AudioClip hitSound;

        // Shared white sprite for rectangles
        private Sprite whiteSprite;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 60f;

            mainCamera = Camera.main;

            playAreaHeight = mainCamera.orthographicSize * 2f;
            playAreaWidth = playAreaHeight * mainCamera.aspect;

            whiteSprite = CreateWhiteSprite();

            ComputeTeePosition();
            CreateGameObjects();

            if (scoreText == null || messageText == null)
                CreateUI();

            CreateAudio();

            Debug.Log($"[GolfWall] Camera ortho={mainCamera.orthographicSize} aspect={mainCamera.aspect:F2} " +
                $"playArea={playAreaWidth:F1}x{playAreaHeight:F1}");
            Debug.Log($"[GolfWall] Wall: leftX={wall.WallLeftX:F2} rightX={wall.WallRightX:F2} " +
                $"topY={wall.WallTopY:F2}");
            Debug.Log($"[GolfWall] Tee: base={teeBasePosition} ball={teePosition}");

            // Configure Board pause screen
            BoardApplication.SetPauseScreenContext(applicationName: "Golf Wall");
            BoardApplication.pauseScreenActionReceived += OnPauseAction;

            UpdateScoreDisplay();
            ShowMessage("Place robot piece\nnear the ball");

            // Place ball on tee
            ball.PlaceOnTee(teePosition);
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

        private void ComputeTeePosition()
        {
            float halfWidth = playAreaWidth / 2f;
            float halfHeight = playAreaHeight / 2f;

            // Wall X position
            float wallX = Mathf.Lerp(-halfWidth, halfWidth, settings.wallXFraction);
            // Tee X: fraction of the way from left edge toward wall
            float teeX = Mathf.Lerp(-halfWidth, wallX, settings.teeXFraction);
            // Tee base Y: offset from bottom edge
            float teeBaseY = -halfHeight + settings.teeBottomOffset;

            teeBasePosition = new Vector3(teeX, teeBaseY, 0);
            // Ball center sits on top of tee
            teePosition = new Vector3(teeX, teeBaseY + settings.teeHeight + settings.ballSize / 2f, 0);
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

            // Create piece indicator (empty root — no scale, rotated by orientation)
            pieceIndicator = new GameObject("PieceIndicator");
            pieceIndicator.SetActive(false);

            // Ring child (visual circle around piece)
            var ringObj = new GameObject("Ring");
            ringObj.transform.SetParent(pieceIndicator.transform, false);
            var ringRenderer = ringObj.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = CreateRingSprite();
            ringRenderer.color = settings.pieceIndicatorColor;
            ringObj.transform.localScale = Vector3.one * settings.hitDetectionRadius * 2f;

            // Club child (rectangle extending from ring edge)
            clubObject = new GameObject("Club");
            clubObject.transform.SetParent(pieceIndicator.transform, false);
            var clubRenderer = clubObject.AddComponent<SpriteRenderer>();
            clubRenderer.sprite = whiteSprite;
            clubRenderer.color = settings.clubColor;
            clubRenderer.sortingOrder = 1;
            // Scale: club length x club width (in world units, parent has no scale)
            clubObject.transform.localScale = new Vector3(settings.clubLength, settings.clubWidth, 1);
            // Offset: starts at ring edge, extends outward along local +X
            clubObject.transform.localPosition = new Vector3(
                settings.hitDetectionRadius + settings.clubLength * 0.5f, 0, 0);

            // Create tee visual
            CreateTee();

            // Create landing zone visual on right side
            CreateLandingZone();

            // Create background
            CreateBackground();
        }

        private void CreateTee()
        {
            teeObject = new GameObject("Tee");
            var renderer = teeObject.AddComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = settings.teeColor;
            renderer.sortingOrder = -1;

            teeObject.transform.localScale = new Vector3(0.08f, settings.teeHeight, 1);
            teeObject.transform.position = teeBasePosition + Vector3.up * (settings.teeHeight * 0.5f);
        }

        private void CreateLandingZone()
        {
            // Landing zone is now just part of the background scenery
            // No separate colored rectangle needed with pixel art backgrounds
        }

        private void CreateBackground()
        {
            // Try to load Kenney pixel art backgrounds
            Texture2D bg0 = Resources.Load<Texture2D>("Sprites/bg_green_0");
            Texture2D bg1 = Resources.Load<Texture2D>("Sprites/bg_green_1");
            Texture2D bg2 = Resources.Load<Texture2D>("Sprites/bg_green_2");
            Texture2D grassTex = Resources.Load<Texture2D>("Sprites/grass");
            Texture2D dirtTex = Resources.Load<Texture2D>("Sprites/dirt");

            if (bg0 != null && bg1 != null && bg2 != null)
            {
                // Build composite background: 3 bands tiled horizontally
                int tileSize = bg0.width; // 18px
                int tilesX = Mathf.CeilToInt(playAreaWidth * 36f / tileSize) + 2;
                int bandHeight = tileSize;
                int texW = tilesX * tileSize;
                int texH = bandHeight * 3;

                Texture2D bgTex = new Texture2D(texW, texH);
                bgTex.filterMode = FilterMode.Point;

                Color[] pixels0 = bg0.GetPixels();
                Color[] pixels1 = bg1.GetPixels();
                Color[] pixels2 = bg2.GetPixels();

                for (int tx = 0; tx < tilesX; tx++)
                {
                    bgTex.SetPixels(tx * tileSize, bandHeight * 2, tileSize, tileSize, pixels0); // top
                    bgTex.SetPixels(tx * tileSize, bandHeight * 1, tileSize, tileSize, pixels1); // mid
                    bgTex.SetPixels(tx * tileSize, 0, tileSize, tileSize, pixels2); // bottom
                }
                bgTex.Apply();

                float ppu = texW / playAreaWidth;
                var bgObj = new GameObject("Background");
                var bgRenderer = bgObj.AddComponent<SpriteRenderer>();
                bgRenderer.sprite = Sprite.Create(bgTex, new Rect(0, 0, texW, texH),
                    new Vector2(0.5f, 0.5f), ppu);
                bgRenderer.sortingOrder = -10;
                bgObj.transform.position = new Vector3(0, 0, 0);

                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                // Use the sky color from the top tile as camera clear color
                mainCamera.backgroundColor = bg0.GetPixel(tileSize / 2, tileSize / 2);
            }
            else
            {
                mainCamera.backgroundColor = settings.backgroundColor;
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            // Ground strip using grass + dirt tiles
            if (grassTex != null && dirtTex != null)
            {
                int tileSize = grassTex.width;
                int tilesX = Mathf.CeilToInt(playAreaWidth * 36f / tileSize) + 2;
                int texW = tilesX * tileSize;
                int texH = tileSize * 2; // grass on top, dirt below

                Texture2D groundTex = new Texture2D(texW, texH);
                groundTex.filterMode = FilterMode.Point;

                Color[] grassPixels = grassTex.GetPixels();
                Color[] dirtPixels = dirtTex.GetPixels();

                for (int tx = 0; tx < tilesX; tx++)
                {
                    groundTex.SetPixels(tx * tileSize, tileSize, tileSize, tileSize, grassPixels);
                    groundTex.SetPixels(tx * tileSize, 0, tileSize, tileSize, dirtPixels);
                }
                groundTex.Apply();

                float ppu = texW / playAreaWidth;
                float groundHeight = (float)texH / ppu;
                var groundObj = new GameObject("Ground");
                var groundRenderer = groundObj.AddComponent<SpriteRenderer>();
                groundRenderer.sprite = Sprite.Create(groundTex, new Rect(0, 0, texW, texH),
                    new Vector2(0.5f, 1f), ppu); // pivot at top center
                groundRenderer.sortingOrder = -5;
                groundObj.transform.position = new Vector3(0, -playAreaHeight / 2f, 0);
            }
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
                        ShowMessage("Spin to swing!");
                    }
                    break;

                case GameState.ReadyToSwing:
                    if (!pieceTracked)
                    {
                        state = GameState.WaitingToStart;
                        ShowMessage("Place robot piece\nnear the ball");
                        break;
                    }
                    CheckForClubHit();
                    break;

                case GameState.BallInFlight:
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
                hasPrevClubTip = false;
                return;
            }

            // Track first glyph
            BoardContact contact = contacts[0];
            Vector3 rawPos = mainCamera.ScreenToWorldPoint(
                new Vector3(contact.screenPosition.x, contact.screenPosition.y, 10));
            rawPos.z = 0;
            pieceWorldPos = rawPos;

            // Orientation: SDK gives radians → Unity Z degrees
            float sdkOrientationRad = contact.orientation;
            pieceOrientationDeg = 90f + sdkOrientationRad * Mathf.Rad2Deg;

            pieceTracked = true;
            pieceIndicator.SetActive(true);
            pieceIndicator.transform.position = rawPos;
            pieceIndicator.transform.rotation = Quaternion.Euler(0, 0, pieceOrientationDeg);

            // Track club tip for swept hit detection
            Vector2 clubDir = (Vector2)pieceIndicator.transform.right;
            float tipRadius = settings.hitDetectionRadius + settings.clubLength;
            Vector2 currTipPos = (Vector2)pieceWorldPos + clubDir * tipRadius;

            if (!hasPrevClubTip)
            {
                prevClubTipPos = currTipPos;
                hasPrevClubTip = true;
            }
            // prevClubTipPos is updated after hit check in CheckForClubHit()

            // Angular velocity from SDK orientation (already in radians)
            double timestamp = contact.timestamp;

            if (hasLastOrientation && timestamp > lastTimestamp)
            {
                float dt = (float)(timestamp - lastTimestamp);
                float dAngle = AngleWrapDelta(sdkOrientationRad - lastOrientation);
                float angVel = dAngle / dt;

                if (Mathf.Abs(angVel) > Mathf.Abs(peakAngularVelocity))
                    peakAngularVelocity = angVel;
            }

            lastOrientation = sdkOrientationRad;
            lastTimestamp = timestamp;
            hasLastOrientation = true;

            // Decay peak over time
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

        /// <summary>
        /// Check if the club tip swept through the ball position this frame.
        /// Uses point-to-segment distance for swept collision detection.
        /// </summary>
        private void CheckForClubHit()
        {
            float absAngVel = Mathf.Abs(peakAngularVelocity);

            // Only check for hit if spinning fast enough
            if (absAngVel < settings.angularVelocityThreshold)
            {
                // Update previous tip position even when not hitting
                Vector2 clubDir = (Vector2)pieceIndicator.transform.right;
                float tipRadius = settings.hitDetectionRadius + settings.clubLength;
                prevClubTipPos = (Vector2)pieceWorldPos + clubDir * tipRadius;
                return;
            }

            Vector2 currClubDir = (Vector2)pieceIndicator.transform.right;
            float currTipRadius = settings.hitDetectionRadius + settings.clubLength;
            Vector2 currTipPos = (Vector2)pieceWorldPos + currClubDir * currTipRadius;

            Vector2 ballPos = (Vector2)teePosition;
            float ballRadius = settings.ballSize / 2f;
            float clubTipRadius = settings.clubWidth * 0.5f;
            float hitDist = ballRadius + clubTipRadius + 0.05f; // small forgiveness

            // Swept collision: distance from ball to the line segment (prevTip → currTip)
            float dist = DistancePointToSegment(ballPos, prevClubTipPos, currTipPos);

            if (dist <= hitDist)
            {
                Debug.Log($"[GolfWall] Club hit ball! angVel={peakAngularVelocity:F2} dist={dist:F3} " +
                    $"clubAngle={pieceOrientationDeg:F0}°");
                LaunchBallFromClub(peakAngularVelocity, currClubDir);
            }

            prevClubTipPos = currTipPos;
        }

        /// <summary>
        /// Distance from point p to the line segment a→b.
        /// </summary>
        public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLen2 = Vector2.Dot(ab, ab);
            if (abLen2 < 1e-8f) return Vector2.Distance(p, a);

            float t = Vector2.Dot(p - a, ab) / abLen2;
            t = Mathf.Clamp01(t);
            Vector2 closest = a + t * ab;
            return Vector2.Distance(p, closest);
        }

        private void LaunchBallFromClub(float signedAngularVelocity, Vector2 clubDir)
        {
            // Speed: tangential velocity of club tip = |angular velocity| * tip radius
            float tipRadius = settings.hitDetectionRadius + settings.clubLength;
            float tipSpeed = Mathf.Abs(signedAngularVelocity) * tipRadius;
            float speed = Mathf.Clamp(tipSpeed * settings.powerMultiplier,
                settings.minLaunchSpeed, settings.maxLaunchSpeed);

            // Direction: tangential velocity of tip = omega * perpCCW(clubDir)
            // perpCCW = (-dy, dx). Sign of omega naturally flips the direction.
            Vector2 perpCCW = new Vector2(-clubDir.y, clubDir.x);
            Vector2 launchDir = perpCCW * Mathf.Sign(signedAngularVelocity);

            // If ball would go downward, don't launch (bad swing direction)
            if (launchDir.y < 0.1f)
            {
                Debug.Log($"[GolfWall] Swing direction wrong (launchDir.y={launchDir.y:F2}), not launching");
                return;
            }

            // Clamp launch angle to [15°, 165°] — must go upward, allow wide range
            float launchAngle = Mathf.Atan2(launchDir.y, launchDir.x);
            float minAngle = 15f * Mathf.Deg2Rad;
            float maxAngle = 165f * Mathf.Deg2Rad;
            launchAngle = Mathf.Clamp(launchAngle, minAngle, maxAngle);
            launchDir = new Vector2(Mathf.Cos(launchAngle), Mathf.Sin(launchAngle));

            Vector2 launchVelocity = launchDir * speed;

            Debug.Log($"[GolfWall] Launch: speed={speed:F1} angle={launchAngle * Mathf.Rad2Deg:F0}° " +
                $"vel=({launchVelocity.x:F1},{launchVelocity.y:F1}) from={teePosition} " +
                $"wallTopY={wall.WallTopY:F2}");

            ball.Launch(teePosition, launchVelocity);
            state = GameState.BallInFlight;
            ballClearedWall = false;
            ShowMessage("");

            PlaySound(hitSound);
            ResetAngularTracking();
            hasPrevClubTip = false;
        }

        private void CheckCollisions()
        {
            Vector3 ballPos = ball.CurrentPosition;
            float halfBall = settings.ballSize / 2f;
            float halfWidth = playAreaWidth / 2f;
            float halfHeight = playAreaHeight / 2f;

            // --- Vertical wall collision (check first) ---
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

            // --- Ball cleared the wall (mark it, let it keep flying) ---
            if (!ballClearedWall && ballPos.x - halfBall > wall.WallRightX)
            {
                ballClearedWall = true;
                PlaySound(scoreSound);
                Debug.Log("[GolfWall] Ball cleared the wall!");
            }

            // --- Screen bounds ---
            // Left wall bounce
            if (ballPos.x - halfBall <= -halfWidth)
            {
                ball.BounceOffWall();
                ball.SnapPosition(new Vector3(-halfWidth + halfBall + 0.01f, ballPos.y, 0));
            }

            // Right wall bounce
            if (ballPos.x + halfBall >= halfWidth)
            {
                ball.BounceOffWall();
                ball.SnapPosition(new Vector3(halfWidth - halfBall - 0.01f, ballPos.y, 0));
            }

            // Ceiling bounce
            if (ballPos.y + halfBall >= halfHeight)
            {
                ball.BounceOffTopBottom();
                ball.SnapPosition(new Vector3(ballPos.x, halfHeight - halfBall - 0.01f, 0));
            }

            // Floor: score if cleared wall, miss if didn't
            if (ballPos.y - halfBall <= -halfHeight)
            {
                if (ballClearedWall)
                    Score();
                else
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

            // Reset ball to tee
            ball.PlaceOnTee(teePosition);

            Debug.Log($"[GolfWall] Score! Now {score}. Wall height growing.");
        }

        private void BallMissed()
        {
            ball.Stop();
            state = GameState.ReadyToSwing;
            ShowMessage("Missed! Try again!");

            // Reset ball to tee
            ball.PlaceOnTee(teePosition);

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

            swingSound = CreateFrequencySweep(200f, 800f, 0.15f);
            thudSound = CreateBeep(110f, 0.1f);
            scoreSound = CreateBeep(880f, 0.2f);
            hitSound = CreateBeep(660f, 0.08f); // short crack for club contact
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

        private Sprite CreateWhiteSprite()
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
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
