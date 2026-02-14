#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace GolfWall.Editor
{
    public static class GolfWallEditorSetup
    {
        [MenuItem("Board/Golf Wall/Setup Scene")]
        public static void SetupScene()
        {
            // Create settings asset if it doesn't exist
            string settingsPath = "Assets/Resources/GolfWallSettings.asset";
            GolfWallSettings settings = AssetDatabase.LoadAssetAtPath<GolfWallSettings>(settingsPath);

            if (settings == null)
            {
                if (!Directory.Exists("Assets/Resources"))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                settings = ScriptableObject.CreateInstance<GolfWallSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Created GolfWallSettings at " + settingsPath);
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
            cam.backgroundColor = settings.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor;

            // Remove any existing game objects
            DestroyExisting("GolfWallGame");
            DestroyExisting("Ball");
            DestroyExisting("Wall");
            DestroyExisting("PieceIndicator");
            DestroyExisting("Canvas");

            // Create game controller
            GameObject gameController = new GameObject("GolfWallGame");
            GolfWallGame game = gameController.AddComponent<GolfWallGame>();

            // Link settings via SerializedObject
            SerializedObject so = new SerializedObject(game);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(game);

            Debug.Log("Golf Wall scene setup complete!");
            Debug.Log("To tweak settings: select GolfWallSettings in Assets/Resources/");

            Selection.activeGameObject = gameController;
        }

        [MenuItem("Board/Golf Wall/Open Settings")]
        public static void OpenSettings()
        {
            string settingsPath = "Assets/Resources/GolfWallSettings.asset";
            GolfWallSettings settings = AssetDatabase.LoadAssetAtPath<GolfWallSettings>(settingsPath);

            if (settings == null)
            {
                Debug.LogWarning("GolfWallSettings not found. Run 'Board > Golf Wall > Setup Scene' first.");
                return;
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        private static void DestroyExisting(string name)
        {
            GameObject obj = GameObject.Find(name);
            if (obj != null)
                Object.DestroyImmediate(obj);
        }
    }
}
#endif
