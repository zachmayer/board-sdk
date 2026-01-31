using UnityEngine;
using UnityEngine.UI;

namespace Pong
{
    /// <summary>
    /// Attach this to any GameObject in your scene to auto-setup Pong.
    /// Creates all necessary objects if they don't exist.
    /// </summary>
    public class PongSceneSetup : MonoBehaviour
    {
        [SerializeField] private PongSettings settings;

        private void Awake()
        {
            // Find or create settings
            if (settings == null)
            {
                settings = Resources.Load<PongSettings>("PongSettings");
                if (settings == null)
                {
                    Debug.LogWarning("PongSettings not found in Resources. Creating default settings.");
                    settings = ScriptableObject.CreateInstance<PongSettings>();
                }
            }

            // Setup camera if needed
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.position = new Vector3(0, 0, -10);
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            }

            // Create game controller
            GameObject gameController = new GameObject("PongGame");
            PongGame game = gameController.AddComponent<PongGame>();

            // Use reflection to set the private settings field
            var settingsField = typeof(PongGame).GetField("settings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            settingsField?.SetValue(game, settings);

            // Create UI
            CreateUI(game);

            // Remove this setup script
            Destroy(this.gameObject);
        }

        private void CreateUI(PongGame game)
        {
            // Create Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // Create Score Text
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(canvasObj.transform, false);
            Text scoreText = scoreObj.AddComponent<Text>();
            scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                             Resources.GetBuiltinResource<Font>("Arial.ttf");
            scoreText.fontSize = 72;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = Color.white;

            RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.anchoredPosition = new Vector2(0, -20);
            scoreRect.sizeDelta = new Vector2(300, 100);

            // Create Message Text
            GameObject msgObj = new GameObject("MessageText");
            msgObj.transform.SetParent(canvasObj.transform, false);
            Text msgText = msgObj.AddComponent<Text>();
            msgText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
                           Resources.GetBuiltinResource<Font>("Arial.ttf");
            msgText.fontSize = 48;
            msgText.alignment = TextAnchor.MiddleCenter;
            msgText.color = Color.white;

            RectTransform msgRect = msgObj.GetComponent<RectTransform>();
            msgRect.anchorMin = new Vector2(0.5f, 0.5f);
            msgRect.anchorMax = new Vector2(0.5f, 0.5f);
            msgRect.pivot = new Vector2(0.5f, 0.5f);
            msgRect.anchoredPosition = Vector2.zero;
            msgRect.sizeDelta = new Vector2(500, 200);

            // Link UI to game
            var scoreField = typeof(PongGame).GetField("scoreText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            scoreField?.SetValue(game, scoreText);

            var msgField = typeof(PongGame).GetField("messageText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            msgField?.SetValue(game, msgText);
        }
    }
}
