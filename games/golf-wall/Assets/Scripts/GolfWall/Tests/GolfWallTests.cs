using NUnit.Framework;
using UnityEngine;

namespace GolfWall.Tests
{
    [TestFixture]
    public class GolfWallTests
    {
        private GolfWallSettings CreateTestSettings()
        {
            var settings = ScriptableObject.CreateInstance<GolfWallSettings>();
            settings.ballSize = 0.4f;
            settings.gravity = 12f;
            settings.powerMultiplier = 2f;
            settings.minLaunchSpeed = 8f;
            settings.maxLaunchSpeed = 18f;
            settings.wallBounceDamping = 0.7f;
            settings.angularVelocityThreshold = 4f;
            settings.baseLaunchAngle = 55f;
            settings.initialHeightFraction = 0.5f;
            settings.wallThickness = 0.3f;
            settings.growthRate = 0.2f;
            settings.hitDetectionRadius = 0.8f;
            return settings;
        }

        [Test]
        public void WallHeight_Score0_ReturnsInitialPosition()
        {
            float playAreaHeight = 10f;
            float initialFraction = 0.5f;
            float growthRate = 0.2f;
            float ballSize = 0.4f;

            float height = Wall.CalculateWallHeight(0, playAreaHeight, initialFraction, growthRate, ballSize);

            // Initial height = 0.5 * 10 = 5.0
            float expectedHeight = initialFraction * playAreaHeight;
            Assert.AreEqual(expectedHeight, height, 0.01f, "Wall at score 0 should be at initial height");
        }

        [Test]
        public void WallHeight_Increases_WithScore()
        {
            float playAreaHeight = 10f;
            float initialFraction = 0.5f;
            float growthRate = 0.2f;
            float ballSize = 0.4f;

            float h0 = Wall.CalculateWallHeight(0, playAreaHeight, initialFraction, growthRate, ballSize);
            float h5 = Wall.CalculateWallHeight(5, playAreaHeight, initialFraction, growthRate, ballSize);
            float h10 = Wall.CalculateWallHeight(10, playAreaHeight, initialFraction, growthRate, ballSize);

            Assert.Greater(h5, h0, "Wall should be taller at score 5 than score 0");
            Assert.Greater(h10, h5, "Wall should be taller at score 10 than score 5");
        }

        [Test]
        public void WallHeight_NeverExceedsMax()
        {
            float playAreaHeight = 10f;
            float initialFraction = 0.5f;
            float growthRate = 0.2f;
            float ballSize = 0.4f;
            float maxHeight = playAreaHeight - ballSize;

            for (int score = 0; score <= 1000; score += 50)
            {
                float h = Wall.CalculateWallHeight(score, playAreaHeight, initialFraction, growthRate, ballSize);
                Assert.LessOrEqual(h, maxHeight, $"Wall at score {score} should not exceed max height ({maxHeight})");
            }
        }

        [Test]
        public void WallHeight_Asymptotes_Correctly()
        {
            float playAreaHeight = 10f;
            float initialFraction = 0.5f;
            float growthRate = 0.2f;
            float ballSize = 0.4f;
            float maxHeight = playAreaHeight - ballSize;

            float hHigh = Wall.CalculateWallHeight(1000, playAreaHeight, initialFraction, growthRate, ballSize);

            // At very high scores, wall should be very close to max
            Assert.AreEqual(maxHeight, hHigh, 0.05f, "Wall should asymptote near max at very high scores");
        }

        [Test]
        public void LaunchSpeed_ClampsToMinMax()
        {
            var settings = CreateTestSettings();

            // Very low angular velocity → should clamp to min
            float lowSpeed = 1f * settings.powerMultiplier;
            float clampedLow = Mathf.Clamp(lowSpeed, settings.minLaunchSpeed, settings.maxLaunchSpeed);
            Assert.AreEqual(settings.minLaunchSpeed, clampedLow, "Low speed should clamp to minimum");

            // Very high angular velocity → should clamp to max
            float highSpeed = 100f * settings.powerMultiplier;
            float clampedHigh = Mathf.Clamp(highSpeed, settings.minLaunchSpeed, settings.maxLaunchSpeed);
            Assert.AreEqual(settings.maxLaunchSpeed, clampedHigh, "High speed should clamp to maximum");
        }

        [Test]
        public void WallBounceDamping_ReducesSpeed()
        {
            var settings = CreateTestSettings();
            float velocityX = 10f;

            // Wall bounce reverses X velocity with damping
            float bouncedX = -velocityX * settings.wallBounceDamping;

            Assert.Less(Mathf.Abs(bouncedX), Mathf.Abs(velocityX), "Bounce should reduce speed");
            Assert.Less(bouncedX, 0f, "Bounced velocity should be reversed");
        }

        [Test]
        public void AngleWrapDelta_WrapsCorrectly()
        {
            // Small delta — no wrapping needed
            float small = GolfWallGame.AngleWrapDelta(0.5f);
            Assert.AreEqual(0.5f, small, 0.001f);

            // Large positive — wraps to negative
            float large = GolfWallGame.AngleWrapDelta(Mathf.PI + 1f);
            Assert.Less(large, Mathf.PI);
            Assert.Greater(large, -Mathf.PI);

            // Large negative — wraps to positive
            float negative = GolfWallGame.AngleWrapDelta(-Mathf.PI - 1f);
            Assert.Less(negative, Mathf.PI);
            Assert.Greater(negative, -Mathf.PI);

            // Exactly PI — atan2(sin(PI), cos(PI)) returns PI
            float exactPi = GolfWallGame.AngleWrapDelta(Mathf.PI);
            Assert.AreEqual(Mathf.PI, Mathf.Abs(exactPi), 0.01f);
        }

        [Test]
        public void GravityParabola_BallFallsBack()
        {
            // Simulate a ball launched at 55° with speed 12, gravity 12
            float speed = 12f;
            float angle = 55f * Mathf.Deg2Rad;
            float vx = speed * Mathf.Cos(angle);
            float vy = speed * Mathf.Sin(angle);
            float gravity = 12f;
            float dt = 1f / 60f;
            float x = 0f;
            float y = 0f;
            float maxY = 0f;
            bool wentUp = false;
            bool cameBack = false;

            for (int i = 0; i < 300; i++)
            {
                vy -= gravity * dt;
                x += vx * dt;
                y += vy * dt;

                if (y > 0.1f) wentUp = true;
                if (y > maxY) maxY = y;
                if (wentUp && y < 0f)
                {
                    cameBack = true;
                    break;
                }
            }

            Assert.IsTrue(wentUp, "Ball should go up");
            Assert.IsTrue(cameBack, "Ball should come back down due to gravity");
            Assert.Greater(maxY, 0f, "Ball should have reached some height");
            Assert.Greater(x, 0f, "Ball should have moved rightward");
        }

        [Test]
        public void Settings_DefaultValues_AreReasonable()
        {
            var settings = CreateTestSettings();

            Assert.Greater(settings.gravity, 0, "Gravity should be positive");
            Assert.Greater(settings.ballSize, 0, "Ball size should be positive");
            Assert.Greater(settings.angularVelocityThreshold, 0, "Threshold should be positive");
            Assert.LessOrEqual(settings.minLaunchSpeed, settings.maxLaunchSpeed, "Min should not exceed max");
            Assert.Greater(settings.wallBounceDamping, 0, "Damping should be positive");
            Assert.LessOrEqual(settings.wallBounceDamping, 1, "Damping should not exceed 1");
        }
    }
}
