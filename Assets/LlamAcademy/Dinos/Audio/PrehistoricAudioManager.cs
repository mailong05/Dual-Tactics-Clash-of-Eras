using System;
using UnityEngine;

namespace LlamAcademy.Dinos.Audio
{
    /// <summary>
    /// Prehistoric Sound and Music Audio Manager.
    /// Manages Master Volume, BGM, and SFX across PC and Mobile platforms.
    /// Features procedural atmospheric music and sound synthesis with zero external asset dependencies.
    /// Saves and restores preferences via PlayerPrefs.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class PrehistoricAudioManager : MonoBehaviour
    {
        public static PrehistoricAudioManager Instance { get; private set; }

        private const string PREF_MASTER_VOL = "Prehistoric_MasterVol";
        private const string PREF_BGM_VOL = "Prehistoric_BgmVol";
        private const string PREF_SFX_VOL = "Prehistoric_SfxVol";
        private const string PREF_BGM_ON = "Prehistoric_BgmOn";
        private const string PREF_SFX_ON = "Prehistoric_SfxOn";

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float _MasterVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float _BgmVolume = 0.70f;
        [Range(0f, 1f)] [SerializeField] private float _SfxVolume = 0.90f;
        [SerializeField] private bool _IsBgmEnabled = true;
        [SerializeField] private bool _IsSfxEnabled = true;

        public float MasterVolume
        {
            get => _MasterVolume;
            set
            {
                _MasterVolume = Mathf.Clamp01(value);
                ApplyVolumes();
                PlayerPrefs.SetFloat(PREF_MASTER_VOL, _MasterVolume);
            }
        }

        public float BgmVolume
        {
            get => _BgmVolume;
            set
            {
                _BgmVolume = Mathf.Clamp01(value);
                ApplyVolumes();
                PlayerPrefs.SetFloat(PREF_BGM_VOL, _BgmVolume);
            }
        }

        public float SfxVolume
        {
            get => _SfxVolume;
            set
            {
                _SfxVolume = Mathf.Clamp01(value);
                ApplyVolumes();
                PlayerPrefs.SetFloat(PREF_SFX_VOL, _SfxVolume);
            }
        }

        public bool IsBgmEnabled
        {
            get => _IsBgmEnabled;
            set
            {
                _IsBgmEnabled = value;
                ApplyVolumes();
                PlayerPrefs.SetInt(PREF_BGM_ON, _IsBgmEnabled ? 1 : 0);
            }
        }

        public bool IsSfxEnabled
        {
            get => _IsSfxEnabled;
            set
            {
                _IsSfxEnabled = value;
                ApplyVolumes();
                PlayerPrefs.SetInt(PREF_SFX_ON, _IsSfxEnabled ? 1 : 0);
            }
        }

        private AudioSource _bgmSource;
        private AudioSource _sfxSource;

        private AudioClip _bgmClip;
        private AudioClip _clickClip;
        private AudioClip _placementClip;
        private AudioClip _hornClip;
        private AudioClip _victoryClip;
        private AudioClip _defeatClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadPreferences();
            SetupAudioSources();
            GenerateProceduralAudioClips();
            ApplyVolumes();
        }

        private void Start()
        {
            StartBGM();
        }

        private void LoadPreferences()
        {
            _MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
            _BgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, 0.70f);
            _SfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.90f);
            _IsBgmEnabled = PlayerPrefs.GetInt(PREF_BGM_ON, 1) == 1;
            _IsSfxEnabled = PlayerPrefs.GetInt(PREF_SFX_ON, 1) == 1;
        }

        private void SetupAudioSources()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f; // 2D Stereo

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = _MasterVolume;

            if (_bgmSource != null)
            {
                _bgmSource.volume = _IsBgmEnabled ? _BgmVolume : 0f;
            }

            if (_sfxSource != null)
            {
                _sfxSource.volume = _IsSfxEnabled ? _SfxVolume : 0f;
            }
        }

        public void StartBGM()
        {
            if (_bgmSource != null && _bgmClip != null && !_bgmSource.isPlaying)
            {
                _bgmSource.clip = _bgmClip;
                _bgmSource.Play();
            }
        }

        public void StopBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
            }
        }

        // ==========================================
        // SFX Playback API
        // ==========================================

        public void PlayButtonClick()
        {
            if (!_IsSfxEnabled || _sfxSource == null || _clickClip == null) return;
            _sfxSource.PlayOneShot(_clickClip, _SfxVolume * 0.75f);
        }

        public void PlayTowerPlaced()
        {
            if (!_IsSfxEnabled || _sfxSource == null || _placementClip == null) return;
            _sfxSource.PlayOneShot(_placementClip, _SfxVolume * 1.0f);
        }

        public void PlayWaveHorn()
        {
            if (!_IsSfxEnabled || _sfxSource == null || _hornClip == null) return;
            _sfxSource.PlayOneShot(_hornClip, _SfxVolume * 1.0f);
        }

        public void PlayVictory()
        {
            if (!_IsSfxEnabled || _sfxSource == null || _victoryClip == null) return;
            _sfxSource.PlayOneShot(_victoryClip, _SfxVolume * 1.0f);
        }

        public void PlayGameOver()
        {
            if (!_IsSfxEnabled || _sfxSource == null || _defeatClip == null) return;
            _sfxSource.PlayOneShot(_defeatClip, _SfxVolume * 1.0f);
        }

        // ==========================================
        // Procedural Audio Synthesis
        // ==========================================

        private void GenerateProceduralAudioClips()
        {
            const int sampleRate = 44100;

            // 1. Procedural BGM: 8-second seamless prehistoric rhythmic ambient drone
            _bgmClip = GeneratePrehistoricAmbientBGM(sampleRate, 8.0f);

            // 2. Procedural UI Click (light wooden tap)
            _clickClip = GenerateTone(sampleRate, 0.05f, t =>
            {
                float decay = Mathf.Exp(-t * 80f);
                return Mathf.Sin(2f * Mathf.PI * 880f * t) * decay * 0.5f;
            });

            // 3. Procedural Tower Placement (heavy stone thud)
            _placementClip = GenerateTone(sampleRate, 0.22f, t =>
            {
                float decay = Mathf.Exp(-t * 22f);
                float f = Mathf.Lerp(160f, 45f, t / 0.22f);
                return Mathf.Sin(2f * Mathf.PI * f * t) * decay * 0.8f;
            });

            // 4. Procedural Tribal War Horn (Wave start alert)
            _hornClip = GenerateTone(sampleRate, 1.2f, t =>
            {
                float env = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
                float f1 = 220f + Mathf.Sin(t * 12f) * 8f;
                float f2 = 330f;
                return (Mathf.Sin(2f * Mathf.PI * f1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.4f) * env * 0.85f;
            });

            // 5. Procedural Victory Fanfare
            _victoryClip = GenerateTone(sampleRate, 1.5f, t =>
            {
                float env = Mathf.Exp(-t * 2.5f);
                float freq = t < 0.35f ? 440f : (t < 0.7f ? 554f : 659f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.7f;
            });

            // 6. Procedural Defeat Gong
            _defeatClip = GenerateTone(sampleRate, 1.8f, t =>
            {
                float env = Mathf.Exp(-t * 2.2f);
                float f = Mathf.Lerp(110f, 65f, t / 1.8f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) + Mathf.Sin(2f * Mathf.PI * (f * 1.48f) * t) * 0.5f) * env * 0.85f;
            });
        }

        private static AudioClip GenerateTone(int sampleRate, float duration, Func<float, float> waveFunc)
        {
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                samples[i] = Mathf.Clamp(waveFunc(t), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Synthesized_SFX", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GeneratePrehistoricAmbientBGM(int sampleRate, float duration)
        {
            int totalSamples = (int)(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Deep ambient drone (D2 = 73.4Hz, A2 = 110Hz)
                float drone = (Mathf.Sin(2f * Mathf.PI * 73.4f * t) * 0.45f) +
                              (Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.25f);

                // Subtle tribal drum pulse every 1 second
                float pulseTime = t % 1.0f;
                float drumPulse = Mathf.Sin(2f * Mathf.PI * 55f * pulseTime) * Mathf.Exp(-pulseTime * 14f) * 0.45f;

                // Atmospheric warm harmonics
                float overtone = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.08f * (0.8f + 0.2f * Mathf.Sin(t * 1.5f));

                // Soft fade in/out near loop boundary for zero-click seamless looping
                float loopFade = 1f;
                if (t < 0.05f) loopFade = t / 0.05f;
                else if (t > duration - 0.05f) loopFade = (duration - t) / 0.05f;

                samples[i] = Mathf.Clamp((drone + drumPulse + overtone) * loopFade * 0.55f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Prehistoric_BGM_Loop", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
