using UnityEngine;

namespace GolfWall
{
    public class Wall : MonoBehaviour
    {
        private GolfWallSettings settings;
        private SpriteRenderer spriteRenderer;
        private float playAreaWidth;
        private float playAreaHeight;
        private float currentHeight;
        private float wallX;

        /// <summary>Top edge of the wall (ball must clear this Y to score).</summary>
        public float WallTopY => -playAreaHeight / 2f + currentHeight;

        /// <summary>Left face X of the wall.</summary>
        public float WallLeftX => wallX - settings.wallThickness / 2f;

        /// <summary>Right face X of the wall.</summary>
        public float WallRightX => wallX + settings.wallThickness / 2f;

        public void Initialize(GolfWallSettings gameSettings, float areaWidth, float areaHeight)
        {
            settings = gameSettings;
            playAreaWidth = areaWidth;
            playAreaHeight = areaHeight;
            wallX = Mathf.Lerp(-areaWidth / 2f, areaWidth / 2f, settings.wallXFraction);

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            spriteRenderer.color = settings.wallColor;

            SetWallForScore(0);
        }

        public void SetWallForScore(int score)
        {
            float halfHeight = playAreaHeight / 2f;
            currentHeight = CalculateWallHeight(score, playAreaHeight, settings.initialHeightFraction,
                settings.growthRate, settings.ballSize);

            // Wall is vertical: thin in X, tall in Y, anchored at bottom of screen
            transform.localScale = new Vector3(settings.wallThickness, currentHeight, 1);

            // Position: at wallX, bottom aligned to screen bottom
            float centerY = -halfHeight + currentHeight / 2f;
            transform.position = new Vector3(wallX, centerY, 0);
        }

        /// <summary>
        /// Pure static function for unit testing.
        /// Returns the wall height in world units for a given score.
        /// Grows asymptotically from initial toward max (full screen height minus one ball).
        /// </summary>
        public static float CalculateWallHeight(int score, float playAreaHeight, float initialHeightFraction,
            float growthRate, float ballSize)
        {
            float initialHeight = initialHeightFraction * playAreaHeight;
            float maxHeight = playAreaHeight - ballSize;

            // Asymptotic: height = max - (max - initial) / (1 + score * rate)
            return maxHeight - (maxHeight - initialHeight) / (1f + score * growthRate);
        }
    }
}
