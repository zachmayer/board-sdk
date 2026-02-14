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

        private Texture2D brickTex;
        private Texture2D brickTopTex;

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

            // Load brick textures from Resources
            brickTex = Resources.Load<Texture2D>("Sprites/brick");
            brickTopTex = Resources.Load<Texture2D>("Sprites/brick_top");

            // Fallback if textures not found
            if (brickTex == null)
            {
                Debug.LogWarning("[Wall] brick texture not found, using solid color");
                brickTex = new Texture2D(1, 1);
                brickTex.SetPixel(0, 0, settings.wallColor);
                brickTex.Apply();
            }
            if (brickTopTex == null)
                brickTopTex = brickTex;

            SetWallForScore(0);
        }

        public void SetWallForScore(int score)
        {
            float halfHeight = playAreaHeight / 2f;
            currentHeight = CalculateWallHeight(score, playAreaHeight, settings.initialHeightFraction,
                settings.growthRate, settings.ballSize);

            // Build a tiled brick texture for the wall
            int tileSize = brickTex.width; // 18px
            int tilesX = Mathf.Max(1, Mathf.CeilToInt(settings.wallThickness * settings.wallTilePPU / tileSize));
            int tilesY = Mathf.Max(1, Mathf.CeilToInt(currentHeight * settings.wallTilePPU / tileSize));

            int texW = tilesX * tileSize;
            int texH = tilesY * tileSize;

            Texture2D wallTex = new Texture2D(texW, texH);
            wallTex.filterMode = FilterMode.Point;

            Color[] bodyPixels = brickTex.GetPixels();
            Color[] topPixels = brickTopTex.GetPixels();

            for (int ty = 0; ty < tilesY; ty++)
            {
                Color[] pixels = (ty == tilesY - 1) ? topPixels : bodyPixels;
                for (int tx = 0; tx < tilesX; tx++)
                {
                    wallTex.SetPixels(tx * tileSize, ty * tileSize, tileSize, tileSize, pixels);
                }
            }
            wallTex.Apply();

            float ppu = texW / settings.wallThickness;
            spriteRenderer.sprite = Sprite.Create(wallTex,
                new Rect(0, 0, texW, texH),
                new Vector2(0.5f, 0f), // pivot at bottom center
                ppu);
            spriteRenderer.color = Color.white; // use texture colors, not tint

            // Position: at wallX, bottom aligned to screen bottom
            transform.localScale = Vector3.one;
            transform.position = new Vector3(wallX, -halfHeight, 0);
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
