#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GolfWall.Editor
{
    public static class BatchSetup
    {
        public static void SetupAndSave()
        {
            // Open the scene explicitly
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

            // Run the setup
            GolfWallEditorSetup.SetupScene();

            // Save the scene
            EditorSceneManager.SaveOpenScenes();

            // Save all assets
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
