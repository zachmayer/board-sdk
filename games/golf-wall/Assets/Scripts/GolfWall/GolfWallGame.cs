using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Board.Input;
using Board.Core;

namespace GolfWall
{
    public class GolfWallGame : MonoBehaviour
    {
        [SerializeField] private GolfWallSettings settings;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text messageText;
        private Text bestText;

        private GolfBall ball;
        private Wall wall;

        private int score;
        private int bestScore;
        private const string BestScoreKey = "GolfWall.BestScore";
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

        // Desktop debug: mouse-based swing simulation (editor + Mac sim build)
        private bool mouseDebugMode;
        private bool mouseDown;
        private Vector3 mouseDragStart;

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

            // Mouse debug mode: mouse-based input when running on desktop (editor
            // play mode + the make gw-sim Mac build). Board hardware uses glyphs.
            mouseDebugMode = Application.isEditor ||
                Application.platform == RuntimePlatform.OSXPlayer ||
                Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.LinuxPlayer;

            bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
            UpdateScoreDisplay();
            UpdateBestDisplay();
            if (mouseDebugMode)
                ShowMessage("Click & drag from ball\nto aim, release to launch");
            else
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
            ringRenderer.color = GolfWallPalette.Ring;
            ringObj.transform.localScale = Vector3.one * settings.hitDetectionRadius * 2f;

            // Club child (rectangle extending from ring edge)
            clubObject = new GameObject("Club");
            clubObject.transform.SetParent(pieceIndicator.transform, false);
            var clubRenderer = clubObject.AddComponent<SpriteRenderer>();
            clubRenderer.sprite = whiteSprite;
            clubRenderer.color = GolfWallPalette.Club;
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
            renderer.color = GolfWallPalette.Tee;
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
            CreateGradientSky();
            CreateClouds();
            CreateGround();
        }

        /// <summary>Full-screen vertical gradient sky (smooth, fills the whole camera view).</summary>
        private void CreateGradientSky()
        {
            const int gh = 256;
            Texture2D tex = new Texture2D(1, gh);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < gh; y++)
            {
                float fromTop = 1f - (float)y / (gh - 1); // top of screen -> stop 0
                tex.SetPixel(0, y, SampleGradient(GolfWallPalette.Sky, fromTop));
            }
            tex.Apply();

            var skyObj = new GameObject("Sky");
            var sr = skyObj.AddComponent<SpriteRenderer>();
            // ppu=1 -> raw sprite is 1 x gh world units; stretch to fill the play area.
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, gh), new Vector2(0.5f, 0.5f), 1f);
            sr.sortingOrder = -100;
            skyObj.transform.position = Vector3.zero;
            skyObj.transform.localScale = new Vector3(playAreaWidth, playAreaHeight / gh, 1f);

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = GolfWallPalette.Sky[0];
        }

        /// <summary>Pixel grass+dirt ground band anchored to the bottom of the screen.</summary>
        private void CreateGround()
        {
            Texture2D grassTex = Resources.Load<Texture2D>("Sprites/grass");
            Texture2D dirtTex = Resources.Load<Texture2D>("Sprites/dirt");
            if (grassTex == null || dirtTex == null) return;

            const float worldTile = 0.6f; // world size of one source tile
            const int dirtRows = 2;
            int rowsY = 1 + dirtRows; // grass on top, dirt below
            int tileSize = grassTex.width; // 18px
            int tilesX = Mathf.CeilToInt(playAreaWidth / worldTile) + 1;

            int texW = tilesX * tileSize;
            int texH = rowsY * tileSize;
            Texture2D tex = new Texture2D(texW, texH);
            tex.filterMode = FilterMode.Point;

            Color[] grass = grassTex.GetPixels();
            Color[] dirt = dirtTex.GetPixels();
            for (int ty = 0; ty < rowsY; ty++)
            {
                Color[] row = (ty == rowsY - 1) ? grass : dirt; // top row = grass
                for (int tx = 0; tx < tilesX; tx++)
                    tex.SetPixels(tx * tileSize, ty * tileSize, tileSize, tileSize, row);
            }
            tex.Apply();

            float ppu = tileSize / worldTile;
            var obj = new GameObject("Ground");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH),
                new Vector2(0.5f, 0f), ppu); // pivot bottom-center
            sr.sortingOrder = -5;
            obj.transform.position = new Vector3(0, -playAreaHeight / 2f, 0);
        }

        /// <summary>Soft, semi-transparent clouds to give the sky depth and charm.</summary>
        private void CreateClouds()
        {
            Sprite cloud = CreateCloudSprite();
            float top = playAreaHeight / 2f;
            float w = playAreaWidth;
            float baseW = 160f / 100f; // cloud sprite world width at scale 1
            // xFrac (of width), yFrac (of top), widthFrac (of playAreaWidth), alpha
            float[,] specs =
            {
                { -0.30f, 0.58f, 0.20f, 0.80f },
                {  0.24f, 0.72f, 0.26f, 0.65f },
                {  0.06f, 0.38f, 0.15f, 0.55f },
                { -0.40f, 0.30f, 0.17f, 0.50f },
            };
            for (int i = 0; i < specs.GetLength(0); i++)
            {
                var o = new GameObject("Cloud");
                var sr = o.AddComponent<SpriteRenderer>();
                sr.sprite = cloud;
                sr.color = new Color(1f, 1f, 1f, specs[i, 3]);
                sr.sortingOrder = -50;
                o.transform.position = new Vector3(w * specs[i, 0], top * specs[i, 1], 0);
                o.transform.localScale = Vector3.one * (w * specs[i, 2] / baseW);
            }
        }

        /// <summary>A soft elliptical white blob used as a distant cloud.</summary>
        private Sprite CreateCloudSprite()
        {
            int cw = 160, ch = 70;
            Texture2D tex = new Texture2D(cw, ch);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 c = new Vector2(cw / 2f, ch / 2f);
            for (int y = 0; y < ch; y++)
            {
                for (int x = 0; x < cw; x++)
                {
                    float dx = (x - c.x) / (cw * 0.5f);
                    float dy = (y - c.y) / (ch * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, cw, ch), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Sample a multi-stop gradient. t=0 -> stops[0], t=1 -> stops[last].</summary>
        private static Color SampleGradient(Color[] stops, float t)
        {
            t = Mathf.Clamp01(t);
            float s = t * (stops.Length - 1);
            int i = Mathf.FloorToInt(s);
            if (i >= stops.Length - 1) return stops[stops.Length - 1];
            return Color.Lerp(stops[i], stops[i + 1], s - i);
        }

        private void Update()
        {
            if (mouseDebugMode)
            {
                ProcessMouseInput();
                return;
            }

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

        /// <summary>
        /// Desktop mouse input: click on ball to start drag, release to launch.
        /// Drag direction and length determine launch angle and power.
        /// Uses the Input System package (legacy Input is disabled in Player Settings).
        /// </summary>
        private void ProcessMouseInput()
        {
            if (state == GameState.BallInFlight) return;

            // Auto-transition to ReadyToSwing (no piece needed with a mouse)
            if (state == GameState.WaitingToStart)
            {
                state = GameState.ReadyToSwing;
                ShowMessage("Click & drag from ball\nto aim, release to launch");
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouse.position.ReadValue());
            mouseWorld.z = 0;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Start drag if near the ball/tee
                float dist = Vector2.Distance(mouseWorld, teePosition);
                if (dist < 1.5f)
                {
                    mouseDown = true;
                    mouseDragStart = mouseWorld;
                }
            }

            if (mouse.leftButton.wasReleasedThisFrame && mouseDown)
            {
                mouseDown = false;
                Vector2 dragVec = (Vector2)(mouseWorld - mouseDragStart);

                // Launch in the direction of drag
                float dragLen = dragVec.magnitude;
                if (dragLen > 0.3f)
                {
                    Vector2 dir = dragVec.normalized;
                    // If dragging downward, reject
                    if (dir.y < 0.1f)
                    {
                        ShowMessage("Drag upward to aim!");
                        return;
                    }

                    // Map drag length to speed
                    float speed = Mathf.Clamp(dragLen * 4f, settings.minLaunchSpeed, settings.maxLaunchSpeed);

                    // Clamp launch angle to [15°, 165°]
                    float angle = Mathf.Atan2(dir.y, dir.x);
                    angle = Mathf.Clamp(angle, 15f * Mathf.Deg2Rad, 165f * Mathf.Deg2Rad);
                    dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                    Vector2 launchVelocity = dir * speed;
                    Debug.Log($"[GolfWall][Mouse] Launch: speed={speed:F1} angle={angle * Mathf.Rad2Deg:F0}° " +
                        $"vel=({launchVelocity.x:F1},{launchVelocity.y:F1})");

                    ball.Launch(teePosition, launchVelocity);
                    state = GameState.BallInFlight;
                    ballClearedWall = false;
                    ShowMessage("");
                    PlaySound(hitSound);
                }
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
            if (score > bestScore)
            {
                bestScore = score;
                PlayerPrefs.SetInt(BestScoreKey, bestScore);
                PlayerPrefs.Save();
                UpdateBestDisplay();
            }
            StartCoroutine(ScorePop());
            wall.SetWallForScore(score);
            ball.Stop();
            state = mouseDebugMode ? GameState.WaitingToStart : GameState.ReadyToSwing;
            ShowMessage($"Nice shot!\nScore: {score}");
            PlaySound(scoreSound);

            // Reset ball to tee
            ball.PlaceOnTee(teePosition);

            Debug.Log($"[GolfWall] Score! Now {score}. Wall height growing.");
        }

        private void BallMissed()
        {
            ball.Stop();
            state = mouseDebugMode ? GameState.WaitingToStart : GameState.ReadyToSwing;
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

        private void UpdateBestDisplay()
        {
            if (bestText != null)
                bestText.text = bestScore > 0 ? $"Best: {bestScore}" : "";
        }

        /// <summary>Quick scale punch on the score text for a bit of juice on each point.</summary>
        private System.Collections.IEnumerator ScorePop()
        {
            if (scoreText == null) yield break;
            Transform t = scoreText.transform;
            const float dur = 0.22f;
            for (float e = 0f; e < dur; e += Time.deltaTime)
            {
                float k = e / dur;
                float s = 1f + 0.35f * (1f - k) * Mathf.Sin(k * Mathf.PI);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
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
            scoreText.fontSize = 54;
            scoreText.fontStyle = FontStyle.Bold;
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
            messageText.fontSize = 36;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            RectTransform msgRect = messageText.rectTransform;
            msgRect.anchorMin = new Vector2(0.2f, 0.4f);
            msgRect.anchorMax = new Vector2(0.8f, 0.6f);
            msgRect.offsetMin = Vector2.zero;
            msgRect.offsetMax = Vector2.zero;

            GameObject bestObj = new GameObject("BestText");
            bestObj.transform.SetParent(canvasObj.transform, false);
            bestText = bestObj.AddComponent<Text>();
            bestText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bestText.fontSize = 30;
            bestText.fontStyle = FontStyle.Bold;
            bestText.alignment = TextAnchor.UpperLeft;
            bestText.color = Color.white;
            RectTransform bestRect = bestText.rectTransform;
            bestRect.anchorMin = new Vector2(0.02f, 0.9f);
            bestRect.anchorMax = new Vector2(0.32f, 1f);
            bestRect.offsetMin = Vector2.zero;
            bestRect.offsetMax = Vector2.zero;

            AddTextOutline(scoreObj);
            AddTextOutline(msgObj);
            AddTextOutline(bestObj);
        }

        /// <summary>Dark outline so white UI text stays readable on any background.</summary>
        private static void AddTextOutline(GameObject go)
        {
            var outline = go.AddComponent<Outline>();
            outline.effectColor = GolfWallPalette.TextOutline;
            outline.effectDistance = new Vector2(2.5f, -2.5f);
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

    /// <summary>
    /// Central color palette — "Clear Day": cool gradient sky so the warm pixel
    /// foreground (grass, brick) pops, with high-contrast readable accents.
    /// Defined in code (not the .asset) so the whole look tunes in one place.
    /// </summary>
    public static class GolfWallPalette
    {
        // Sky gradient, top of screen -> horizon.
        public static readonly Color[] Sky =
        {
            new Color32(0x17, 0x34, 0x63, 255), // deep blue (top)
            new Color32(0x35, 0x6F, 0xB0, 255), // azure
            new Color32(0x7B, 0xB8, 0xDE, 255), // sky blue
            new Color32(0xC9, 0xE3, 0xE8, 255), // pale
            new Color32(0xFC, 0xE6, 0xBE, 255), // warm horizon (golden hour)
        };

        public static readonly Color Ball        = new Color32(0xFF, 0xFB, 0xEF, 255); // warm cream
        public static readonly Color BallOutline = new Color32(0x2A, 0x3A, 0x4A, 255); // dark rim
        public static readonly Color Tee         = new Color32(0x4A, 0x33, 0x28, 255); // dark wood
        public static readonly Color Club        = new Color32(0xFF, 0xC9, 0x3C, 255); // gold
        public static readonly Color Ring        = new Color32(0xFF, 0x70, 0x43, 110); // coral, translucent
        public static readonly Color TextOutline = new Color32(0x13, 0x22, 0x41, 230); // navy
    }
}
