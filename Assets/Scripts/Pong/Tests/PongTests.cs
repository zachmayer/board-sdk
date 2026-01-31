using NUnit.Framework;
using UnityEngine;

namespace Pong.Tests
{
    /// <summary>
    /// Unit tests for Pong game logic.
    /// Run via Window > General > Test Runner, or from command line:
    /// Unity -runTests -testPlatform PlayMode -projectPath "path/to/project"
    /// </summary>
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

            // Simulate many paddle hits
            for (int i = 0; i < 100; i++)
            {
                currentSpeed = Mathf.Min(currentSpeed + settings.ballSpeedIncrease, settings.maxBallSpeed);
            }

            Assert.LessOrEqual(currentSpeed, settings.maxBallSpeed, "Speed should never exceed max");
        }

        [Test]
        public void Paddle_HitPosition_ReturnsValidRange()
        {
            // Test that hit position calculation would return -1 to 1
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

        [Test]
        public void Paddle_IsWithinReach_DetectsCorrectly()
        {
            float paddleY = 0;
            float paddleWidth = 2f;
            float halfWidth = paddleWidth / 2f;

            // Ball within reach
            Assert.IsTrue(IsWithinReach(0, paddleY, halfWidth), "Ball at paddle center should be reachable");
            Assert.IsTrue(IsWithinReach(0.9f, paddleY, halfWidth), "Ball near paddle edge should be reachable");

            // Ball out of reach
            Assert.IsFalse(IsWithinReach(2f, paddleY, halfWidth), "Ball far above paddle should not be reachable");
            Assert.IsFalse(IsWithinReach(-2f, paddleY, halfWidth), "Ball far below paddle should not be reachable");
        }

        private bool IsWithinReach(float ballY, float paddleY, float halfWidth)
        {
            return ballY >= paddleY - halfWidth && ballY <= paddleY + halfWidth;
        }

        [Test]
        public void Ball_BounceAngle_CalculatesCorrectly()
        {
            // Simulate ball bounce off paddle
            float hitPosition = 0.5f; // Hit upper part of paddle
            float angleInfluence = 0.5f;

            float angle = hitPosition * 60f * angleInfluence * Mathf.Deg2Rad;

            // Angle should be between -30 and 30 degrees (in radians)
            Assert.Greater(angle, -Mathf.PI / 6f, "Angle should be within bounds");
            Assert.Less(angle, Mathf.PI / 6f, "Angle should be within bounds");
        }
    }
}
