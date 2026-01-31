using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Controls the ball movement and collision in Pong.
    /// </summary>
    public class PongBall : MonoBehaviour
    {
        private PongSettings settings;
        private Vector2 velocity;
        private float currentSpeed;
        private bool isActive;

        private SpriteRenderer spriteRenderer;

        public Vector2 Velocity => velocity;

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
        }

        public void Serve(int direction)
        {
            // Reset position to center
            transform.position = Vector3.zero;

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

        private void Update()
        {
            if (!isActive) return;

            // Move ball
            Vector3 newPos = transform.position + (Vector3)(velocity * Time.deltaTime);
            transform.position = newPos;
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
