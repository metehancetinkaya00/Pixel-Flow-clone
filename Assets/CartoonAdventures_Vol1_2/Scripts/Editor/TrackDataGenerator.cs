using UnityEngine;
using UnityEditor;
using System.IO;

namespace DeerFunk.CartoonAdventures.Editor
{
    /// <summary>
    /// Editor utility to automatically generate all 18 MusicTrackData assets
    /// </summary>
    public class TrackDataGenerator : EditorWindow
    {
        private const string AUDIO_FOLDER_PATH = "Assets/CartoonAdventures_Vol1_2/Audio/WAV";
        private const string OUTPUT_FOLDER_PATH = "Assets/CartoonAdventures_Vol1_2/Demo/TrackData";

        [MenuItem("Tools/Deer Funk/Generate All Track Data Assets")]
        public static void GenerateAllTrackData()
        {
            // Create output folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER_PATH))
            {
                string parentFolder = Path.GetDirectoryName(OUTPUT_FOLDER_PATH).Replace('\\', '/');
                string folderName = Path.GetFileName(OUTPUT_FOLDER_PATH);
                AssetDatabase.CreateFolder(parentFolder, folderName);
            }

            // Track definitions
            var trackDefinitions = new[]
            {
                new TrackDefinition(1, "Welcome to Toonland", "Track01_WelcomeToToonland", TrackMood.Upbeat, "Opening theme - cheerful and welcoming"),
                new TrackDefinition(2, "Bouncy Streets", "Track02_BouncyStreets", TrackMood.Upbeat, "Fun platformer level music"),
                new TrackDefinition(3, "The Banana Chase", "Track03_BananaChase", TrackMood.Action, "Fast-paced chase scene"),
                new TrackDefinition(4, "Ta-Daaa!", "Track04_TaDaaa", TrackMood.Victory, "Success and achievement theme"),
                new TrackDefinition(5, "Oops Banana Peel", "Track05_OopsBananaPeel", TrackMood.Funny, "Silly accident or failure moment"),
                new TrackDefinition(6, "Toy Store Swing", "Track06_ToyStoreSwing", TrackMood.Upbeat, "Jazzy shop or menu music"),
                new TrackDefinition(7, "Crazy Chicken Attack", "Track07_CrazyChickenAttack", TrackMood.Action, "Chaotic boss battle or enemy encounter"),
                new TrackDefinition(8, "Mischief in the Closet", "Track08_MischiefInTheCloset", TrackMood.Mysterious, "Sneaky exploration music"),
                new TrackDefinition(9, "The Great Escape", "Track09_GreatEscape", TrackMood.Adventure, "Escape sequence or running level"),
                new TrackDefinition(10, "See You Soon, Hero!", "Track10_SeeYouSoonHero", TrackMood.Menu, "Ending or credits music"),
                new TrackDefinition(11, "Bubblegum Battlefield", "Track11_BubblegumBattlefield", TrackMood.Action, "Candy-themed combat level"),
                new TrackDefinition(12, "Sneaky Socks Squad", "Track12_SneakySocksSquad", TrackMood.Mysterious, "Stealth level music"),
                new TrackDefinition(13, "Jellybean Jungle", "Track13_JellybeanJungle", TrackMood.Adventure, "Colorful exploration theme"),
                new TrackDefinition(14, "Popcorn Parade", "Track14_PopcornParade", TrackMood.Upbeat, "Festival or celebration music"),
                new TrackDefinition(15, "Funky Duck Disco", "Track15_FunkyDuckDisco", TrackMood.Silly, "Groovy dance party theme"),
                new TrackDefinition(16, "Cookie Carnival", "Track16_CookieCarnival", TrackMood.Upbeat, "Carnival or fair level music"),
                new TrackDefinition(17, "Silly Spy Sneakers", "Track17_SillySpySneakers", TrackMood.Mysterious, "Comedy spy mission theme"),
                new TrackDefinition(18, "Rubber Duck Rumble", "Track18_RubberDuckRumble", TrackMood.Action, "Bath-time battle music")
            };

            int successCount = 0;
            int skipCount = 0;

            foreach (var trackDef in trackDefinitions)
            {
                string assetPath = $"{OUTPUT_FOLDER_PATH}/TrackData_{trackDef.trackNumber:D2}_{trackDef.fileName}.asset";

                // Skip if already exists
                if (File.Exists(assetPath))
                {
                    Debug.Log($"Skipping Track {trackDef.trackNumber:D2} - already exists");
                    skipCount++;
                    continue;
                }

                // Load the audio clip
                string audioPath = $"{AUDIO_FOLDER_PATH}/{trackDef.fileName}.wav";
                AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);

                if (audioClip == null)
                {
                    Debug.LogWarning($"Could not find audio clip at: {audioPath}");
                    continue;
                }

                // Create the ScriptableObject
                MusicTrackData trackData = ScriptableObject.CreateInstance<MusicTrackData>();
                trackData.trackName = trackDef.trackName;
                trackData.trackNumber = trackDef.trackNumber;
                trackData.audioClip = audioClip;
                trackData.description = trackDef.description;
                trackData.mood = trackDef.mood;
                trackData.isLoopable = true;

                // Save the asset
                AssetDatabase.CreateAsset(trackData, assetPath);
                successCount++;

                Debug.Log($"Created: {trackData.GetFormattedName()}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Track Data Generation Complete",
                $"Successfully created {successCount} track data assets!\n" +
                $"Skipped {skipCount} existing assets.\n\n" +
                $"Location: {OUTPUT_FOLDER_PATH}",
                "OK"
            );

            Debug.Log($"=== Track Data Generation Complete ===");
            Debug.Log($"Created: {successCount} assets");
            Debug.Log($"Skipped: {skipCount} assets");
            Debug.Log($"Location: {OUTPUT_FOLDER_PATH}");
        }

        // Helper class to define track metadata
        private class TrackDefinition
        {
            public int trackNumber;
            public string trackName;
            public string fileName;
            public TrackMood mood;
            public string description;

            public TrackDefinition(int number, string name, string file, TrackMood trackMood, string desc)
            {
                trackNumber = number;
                trackName = name;
                fileName = file;
                mood = trackMood;
                description = desc;
            }
        }
    }
}
