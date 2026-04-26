using UnityEngine;
using System.Collections;

namespace DeerFunk.CartoonAdventures
{
    /// <summary>
    /// Simple music manager for Cartoon Adventures music pack.
    /// Handles playing, fading, and looping background music tracks.
    /// </summary>
    public class CartoonMusicManager : MonoBehaviour
    {
        [Header("Audio Source")]
        [SerializeField] private AudioSource musicSource;

        [Header("Settings")]
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float fadeTime = 1.5f;
        [SerializeField] private float defaultVolume = 0.7f;

        private Coroutine fadeCoroutine;

        private void Awake()
        {
            // Create audio source if not assigned
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
            }

            // Configure audio source for music
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = defaultVolume;
        }

        private void Start()
        {
            if (playOnStart && musicSource.clip != null)
            {
                PlayMusic();
            }
        }

        /// <summary>
        /// Play the currently assigned music clip
        /// </summary>
        public void PlayMusic()
        {
            if (musicSource.clip == null)
            {
                Debug.LogWarning("No audio clip assigned to CartoonMusicManager!");
                return;
            }

            musicSource.Play();
        }

        /// <summary>
        /// Play a specific music track
        /// </summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            musicSource.clip = clip;
            musicSource.Play();
        }

        /// <summary>
        /// Play a specific track with fade in
        /// </summary>
        public void PlayMusicWithFade(AudioClip clip, float fadeDuration = -1)
        {
            if (clip == null) return;

            if (fadeDuration < 0) fadeDuration = fadeTime;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeToNewTrack(clip, fadeDuration));
        }

        /// <summary>
        /// Stop the music
        /// </summary>
        public void StopMusic()
        {
            musicSource.Stop();
        }

        /// <summary>
        /// Stop music with fade out
        /// </summary>
        public void StopMusicWithFade(float fadeDuration = -1)
        {
            if (fadeDuration < 0) fadeDuration = fadeTime;

            if (fadeCoroutine != null)
                StopCoroutine(fadeCoroutine);

            fadeCoroutine = StartCoroutine(FadeOut(fadeDuration));
        }

        /// <summary>
        /// Pause the music
        /// </summary>
        public void PauseMusic()
        {
            musicSource.Pause();
        }

        /// <summary>
        /// Resume paused music
        /// </summary>
        public void ResumeMusic()
        {
            musicSource.UnPause();
        }

        /// <summary>
        /// Set the music volume
        /// </summary>
        public void SetVolume(float volume)
        {
            musicSource.volume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// Get current volume
        /// </summary>
        public float GetVolume()
        {
            return musicSource.volume;
        }

        /// <summary>
        /// Check if music is currently playing
        /// </summary>
        public bool IsPlaying()
        {
            return musicSource.isPlaying;
        }

        // Coroutine to fade out current track and fade in new track
        private IEnumerator FadeToNewTrack(AudioClip newClip, float duration)
        {
            float startVolume = musicSource.volume;

            // Fade out
            float elapsed = 0f;
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (duration / 2));
                yield return null;
            }

            // Switch track
            musicSource.Stop();
            musicSource.clip = newClip;
            musicSource.Play();

            // Fade in
            elapsed = 0f;
            while (elapsed < duration / 2)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, defaultVolume, elapsed / (duration / 2));
                yield return null;
            }

            musicSource.volume = defaultVolume;
        }

        // Coroutine to fade out
        private IEnumerator FadeOut(float duration)
        {
            float startVolume = musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            musicSource.volume = 0f;
            musicSource.Stop();
        }
    }
}
