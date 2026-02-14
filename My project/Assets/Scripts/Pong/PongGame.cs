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
            Color blueGoal = new Color(0.2f, 0.4f, 0.6f, 0.5f);   // left - blue scores here
            Color orangeGoal = new Color(0.6f, 0.35f, 0.2f, 0.5f); // right - orange scores here

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

            // Paddle collisions (direction is where ball goes AFTER bounce)
            CheckPaddleCollision(leftPaddle, ballPos, halfBallSize, 1);   // left paddle → ball goes right
            CheckPaddleCollision(rightPaddle, ballPos, halfBallSize, -1); // right paddle → ball goes left
        }

        private void CheckPaddleCollision(PongPaddle paddle, Vector3 ballPos, float halfBallSize, int bounceDirection)
        {
            float paddleX = paddle.XPosition;
            float halfPaddleThickness = settings.paddleHeight / 2f;

            // Determine if this is left or right paddle based on position
            bool isLeftPaddle = paddleX < 0;

            // Check if ball overlaps paddle's X range
            bool atPaddleX = isLeftPaddle
                ? ballPos.x - halfBallSize <= paddleX + halfPaddleThickness
                : ballPos.x + halfBallSize >= paddleX - halfPaddleThickness;

            if (!atPaddleX) return;

            // Check if ball is moving toward this paddle (not away)
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
                // Ball hit left wall - right player scores, ball bounces back
                ScorePoint(1);
                ball.BounceOffSideWall();
                // Push ball away from wall
                ball.transform.position = new Vector3(-halfWidth + halfBallSize + 0.05f, ballPos.y, ballPos.z);
            }
            else if (ballPos.x + halfBallSize >= halfWidth)
            {
                // Ball hit right wall - left player scores, ball bounces back
                ScorePoint(0);
                ball.BounceOffSideWall();
                // Push ball away from wall
                ball.transform.position = new Vector3(halfWidth - halfBallSize - 0.05f, ballPos.y, ballPos.z);
            }
        }

        private void ScorePoint(int playerIndex)
        {
            scores[playerIndex]++;
            UpdateScoreDisplay();

            // Check for winner
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
            // Serve in random direction
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

            // Draw scores if no UI is set up
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
