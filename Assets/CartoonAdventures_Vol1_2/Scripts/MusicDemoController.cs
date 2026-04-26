using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeerFunk.CartoonAdventures
{
    /// <summary>
    /// Demo script for showcasing the Cartoon Adventures music tracks.
    /// Displays track list and allows preview playback.
    /// NOTE: Requires TextMeshPro package installed in Unity.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicDemoController : MonoBehaviour
    {
        // Ensure only one demo controller is active at runtime to avoid duplicate UI/input handling
        private static MusicDemoController _instance;

        [Header("References")]
        [SerializeField] private CartoonMusicManager musicManager;
        [SerializeField] private MusicTrackData[] tracks;

        [Header("Settings")]
        [SerializeField] private bool playOnStart = true;

        [Header("UI References (Optional)")]
        [SerializeField] private TextMeshProUGUI currentTrackText;
        [SerializeField] private TextMeshProUGUI trackDurationText;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private Button playButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button previousButton;

        private int currentTrackIndex = 0;

        private void Awake()
        {
            // Singleton guard: if another instance exists, disable/destroy this one
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("MusicDemoController: Duplicate instance detected in scene. This instance will be destroyed to prevent duplicate text and controls.", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            // Setup UI listeners if assigned
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
                volumeSlider.value = musicManager != null ? musicManager.GetVolume() : 0.7f;
            }

            if (playButton != null)
                playButton.onClick.AddListener(PlayCurrentTrack);

            if (pauseButton != null)
                pauseButton.onClick.AddListener(TogglePause);

            if (stopButton != null)
                stopButton.onClick.AddListener(StopMusic);

            if (nextButton != null)
                nextButton.onClick.AddListener(NextTrack);

            if (previousButton != null)
                previousButton.onClick.AddListener(PreviousTrack);

            // Load first track
            if (tracks != null && tracks.Length > 0)
            {
                if (playOnStart)
                {
                    LoadAndPlayTrack(0); // Auto-play first track
                }
                else
                {
                    LoadTrack(0); // Just load, don't play
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up UI listeners to avoid leaks/ghost callbacks
            if (volumeSlider != null)
            {
                volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            }

            if (playButton != null) playButton.onClick.RemoveListener(PlayCurrentTrack);
            if (pauseButton != null) pauseButton.onClick.RemoveListener(TogglePause);
            if (stopButton != null) stopButton.onClick.RemoveListener(StopMusic);
            if (nextButton != null) nextButton.onClick.RemoveListener(NextTrack);
            if (previousButton != null) previousButton.onClick.RemoveListener(PreviousTrack);

            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Play the currently selected track
        /// </summary>
        public void PlayCurrentTrack()
        {
            if (musicManager == null || tracks == null || tracks.Length == 0) return;

            var track = tracks[currentTrackIndex];
            if (track != null && track.audioClip != null)
            {
                musicManager.PlayMusicWithFade(track.audioClip, 0.5f);
                UpdateUI();
            }
        }

        /// <summary>
        /// Load and play a specific track by index
        /// </summary>
        public void LoadAndPlayTrack(int index)
        {
            if (index < 0 || index >= tracks.Length) return;

            currentTrackIndex = index;
            PlayCurrentTrack();
        }

        /// <summary>
        /// Load track without playing
        /// </summary>
        public void LoadTrack(int index)
        {
            if (index < 0 || index >= tracks.Length) return;

            currentTrackIndex = index;
            UpdateUI();
        }

        /// <summary>
        /// Play next track in the list
        /// </summary>
        public void NextTrack()
        {
            currentTrackIndex = (currentTrackIndex + 1) % tracks.Length;
            PlayCurrentTrack();
        }

        /// <summary>
        /// Play previous track in the list
        /// </summary>
        public void PreviousTrack()
        {
            currentTrackIndex--;
            if (currentTrackIndex < 0)
                currentTrackIndex = tracks.Length - 1;
            PlayCurrentTrack();
        }

        /// <summary>
        /// Toggle pause/resume
        /// </summary>
        public void TogglePause()
        {
            if (musicManager == null) return;

            if (musicManager.IsPlaying())
            {
                musicManager.PauseMusic();
            }
            else
            {
                musicManager.ResumeMusic();
            }
        }

        /// <summary>
        /// Stop music playback
        /// </summary>
        public void StopMusic()
        {
            if (musicManager != null)
            {
                musicManager.StopMusicWithFade(0.5f);
            }
        }

        /// <summary>
        /// Handle volume slider changes
        /// </summary>
        private void OnVolumeChanged(float value)
        {
            if (musicManager != null)
            {
                musicManager.SetVolume(value);
            }
        }

        /// <summary>
        /// Update UI to reflect current track
        /// </summary>
        private void UpdateUI()
        {
            if (tracks == null || currentTrackIndex >= tracks.Length) return;

            var track = tracks[currentTrackIndex];
            if (track == null) return;

            if (currentTrackText != null)
            {
                currentTrackText.text = track.GetFormattedName();
            }

            if (trackDurationText != null)
            {
                trackDurationText.text = $"{track.GetFormattedDuration()} | {track.mood}";
            }
        }

        /// <summary>
        /// Shuffle and play random track
        /// </summary>
        public void PlayRandomTrack()
        {
            if (tracks == null || tracks.Length == 0) return;

            int randomIndex = Random.Range(0, tracks.Length);
            LoadAndPlayTrack(randomIndex);
        }

        // Keyboard shortcuts for demo
        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            // New Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    TogglePause();
                }
                else if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
                {
                    NextTrack();
                }
                else if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
                {
                    PreviousTrack();
                }
                else if (Keyboard.current.sKey.wasPressedThisFrame)
                {
                    StopMusic();
                }
                else if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    PlayRandomTrack();
                }
            }
#else
            // Legacy Input System
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                NextTrack();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                PreviousTrack();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                StopMusic();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                PlayRandomTrack();
            }
#endif
        }

        // Display controls in editor
        private void OnGUI()
        {
            // Only the primary instance may draw fallback GUI
            if (_instance != this) return;

            // Only show if no UI is assigned (fallback)
            if (currentTrackText == null)
            {
                // Larger window: increased from 400x200 to 600x400
                GUILayout.BeginArea(new Rect(10, 10, 600, 400));
                
                // Title - increased from 20 to 32
                GUILayout.Label("Cartoon Adventures Demo", new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold });
                
                if (tracks != null && currentTrackIndex < tracks.Length && tracks[currentTrackIndex] != null)
                {
                    // Track info - increased from default to 24
                    var trackStyle = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                    GUILayout.Label($"Current: {tracks[currentTrackIndex].GetFormattedName()}", trackStyle);
                    GUILayout.Label($"Duration: {tracks[currentTrackIndex].GetFormattedDuration()}", trackStyle);
                }

                GUILayout.Space(20); // Add some space
                
                // Controls label - increased to 20
                var controlsHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                GUILayout.Label("Controls:", controlsHeaderStyle);
                
                // Control instructions - increased from default to 18
                var controlsStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
                GUILayout.Label("Space - Play/Pause", controlsStyle);
                GUILayout.Label("Right Arrow - Next Track", controlsStyle);
                GUILayout.Label("Left Arrow - Previous Track", controlsStyle);
                GUILayout.Label("R - Random Track", controlsStyle);
                GUILayout.Label("S - Stop", controlsStyle);
                
                GUILayout.EndArea();
            }
        }
    }
}
