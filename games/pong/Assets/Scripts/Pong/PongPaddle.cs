using UnityEngine;

namespace Pong
{
    /// <summary>
    /// Controls a paddle that follows touch input.
    /// </summary>
    public class PongPaddle : MonoBehaviour
    {
        private PongSettings settings;
        private float targetY;
        private float minY;
        private float maxY;
        private bool isControlled;

        private SpriteRenderer spriteRenderer;

        public int PlayerIndex { get; private set; }
        public float XPosition => transform.position.x;

        public void Initialize(PongSettings gameSettings, int playerIndex, float xPosition, float playAreaHeight)
        {
            settings = gameSettings;
            PlayerIndex = playerIndex;

            // Set up bounds
            float halfPaddleWidth = settings.paddleWidth / 2f;
            float halfArea = playAreaHeight / 2f;
            minY = -halfArea + halfPaddleWidth;
            maxY = halfArea - halfPaddleWidth;

            // Position the paddle
            transform.position = new Vector3(xPosition, 0, 0);
            targetY = 0;

            // Set up visual
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = CreateRectSprite();
            spriteRenderer.color = playerIndex == 0 ? new Color(0.3f, 0.7f, 1f) : new Color(1f, 0.5f, 0.3f);
            transform.localScale = new Vector3(settings.paddleHeight, settings.paddleWidth, 1);
        }

        public void SetTargetPosition(float worldY)
        {
            targetY = Mathf.Clamp(worldY, minY, maxY);
            isControlled = true;
        }

        public void ReleaseControl()
        {
            isControlled = false;
        }

        public bool IsControlled => isControlled;

        private void Update()
        {
            // Smoothly move toward target position
            Vector3 pos = transform.position;
            float newY = Mathf.Lerp(pos.y, targetY, settings.paddleResponsiveness * Time.deltaTime);
            transform.position = new Vector3(pos.x, newY, pos.z);
        }

        public float GetHitPosition(float ballY)
        {
            // Returns -1 to 1 based on where the ball hit the paddle
            float paddleY = transform.position.y;
            float halfWidth = settings.paddleWidth / 2f;
            return Mathf.Clamp((ballY - paddleY) / halfWidth, -1f, 1f);
        }

        public bool IsWithinReach(float ballY)
        {
            float paddleY = transform.position.y;
            float halfWidth = settings.paddleWidth / 2f;
            return ballY >= paddleY - halfWidth && ballY <= paddleY + halfWidth;
        }

        private Sprite CreateRectSprite()
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
