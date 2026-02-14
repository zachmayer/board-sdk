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
