using UnityEngine;

namespace GolfWall
{
    public class GolfBall : MonoBehaviour
    {
        private GolfWallSettings settings;
        private Vector2 velocity;
        private bool isActive;

        private SpriteRenderer spriteRenderer;

        // Interpolation state (60Hz physics, 120Hz render)
        private Vector3 previousPosition;
        private Vector3 currentPosition;

        public Vector2 Velocity => velocity;
        public Vector3 CurrentPosition => currentPosition;
        public bool IsActive => isActive;

        public void Initialize(GolfWallSettings gameSettings)
        {
            settings = gameSettings;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = Color.white; // fill + outline are baked into the sprite
            transform.localScale = Vector3.one * settings.ballSize;

            previousPosition = transform.position;
            currentPosition = transform.position;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Launch the ball with a specific velocity vector (vx, vy).
        /// </summary>
        public void Launch(Vector3 position, Vector2 launchVelocity)
        {
            gameObject.SetActive(true);

            velocity = launchVelocity;
            currentPosition = position;
            previousPosition = position;
            transform.position = position;
            isActive = true;
        }

        /// <summary>Show ball on tee (visible but no physics).</summary>
        public void PlaceOnTee(Vector3 position)
        {
            gameObject.SetActive(true);
            isActive = false;
            velocity = Vector2.zero;
            currentPosition = position;
            previousPosition = position;
            transform.position = position;
        }

        public void Stop()
        {
            isActive = false;
            velocity = Vector2.zero;
            gameObject.SetActive(false);
        }

        public void PhysicsStep()
        {
            if (!isActive) return;

            previousPosition = currentPosition;

            // Apply gravity
            velocity.y -= settings.gravity * Time.fixedDeltaTime;

            // Integrate position
            currentPosition += (Vector3)(velocity * Time.fixedDeltaTime);
        }

        private void Update()
        {
            if (!isActive) return;

            float t = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            t = Mathf.Clamp01(t);
            transform.position = Vector3.Lerp(previousPosition, currentPosition, t);
        }

        public void SnapPosition(Vector3 pos)
        {
            currentPosition = pos;
            previousPosition = pos;
            transform.position = pos;
        }

        /// <summary>Bounce off a vertical wall (reverse X velocity with damping).</summary>
        public void BounceOffWall()
        {
            velocity.x = -velocity.x * settings.wallBounceDamping;
        }

        /// <summary>Bounce off top/bottom screen edges (reverse Y).</summary>
        public void BounceOffTopBottom()
        {
            velocity.y = -velocity.y;
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Bilinear;
            float radius = size / 2f;
            float rim = 4f; // outline thickness in px
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    Color c;
                    if (dist > radius - 1) c = Color.clear;
                    else if (dist > radius - 1 - rim) c = GolfWallPalette.BallOutline;
                    else c = GolfWallPalette.Ball;
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
