using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pong.Tests
{
    /// <summary>
    /// Play mode tests that verify the game actually runs.
    /// These tests instantiate real game objects and simulate gameplay.
    ///
    /// Run from CLI:
    /// Unity -batchmode -nographics -projectPath "path" -runTests -testPlatform PlayMode
    /// </summary>
    [TestFixture]
    public class PongPlayModeTests
    {
        private PongSettings settings;
        private Camera testCamera;

        [SetUp]
        public void SetUp()
        {
            // Create test settings
            settings = ScriptableObject.CreateInstance<PongSettings>();
            settings.ballSpeed = 10f;
            settings.paddleWidth = 2f;
            settings.paddleHeight = 0.3f;
            settings.paddleResponsiveness = 20f;
            settings.winningScore = 3;
            settings.ballSize = 0.3f;
            settings.paddleEdgeOffset = 1f;

            // Create camera
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

            // Clean up any game objects
            foreach (var obj in Object.FindObjectsOfType<PongBall>())
                Object.Destroy(obj.gameObject);
            foreach (var obj in Object.FindObjectsOfType<PongPaddle>())
                Object.Destroy(obj.gameObject);
        }

        [UnityTest]
        public IEnumerator Ball_Initializes_WithCorrectSettings()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            yield return null; // Wait one frame

            Assert.IsNotNull(ball, "Ball should be created");
            Assert.IsNotNull(ball.GetComponent<SpriteRenderer>(), "Ball should have SpriteRenderer");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Paddle_Initializes_AtCorrectPosition()
        {
            float expectedX = 5f;
            var paddleObj = new GameObject("TestPaddle");
            var paddle = paddleObj.AddComponent<PongPaddle>();
            paddle.Initialize(settings, 0, expectedX, 10f);

            yield return null;

            Assert.AreEqual(expectedX, paddle.XPosition, 0.001f, "Paddle should be at expected X position");
            Assert.AreEqual(0, paddle.PlayerIndex, "Player index should be 0");

            Object.Destroy(paddleObj);
        }

        [UnityTest]
        public IEnumerator Paddle_FollowsTargetPosition_Smoothly()
        {
            var paddleObj = new GameObject("TestPaddle");
            var paddle = paddleObj.AddComponent<PongPaddle>();
            paddle.Initialize(settings, 0, 0f, 10f);

            // Set target position
            paddle.SetTargetPosition(2f);

            // Wait a few frames
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            // Paddle should have moved toward target
            float paddleY = paddleObj.transform.position.y;
            Assert.Greater(paddleY, 0.5f, "Paddle should have moved toward target");

            Object.Destroy(paddleObj);
        }

        [UnityTest]
        public IEnumerator Ball_Moves_WhenServed()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            Vector3 startPos = ballObj.transform.position;

            // Serve the ball
            ball.Serve(1);

            // Wait several frames
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            Vector3 endPos = ballObj.transform.position;

            Assert.AreNotEqual(startPos, endPos, "Ball should have moved after serving");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Ball_Stops_WhenStopCalled()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            ball.Serve(1);
            yield return null;

            ball.Stop();
            Vector3 posAfterStop = ballObj.transform.position;

            yield return null;
            yield return null;

            Vector3 posLater = ballObj.transform.position;

            Assert.AreEqual(posAfterStop, posLater, "Ball should not move after Stop()");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Paddle_ClampsToPlayArea()
        {
            var paddleObj = new GameObject("TestPaddle");
            var paddle = paddleObj.AddComponent<PongPaddle>();
            paddle.Initialize(settings, 0, 0f, 10f); // Play area height = 10

            // Try to move paddle way outside bounds
            paddle.SetTargetPosition(100f);

            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }

            float paddleY = paddleObj.transform.position.y;

            // Should be clamped to play area (5 - paddleWidth/2 = ~4)
            Assert.Less(paddleY, 5f, "Paddle should be clamped to play area");

            Object.Destroy(paddleObj);
        }

        [UnityTest]
        public IEnumerator MultipleObjects_CanCoexist()
        {
            // Create full game setup
            var ballObj = new GameObject("Ball");
            var ball = ballObj.AddComponent<PongBall>();
            ball.Initialize(settings);

            var leftPaddleObj = new GameObject("LeftPaddle");
            var leftPaddle = leftPaddleObj.AddComponent<PongPaddle>();
            leftPaddle.Initialize(settings, 0, -5f, 10f);

            var rightPaddleObj = new GameObject("RightPaddle");
            var rightPaddle = rightPaddleObj.AddComponent<PongPaddle>();
            rightPaddle.Initialize(settings, 1, 5f, 10f);

            yield return null;

            Assert.IsNotNull(ball, "Ball exists");
            Assert.IsNotNull(leftPaddle, "Left paddle exists");
            Assert.IsNotNull(rightPaddle, "Right paddle exists");
            Assert.AreEqual(0, leftPaddle.PlayerIndex);
            Assert.AreEqual(1, rightPaddle.PlayerIndex);

            Object.Destroy(ballObj);
            Object.Destroy(leftPaddleObj);
            Object.Destroy(rightPaddleObj);
        }
    }
}
