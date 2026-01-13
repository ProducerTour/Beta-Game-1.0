using UnityEngine;
using System.Collections.Generic;
using CreatorWorld.Core;
using CreatorWorld.Interfaces;

namespace CreatorWorld.Audio
{
    /// <summary>
    /// Centralized audio manager handling all game sounds.
    /// Implements IAudioService for use with ServiceLocator.
    /// </summary>
    public class AudioManager : ServiceBehaviour<IAudioService>, IAudioService
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource uiSource;
        [SerializeField] private int sfxPoolSize = 10;

        [Header("Volume Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

        [Header("Footstep Clips")]
        [SerializeField] private AudioClip[] footstepWalk;
        [SerializeField] private AudioClip[] footstepRun;
        [SerializeField] private AudioClip[] footstepLand;

        [Header("Weapon Clips")]
        [SerializeField] private AudioClip[] rifleFire;
        [SerializeField] private AudioClip[] pistolFire;
        [SerializeField] private AudioClip[] reloadStart;
        [SerializeField] private AudioClip[] reloadComplete;
        [SerializeField] private AudioClip emptyClick;

        [Header("UI Clips")]
        [SerializeField] private AudioClip uiClick;
        [SerializeField] private AudioClip uiHover;
        [SerializeField] private AudioClip uiOpen;
        [SerializeField] private AudioClip uiClose;
        [SerializeField] private AudioClip uiError;
        [SerializeField] private AudioClip uiSuccess;

        [Header("Hit Marker Clips")]
        [SerializeField] private AudioClip hitMarkerNormal;
        [SerializeField] private AudioClip hitMarkerHeadshot;
        [SerializeField] private AudioClip hitMarkerKill;
        [SerializeField, Range(0f, 1f)] private float hitMarkerVolume = 0.5f;

        // Audio source pool for SFX
        private List<AudioSource> sfxPool;
        private int poolIndex;

        protected override void Awake()
        {
            base.Awake();
            InitializePool();
        }

        private void InitializePool()
        {
            sfxPool = new List<AudioSource>();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                var go = new GameObject($"SFX_Source_{i}");
                go.transform.SetParent(transform);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f; // 3D sound
                source.minDistance = 1f;
                source.maxDistance = 50f;
                source.rolloffMode = AudioRolloffMode.Linear;

                sfxPool.Add(source);
            }

            // Ensure music source exists
            if (musicSource == null)
            {
                var musicGo = new GameObject("Music_Source");
                musicGo.transform.SetParent(transform);
                musicSource = musicGo.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f; // 2D
            }

            // Ensure UI source exists
            if (uiSource == null)
            {
                var uiGo = new GameObject("UI_Source");
                uiGo.transform.SetParent(transform);
                uiSource = uiGo.AddComponent<AudioSource>();
                uiSource.playOnAwake = false;
                uiSource.spatialBlend = 0f; // 2D
            }
        }

        private AudioSource GetPooledSource()
        {
            var source = sfxPool[poolIndex];
            poolIndex = (poolIndex + 1) % sfxPool.Count;
            return source;
        }

        private void PlayAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clip == null) return;

            var source = GetPooledSource();
            source.transform.position = position;
            source.clip = clip;
            source.volume = masterVolume * sfxVolume * volumeMultiplier;
            source.pitch = Random.Range(0.95f, 1.05f); // Slight variation
            source.Play();
        }

        private void PlayRandomAtPosition(AudioClip[] clips, Vector3 position, float volumeMultiplier = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            PlayAtPosition(clips[Random.Range(0, clips.Length)], position, volumeMultiplier);
        }

        #region IAudioService Implementation

        public void PlayFootstep(FootstepType type, Vector3 position)
        {
            switch (type)
            {
                case FootstepType.Walk:
                case FootstepType.Crouch:
                    PlayRandomAtPosition(footstepWalk, position, 0.5f);
                    break;
                case FootstepType.Run:
                case FootstepType.Sprint:
                    PlayRandomAtPosition(footstepRun, position, 0.7f);
                    break;
                case FootstepType.Land:
                    PlayRandomAtPosition(footstepLand, position, 0.8f);
                    break;
            }
        }

        public void PlayWeaponSound(WeaponSoundType type, Vector3 position)
        {
            switch (type)
            {
                case WeaponSoundType.Fire:
                    // Default to rifle, could be extended with weapon type parameter
                    PlayRandomAtPosition(rifleFire, position, 1f);
                    break;
                case WeaponSoundType.Reload:
                    PlayRandomAtPosition(reloadStart, position, 0.8f);
                    break;
                case WeaponSoundType.ReloadComplete:
                    PlayRandomAtPosition(reloadComplete, position, 0.8f);
                    break;
                case WeaponSoundType.Empty:
                    PlayAtPosition(emptyClick, position, 0.6f);
                    break;
                case WeaponSoundType.Equip:
                case WeaponSoundType.Holster:
                    // Add equip/holster sounds as needed
                    break;
            }
        }

        public void PlayImpact(SurfaceType surface, Vector3 position)
        {
            // TODO: Add impact sounds per surface type
            // For now, play a generic sound
        }

        public void PlayUISound(UISoundType type)
        {
            AudioClip clip = type switch
            {
                UISoundType.Click => uiClick,
                UISoundType.Hover => uiHover,
                UISoundType.Open => uiOpen,
                UISoundType.Close => uiClose,
                UISoundType.Error => uiError,
                UISoundType.Success => uiSuccess,
                _ => null
            };

            if (clip != null && uiSource != null)
            {
                uiSource.PlayOneShot(clip, masterVolume * sfxVolume);
            }
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            UpdateMusicVolume();
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            UpdateMusicVolume();
        }

        #endregion

        private void UpdateMusicVolume()
        {
            if (musicSource != null)
            {
                musicSource.volume = masterVolume * musicVolume;
            }
        }

        #region Public Helpers

        /// <summary>
        /// Play weapon fire sound with specific weapon type.
        /// </summary>
        public void PlayWeaponFire(WeaponType weaponType, Vector3 position)
        {
            AudioClip[] clips = weaponType switch
            {
                WeaponType.Rifle => rifleFire,
                WeaponType.Pistol => pistolFire,
                _ => rifleFire // Default to rifle
            };
            PlayRandomAtPosition(clips, position, 1f);
        }

        /// <summary>
        /// Play music track.
        /// </summary>
        public void PlayMusic(AudioClip track, bool loop = true)
        {
            if (musicSource == null || track == null) return;

            musicSource.clip = track;
            musicSource.loop = loop;
            musicSource.volume = masterVolume * musicVolume;
            musicSource.Play();
        }

        /// <summary>
        /// Stop music.
        /// </summary>
        public void StopMusic()
        {
            musicSource?.Stop();
        }

        /// <summary>
        /// Play hit marker sound (2D, non-positional).
        /// AAA Pattern: Hit confirmation is immediate player feedback, always 2D.
        /// </summary>
        public void PlayHitMarker(HitFeedbackType type)
        {
            AudioClip clip = type switch
            {
                HitFeedbackType.Kill => hitMarkerKill ?? hitMarkerHeadshot ?? hitMarkerNormal,
                HitFeedbackType.Headshot => hitMarkerHeadshot ?? hitMarkerNormal,
                _ => hitMarkerNormal
            };

            if (clip != null && uiSource != null)
            {
                uiSource.PlayOneShot(clip, masterVolume * sfxVolume * hitMarkerVolume);
            }
        }

        #endregion
    }
}
