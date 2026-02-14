using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfWall.Tests
{
    [TestFixture]
    public class GolfWallPlayModeTests
    {
        private GolfWallSettings settings;
        private Camera testCamera;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<GolfWallSettings>();
            settings.ballSize = 0.4f;
            settings.gravity = 12f;
            settings.powerMultiplier = 2f;
            settings.minLaunchSpeed = 8f;
            settings.maxLaunchSpeed = 18f;
            settings.wallBounceDamping = 0.7f;
            settings.angularVelocityThreshold = 4f;
            settings.clubLength = 1.0f;
            settings.clubWidth = 0.12f;
            settings.initialHeightFraction = 0.4f;
            settings.wallThickness = 0.3f;
            settings.growthRate = 0.2f;
            settings.hitDetectionRadius = 0.8f;
            settings.wallColor = Color.gray;
            settings.ballColor = Color.white;
            settings.pieceIndicatorColor = new Color(0.3f, 0.8f, 0.3f, 0.3f);

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

            foreach (var obj in Object.FindObjectsOfType<GolfBall>())
                Object.Destroy(obj.gameObject);
            foreach (var obj in Object.FindObjectsOfType<Wall>())
                Object.Destroy(obj.gameObject);
        }

        [UnityTest]
        public IEnumerator Ball_Initializes_WithSprite()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            yield return null;

            Assert.IsNotNull(ball, "Ball should be created");
            Assert.IsNotNull(ball.GetComponent<SpriteRenderer>(), "Ball should have SpriteRenderer");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Ball_PlacesOnTee_VisibleButInactive()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            Vector3 teePos = new Vector3(-2f, -3f, 0);
            ball.PlaceOnTee(teePos);

            yield return null;

            Assert.IsTrue(ballObj.activeSelf, "Ball should be visible on tee");
            Assert.IsFalse(ball.IsActive, "Ball should not have active physics on tee");
            Assert.AreEqual(teePos, ball.CurrentPosition, "Ball should be at tee position");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Ball_Launches_Upward()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            ball.Launch(Vector3.zero, new Vector2(8f, 12f));

            for (int i = 0; i < 5; i++)
            {
                ball.PhysicsStep();
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(ball.CurrentPosition.y, 0f, "Ball should move upward after launch");
            Assert.Greater(ball.CurrentPosition.x, 0f, "Ball should move rightward after launch");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Ball_Falls_UnderGravity()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            ball.Launch(new Vector3(-3, 0, 0), new Vector2(5f, 5f));

            float maxY = ball.CurrentPosition.y;
            bool startedFalling = false;

            for (int i = 0; i < 120; i++)
            {
                ball.PhysicsStep();
                yield return new WaitForFixedUpdate();

                if (ball.CurrentPosition.y > maxY)
                    maxY = ball.CurrentPosition.y;

                if (ball.CurrentPosition.y < maxY - 0.5f)
                {
                    startedFalling = true;
                    break;
                }
            }

            Assert.IsTrue(startedFalling, "Ball should fall back down due to gravity");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Ball_Stops_WhenStopped()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            ball.Launch(Vector3.zero, new Vector2(8f, 10f));
            ball.PhysicsStep();
            yield return new WaitForFixedUpdate();

            ball.Stop();

            yield return null;
            yield return null;

            Assert.IsFalse(ball.IsActive, "Ball should be inactive after Stop");

            Object.Destroy(ballObj);
        }

        [UnityTest]
        public IEnumerator Wall_Initializes_AtCorrectPosition()
        {
            float playAreaWidth = 10f * testCamera.aspect;
            float playAreaHeight = 10f;

            var wallObj = new GameObject("TestWall");
            var wall = wallObj.AddComponent<Wall>();
            wall.Initialize(settings, playAreaWidth, playAreaHeight);

            yield return null;

            Assert.AreEqual(0f, wallObj.transform.position.x, 0.01f,
                "Wall should be centered at x=0");

            float expectedHeight = Wall.CalculateWallHeight(0, playAreaHeight,
                settings.initialHeightFraction, settings.growthRate, settings.ballSize);
            float expectedTopY = -playAreaHeight / 2f + expectedHeight;
            Assert.AreEqual(expectedTopY, wall.WallTopY, 0.01f,
                "Wall top should be at expected Y for score 0");

            Object.Destroy(wallObj);
        }

        [UnityTest]
        public IEnumerator Wall_Grows_AfterScoring()
        {
            float playAreaWidth = 10f * testCamera.aspect;
            float playAreaHeight = 10f;

            var wallObj = new GameObject("TestWall");
            var wall = wallObj.AddComponent<Wall>();
            wall.Initialize(settings, playAreaWidth, playAreaHeight);

            yield return null;

            float initialTopY = wall.WallTopY;

            wall.SetWallForScore(5);

            yield return null;

            float newTopY = wall.WallTopY;
            Assert.Greater(newTopY, initialTopY, "Wall should be taller after scoring");

            Object.Destroy(wallObj);
        }

        [UnityTest]
        public IEnumerator Ball_Bounce_ReducesVelocity()
        {
            var ballObj = new GameObject("TestBall");
            var ball = ballObj.AddComponent<GolfBall>();
            ball.Initialize(settings);

            ball.Launch(Vector3.zero, new Vector2(15f, 5f));
            ball.PhysicsStep();
            yield return new WaitForFixedUpdate();

            float speedBefore = Mathf.Abs(ball.Velocity.x);

            ball.BounceOffWall();

            float speedAfter = Mathf.Abs(ball.Velocity.x);

            Assert.Less(speedAfter, speedBefore, "Bounce should reduce X speed due to damping");

            Object.Destroy(ballObj);
        }
    }
}
