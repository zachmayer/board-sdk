using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Controls the ball movement and collision in Pong.
    /// Uses FixedUpdate for physics with interpolation for smooth rendering.
    /// </summary>
    public class PongBall : MonoBehaviour
    {
        private PongSettings settings;
        private Vector2 velocity;
        private float currentSpeed;
        private bool isActive;

        private SpriteRenderer spriteRenderer;

        // Interpolation state
        private Vector3 previousPosition;
        private Vector3 currentPosition;

        public Vector2 Velocity => velocity;
        public Vector3 CurrentPosition => currentPosition;

        public void Initialize(PongSettings gameSettings)
        {
            settings = gameSettings;
            currentSpeed = settings.ballSpeed;

            // Set up visual
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            // Create a circle sprite
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = Color.white;
            transform.localScale = Vector3.one * settings.ballSize;

            previousPosition = transform.position;
            currentPosition = transform.position;
        }

        public void Serve(int direction)
        {
            // Reset position to center
            transform.position = Vector3.zero;
            previousPosition = Vector3.zero;
            currentPosition = Vector3.zero;

            // Reset speed
            currentSpeed = settings.ballSpeed;

            // Launch at random angle toward the specified direction
            float angle = Random.Range(-30f, 30f) * Mathf.Deg2Rad;
            velocity = new Vector2(
                Mathf.Cos(angle) * direction,
                Mathf.Sin(angle)
            ).normalized * currentSpeed;

            isActive = true;
        }

        public void Stop()
        {
            isActive = false;
            velocity = Vector2.zero;
        }

        /// <summary>
        /// Called by PongGame in FixedUpdate for deterministic physics.
        /// </summary>
        public void PhysicsStep()
        {
            if (!isActive) return;

            previousPosition = currentPosition;
            currentPosition += (Vector3)(velocity * Time.fixedDeltaTime);
        }

        /// <summary>
        /// Interpolate between physics steps for smooth rendering.
        /// </summary>
        private void Update()
        {
            if (!isActive) return;

            // Interpolation factor: how far between the last two fixed steps
            float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            t = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(previousPosition, currentPosition, t);
        }

        /// <summary>
        /// Snap position (used after collision resolution to avoid interpolation artifacts).
        /// </summary>
        public void SnapPosition(Vector3 pos)
        {
            currentPosition = pos;
            previousPosition = pos;
            transform.position = pos;
        }

        public void BounceOffWall()
        {
            velocity.y = -velocity.y;
        }

        public void BounceOffSideWall()
        {
            velocity.x = -velocity.x;
        }

        public void BounceOffPaddle(float hitPosition, int direction)
        {
            // hitPosition is -1 to 1 (where on the paddle it hit)
            // direction is 1 for right, -1 for left

            // Increase speed
            currentSpeed = Mathf.Min(currentSpeed + settings.ballSpeedIncrease, settings.maxBallSpeed);

            // Curved paddle physics:
            // - Center hit (0) = straight back
            // - Edge hit (+/-1) = max angle (75 degrees)
            float maxAngle = 75f * settings.paddleAngleInfluence;
            float angle = hitPosition * maxAngle * Mathf.Deg2Rad;

            // Calculate new velocity
            float vx = Mathf.Cos(angle) * direction;
            float vy = Mathf.Sin(angle);

            // Enforce minimum horizontal speed (prevent vertical bouncing)
            float minHorizontal = 0.4f;
            if (Mathf.Abs(vx) < minHorizontal)
            {
                vx = minHorizontal * direction;
                vy = Mathf.Sqrt(1f - vx * vx) * Mathf.Sign(vy);
            }

            velocity = new Vector2(vx, vy).normalized * currentSpeed;
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            float radius = size / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, dist < radius - 1 ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
