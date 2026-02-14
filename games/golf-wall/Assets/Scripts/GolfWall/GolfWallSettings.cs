using UnityEngine;

namespace GolfWall
{
    [CreateAssetMenu(fileName = "GolfWallSettings", menuName = "GolfWall/Settings")]
    public class GolfWallSettings : ScriptableObject
    {
        [Header("Ball Settings")]
        [Tooltip("Size of the ball")]
        [Range(0.2f, 1f)]
        public float ballSize = 0.4f;

        [Tooltip("Gravity applied to ball (units/s²)")]
        [Range(1f, 30f)]
        public float gravity = 22f;

        [Tooltip("Multiplier from angular velocity to launch speed")]
        [Range(0.5f, 5f)]
        public float powerMultiplier = 2f;

        [Tooltip("Minimum launch speed")]
        [Range(1f, 20f)]
        public float minLaunchSpeed = 11f;

        [Tooltip("Maximum launch speed")]
        [Range(10f, 40f)]
        public float maxLaunchSpeed = 18f;

        [Tooltip("Speed retained when bouncing off wall (0-1)")]
        [Range(0f, 1f)]
        public float wallBounceDamping = 0.7f;

        [Header("Swing Settings")]
        [Tooltip("Minimum angular velocity (rad/s) to trigger a swing")]
        [Range(1f, 20f)]
        public float angularVelocityThreshold = 4f;

        [Header("Club Settings")]
        [Tooltip("Length of the club extending past the ring edge")]
        [Range(0.5f, 2f)]
        public float clubLength = 1.0f;

        [Tooltip("Width of the club rectangle")]
        [Range(0.05f, 0.3f)]
        public float clubWidth = 0.12f;

        [Header("Tee Settings")]
        [Tooltip("Height of the tee stick")]
        [Range(0.2f, 1f)]
        public float teeHeight = 0.4f;

        [Tooltip("Horizontal position of tee as fraction from left edge toward wall (0=left edge, 1=wall)")]
        [Range(0.2f, 0.8f)]
        public float teeXFraction = 0.45f;

        [Tooltip("Vertical position of tee base from bottom edge")]
        [Range(0.5f, 3f)]
        public float teeBottomOffset = 0.8f;

        [Header("Wall Settings")]
        [Tooltip("Initial wall height as fraction of screen height")]
        [Range(0.2f, 0.8f)]
        public float initialHeightFraction = 0.25f;

        [Tooltip("Horizontal position of wall as fraction of screen width (0=left, 1=right)")]
        [Range(0.3f, 0.8f)]
        public float wallXFraction = 0.67f;

        [Tooltip("Wall thickness in world units")]
        [Range(0.3f, 1.5f)]
        public float wallThickness = 0.6f;

        [Tooltip("Pixels per world unit for wall tile rendering")]
        public float wallTilePPU = 36f;

        [Tooltip("Growth rate for asymptotic wall height formula")]
        [Range(0.05f, 1f)]
        public float growthRate = 0.2f;

        [Header("Player Settings")]
        [Tooltip("Radius for detecting ball-piece collision")]
        [Range(0.3f, 1.5f)]
        public float hitDetectionRadius = 0.8f;

        [Header("Visuals")]
        public Color backgroundColor = new Color(0.08f, 0.12f, 0.08f);
        public Color wallColor = new Color(0.6f, 0.3f, 0.2f);
        public Color pieceIndicatorColor = new Color(0.3f, 0.8f, 0.3f, 0.3f);
        public Color clubColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        public Color teeColor = new Color(0.6f, 0.4f, 0.2f, 1f);
        public Color ballColor = Color.white;
        public Color landingZoneColor = new Color(0.2f, 0.5f, 0.2f, 0.3f);

        [Header("Audio")]
        [Tooltip("Enable sound effects")]
        public bool enableSound = true;
    }
}
