using UnityEngine;

namespace DeerFunk.CartoonAdventures
{
    /// <summary>
    /// ScriptableObject to store music track information.
    /// Create instances via: Assets > Create > Deer Funk > Music Track
    /// </summary>
    [CreateAssetMenu(fileName = "NewMusicTrack", menuName = "Deer Funk/Music Track", order = 1)]
    public class MusicTrackData : ScriptableObject
    {
        [Header("Track Information")]
        [Tooltip("The name of the track")]
        public string trackName = "New Track";

        [Tooltip("Track number (e.g., 01, 02, 03)")]
        public int trackNumber = 1;

        [Tooltip("The audio clip for this track")]
        public AudioClip audioClip;

        [Header("Description")]
        [Tooltip("Brief description of the track's mood/style")]
        [TextArea(2, 4)]
        public string description = "Upbeat cartoon music";

        [Header("Metadata")]
        [Tooltip("Suggested use cases for this track")]
        public TrackMood mood = TrackMood.Upbeat;

        [Tooltip("Track duration in seconds (auto-calculated if clip is assigned)")]
        public float duration;

        [Tooltip("Is this track loopable?")]
        public bool isLoopable = true;

        // Auto-calculate duration when clip is assigned
        private void OnValidate()
        {
            if (audioClip != null)
            {
                duration = audioClip.length;
            }
        }

        /// <summary>
        /// Get formatted track name with number
        /// </summary>
        public string GetFormattedName()
        {
            return $"Track {trackNumber:D2} - {trackName}";
        }

        /// <summary>
        /// Get duration as formatted string (MM:SS)
        /// </summary>
        public string GetFormattedDuration()
        {
            int minutes = Mathf.FloorToInt(duration / 60f);
            int seconds = Mathf.FloorToInt(duration % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }
    }

    /// <summary>
    /// Enum for categorizing track moods
    /// </summary>
    public enum TrackMood
    {
        Upbeat,
        Funny,
        Silly,
        Action,
        Adventure,
        Mysterious,
        Victory,
        Menu
    }
}
