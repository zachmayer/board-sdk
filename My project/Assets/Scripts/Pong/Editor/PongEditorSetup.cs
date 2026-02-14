#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Pong.Editor
{
    /// <summary>
    /// Editor utilities for setting up Pong.
    /// </summary>
    public static class PongEditorSetup
    {
        [MenuItem("Board/Pong/Setup Scene")]
        public static void SetupScene()
        {
            // Create settings asset if it doesn't exist
            string settingsPath = "Assets/Resources/PongSettings.asset";
            PongSettings settings = AssetDatabase.LoadAssetAtPath<PongSettings>(settingsPath);

            if (settings == null)
            {
                // Ensure Resources folder exists
                if (!Directory.Exists("Assets/Resources"))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                settings = ScriptableObject.CreateInstance<PongSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created PongSettings at " + settingsPath);
            }

            // Setup camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0, 0, -10);
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Remove any existing Pong objects
            DestroyExisting("PongGame");
            DestroyExisting("Ball");
            DestroyExisting("LeftPaddle");
            DestroyExisting("RightPaddle");
            DestroyExisting("TopWall");
            DestroyExisting("BottomWall");
            DestroyExisting("Canvas");

            // Create game controller
            GameObject gameController = new GameObject("PongGame");
            PongGame game = gameController.AddComponent<PongGame>();

            // Link settings via SerializedObject
            SerializedObject so = new SerializedObject(game);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(game);

            Debug.Log("Pong scene setup complete! Press Play and use Board Simulator to test.");
            Debug.Log("To open simulator: Board > Input > Simulator");
            Debug.Log("To tweak settings: select PongSettings in Assets/Resources/");

            Selection.activeGameObject = gameController;
        }

        [MenuItem("Board/Pong/Open Settings")]
        public static void OpenSettings()
        {
            string settingsPath = "Assets/Resources/PongSettings.asset";
            PongSettings settings = AssetDatabase.LoadAssetAtPath<PongSettings>(settingsPath);

            if (settings == null)
            {
                Debug.LogWarning("PongSettings not found. Run 'Board > Pong > Setup Scene' first.");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Board/Pong/Quick Start Guide")]
        public static void ShowGuide()
        {
            EditorUtility.DisplayDialog("Pong Quick Start Guide",
                "1. Run 'Board > Pong > Setup Scene' (already done if you see this)\n\n" +
                "2. Press PLAY in Unity\n\n" +
                "3. Open the Board Simulator:\n" +
                "   Board > Input > Simulator\n\n" +
                "4. In the Simulator window:\n" +
                "   - Click 'Enable Simulation'\n" +
                "   - Select the finger icon from palette\n" +
                "   - Click in Game view to place fingers\n" +
                "   - DRAG placed fingers to move paddles\n" +
                "   - Click finger again to lift (remove)\n\n" +
                "5. To tweak the game:\n" +
                "   Board > Pong > Open Settings\n" +
                "   (changes apply in real-time during play!)\n\n" +
                "Blue paddle (left) = left side touches\n" +
                "Orange paddle (right) = right side touches",
                "Got it!");
        }

        private static void DestroyExisting(string name)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
#endif
