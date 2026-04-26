using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Linq;

namespace DeerFunk.CartoonAdventures.Editor
{
    /// <summary>
    /// Editor utility to automatically set up the demo scene with all components
    /// </summary>
    public class DemoSceneSetup : EditorWindow
    {
        private const string TRACKDATA_FOLDER = "Assets/CartoonAdventures_Vol1_2/Demo/TrackData";

        [MenuItem("Tools/Deer Funk/Setup Demo Scene (Auto)")]
        public static void SetupDemoScene()
        {
            // Check if track data assets exist
            string[] trackDataGuids = AssetDatabase.FindAssets("t:MusicTrackData", new[] { TRACKDATA_FOLDER });

            if (trackDataGuids.Length == 0)
            {
                bool generate = EditorUtility.DisplayDialog(
                    "Track Data Not Found",
                    "No MusicTrackData assets found!\n\n" +
                    "Would you like to generate them automatically first?",
                    "Yes, Generate Now",
                    "Cancel"
                );

                if (generate)
                {
                    TrackDataGenerator.GenerateAllTrackData();
                    // Refresh to find newly created assets
                    trackDataGuids = AssetDatabase.FindAssets("t:MusicTrackData", new[] { TRACKDATA_FOLDER });
                }
                else
                {
                    return;
                }
            }

            // Load all track data assets
            var trackDataAssets = trackDataGuids
                .Select(guid => AssetDatabase.LoadAssetAtPath<MusicTrackData>(AssetDatabase.GUIDToAssetPath(guid)))
                .OrderBy(t => t.trackNumber)
                .ToArray();

            if (trackDataAssets.Length < 18)
            {
                Debug.LogWarning($"Only found {trackDataAssets.Length} track data assets. Expected 18.");
            }

            // Check if objects already exist in scene
            CartoonMusicManager existingManager = GameObject.FindObjectOfType<CartoonMusicManager>();
            MusicDemoController existingController = GameObject.FindObjectOfType<MusicDemoController>();

            if (existingManager != null || existingController != null)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Objects Already Exist",
                    "MusicManager or DemoController already exists in the scene.\n\n" +
                    "Do you want to continue anyway? (This will create duplicates)",
                    "Continue",
                    "Cancel"
                );

                if (!proceed) return;
            }

            // Create MusicManager GameObject
            GameObject musicManagerObj = new GameObject("MusicManager");
            CartoonMusicManager musicManager = musicManagerObj.AddComponent<CartoonMusicManager>();
            Debug.Log("✓ Created MusicManager GameObject");

            // Create DemoController GameObject
            GameObject demoControllerObj = new GameObject("DemoController");
            MusicDemoController demoController = demoControllerObj.AddComponent<MusicDemoController>();

            // Assign references using SerializedObject (for private serialized fields)
            SerializedObject serializedController = new SerializedObject(demoController);
            serializedController.FindProperty("musicManager").objectReferenceValue = musicManager;
            
            SerializedProperty tracksProperty = serializedController.FindProperty("tracks");
            tracksProperty.arraySize = trackDataAssets.Length;
            for (int i = 0; i < trackDataAssets.Length; i++)
            {
                tracksProperty.GetArrayElementAtIndex(i).objectReferenceValue = trackDataAssets[i];
            }
            serializedController.ApplyModifiedProperties();

            Debug.Log($"✓ Created DemoController GameObject with {trackDataAssets.Length} tracks assigned");

            // Mark scene as dirty
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Select the DemoController in hierarchy
            Selection.activeGameObject = demoControllerObj;

            // Show completion dialog
            EditorUtility.DisplayDialog(
                "Demo Scene Setup Complete!",
                $"Successfully set up demo scene:\n\n" +
                $"✓ MusicManager GameObject created\n" +
                $"✓ DemoController GameObject created\n" +
                $"✓ {trackDataAssets.Length} tracks assigned\n\n" +
                $"Press Play to test!\n\n" +
                $"Keyboard controls:\n" +
                $"• SPACE: Play/Pause\n" +
                $"• LEFT/RIGHT: Previous/Next track\n" +
                $"• R: Random track\n" +
                $"• S: Stop",
                "OK"
            );

            Debug.Log("=== Demo Scene Setup Complete ===");
            Debug.Log("Ready to test! Press Play button.");
        }
    }
}
