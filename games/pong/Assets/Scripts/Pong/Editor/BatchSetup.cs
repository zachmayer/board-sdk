#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Pong.Editor
{
    public static class BatchSetup
    {
        public static void SetupAndSave()
        {
            // Open the scene explicitly
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

            // Run the setup
            PongEditorSetup.SetupScene();

            // Save the scene
            EditorSceneManager.SaveOpenScenes();

            // Save all assets
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
