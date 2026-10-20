using System;
using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Audio
{
    /// <summary>
    /// Prehistoric Sound and Music Audio Manager.
    /// Manages Master Volume, BGM, and SFX across PC and Mobile platforms.
    /// Supports real audio tracks for Opening, Tower Defense, Dino Assault, Common BGM,
    /// and distinctive dinosaur roars for each dino species.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class PrehistoricAudioManager : MonoBehaviour
    {
        private static PrehistoricAudioManager _instance;
        public static PrehistoricAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<PrehistoricAudioManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("PrehistoricAudioManager");
                        _instance = go.AddComponent<PrehistoricAudioManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

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
        private AudioSource _dinoSelectSource;
        private Coroutine _dinoSelectSoundRoutine;
        private Coroutine _bgmCoroutine;
        private Coroutine _dinoRoarsCoroutine;

        [Header("Real Audio Clips")]
        [SerializeField] private AudioClip _openingBgmClip;
        [SerializeField] private AudioClip _defenseBgmClip;
        [SerializeField] private AudioClip _assaultBgmClip;
        [SerializeField] private AudioClip _commonBgmClip;
        [SerializeField] private AudioClip _alternativeBgmClip;

        [Header("SFX Clips")]
        [SerializeField] private AudioClip _pterodactylClip;
        [SerializeField] private AudioClip _trexClip;
        [SerializeField] private AudioClip _raptorClip;
        [SerializeField] private AudioClip _ankylosaurusClip;
        [SerializeField] private AudioClip _victoryClip;
        [SerializeField] private AudioClip _defeatClip;
        [SerializeField] private AudioClip _hornClip;
        [SerializeField] private AudioClip _clickClip;
        [SerializeField] private AudioClip _placementClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioListenerExists();
            LoadPreferences();
            SetupAudioSources();
            LoadAllAudioClips();
            ApplyVolumes();
        }

        private void Start()
        {
            EnsureAudioListenerExists();
            // Tự động kích hoạt nhạc nền theo chế độ chơi hiện tại
            if (PrehistoricGameModeManager.Instance != null)
            {
                PlayBgmForMode(PrehistoricGameModeManager.Instance.CurrentMode);
            }
            else
            {
                PlayOpeningBgm();
            }
        }

        public void EnsureAudioListenerExists()
        {
            AudioListener listener = FindFirstObjectByType<AudioListener>();
            if (listener == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = FindFirstObjectByType<Camera>();
                if (cam != null)
                {
                    listener = cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log($"<color=cyan>[AudioManager]</color> Added AudioListener to camera: {cam.gameObject.name}");
                }
                else
                {
                    listener = gameObject.AddComponent<AudioListener>();
                    Debug.Log("<color=cyan>[AudioManager]</color> Added AudioListener to AudioManager GO");
                }
            }
            if (listener != null)
            {
                listener.enabled = true;
            }
            AudioListener.pause = false;
            AudioListener.volume = _MasterVolume;
        }

        private void LoadPreferences()
        {
            _MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 1.0f);
            if (_MasterVolume <= 0.05f) _MasterVolume = 1.0f;

            _BgmVolume = PlayerPrefs.GetFloat(PREF_BGM_VOL, 0.85f);
            if (_BgmVolume <= 0.05f) _BgmVolume = 0.85f;

            _SfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1.0f);
            if (_SfxVolume <= 0.05f) _SfxVolume = 1.0f;

            _IsBgmEnabled = PlayerPrefs.GetInt(PREF_BGM_ON, 1) == 1;
            _IsSfxEnabled = PlayerPrefs.GetInt(PREF_SFX_ON, 1) == 1;
        }

        private void SetupAudioSources()
        {
            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
                _bgmSource.spatialBlend = 0f; // 2D Stereo
                _bgmSource.mute = false;
                _bgmSource.bypassEffects = true;
                _bgmSource.bypassListenerEffects = true;
                _bgmSource.bypassReverbZones = true;
                _bgmSource.volume = _BgmVolume;
            }

            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f; // 2D Stereo
                _sfxSource.mute = false;
                _sfxSource.bypassEffects = true;
                _sfxSource.bypassListenerEffects = true;
                _sfxSource.bypassReverbZones = true;
                _sfxSource.volume = _SfxVolume;
            }
        }

        private void LoadAllAudioClips()
        {
            // 1. Nhạc nền (BGM)
            _openingBgmClip = LoadClip("Audio/nhac_mo_dau", "nhac_mo_dau");
            _defenseBgmClip = LoadClip("Audio/nhac_nen_cho_thu_thanh", "nhac_nen_cho_thu_thanh");
            _assaultBgmClip = LoadClip("Audio/nhac_nen_cho_cong_thanh", "nhac_nen_cho_cong_thanh");
            _commonBgmClip = LoadClip("Audio/nhac_nen_chung_cho_2_che_do", "nhac_nen_chung_cho_2_che_do");
            _alternativeBgmClip = LoadClip("Audio/nhac_nen_2", "nhac_nen_2");

            // 2. Âm thanh tiếng kêu / gầm khủng long (Dino SFX)
            _pterodactylClip = LoadClip("Audio/dragon-studio-pterodactyl-call-382716", "dragon-studio-pterodactyl-call-382716");
            _trexClip = LoadClip("Audio/dffdv-dinosaur-roar-with-screams-and-growls-193210", "dffdv-dinosaur-roar-with-screams-and-growls-193210");
            _raptorClip = LoadClip("Audio/freesound_community-dinosaur-99810", "freesound_community-dinosaur-99810");
            _ankylosaurusClip = _trexClip != null ? _trexClip : _raptorClip;

            // 3. Âm thanh thắng / thua / giao diện
            _victoryClip = LoadClip("Audio/floraphonic-you-win-sequence-2-183949", "floraphonic-you-win-sequence-2-183949");
            _defeatClip = LoadClip("Audio/u_l5xum8z250-losing-horn-313723", "u_l5xum8z250-losing-horn-313723");

            // Tạo các clip bổ trợ nếu thiếu
            GenerateFallbackClipsIfNull();
        }

        private AudioClip LoadClip(string resourcePath, string fileBaseName)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null && !resourcePath.StartsWith("Audio/"))
            {
                clip = Resources.Load<AudioClip>("Audio/" + resourcePath);
            }

#if UNITY_EDITOR
            if (clip == null && !string.IsNullOrEmpty(fileBaseName))
            {
                string[] candidates = new string[]
                {
                    $"Assets/Resources/Audio/{fileBaseName}.mp3",
                    $"Assets/Audio/{fileBaseName}.mp3"
                };
                foreach (string p in candidates)
                {
                    clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                    if (clip != null) break;
                }
            }
#endif
            if (clip != null)
            {
                if (clip.loadState != AudioDataLoadState.Loaded)
                {
                    clip.LoadAudioData();
                }
                Debug.Log($"<color=lime>[AudioManager]</color> Loaded clip: {clip.name} ({clip.length:F1}s)");
            }
            else
            {
                Debug.LogWarning($"<color=red>[AudioManager]</color> Could not find clip: {resourcePath} ({fileBaseName})");
            }
            return clip;
        }

        private void GenerateFallbackClipsIfNull()
        {
            const int sampleRate = 44100;
            if (_clickClip == null)
            {
                _clickClip = GenerateTone(sampleRate, 0.05f, t => Mathf.Sin(2f * Mathf.PI * 880f * t) * Mathf.Exp(-t * 80f) * 0.5f);
            }
            if (_placementClip == null)
            {
                _placementClip = GenerateTone(sampleRate, 0.22f, t => Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(160f, 45f, t / 0.22f) * t) * Mathf.Exp(-t * 22f) * 0.8f);
            }
            if (_hornClip == null)
            {
                _hornClip = GenerateTone(sampleRate, 1.2f, t =>
                {
                    float env = Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI);
                    return (Mathf.Sin(2f * Mathf.PI * (220f + Mathf.Sin(t * 12f) * 8f) * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.4f) * env * 0.85f;
                });
            }
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

        // ==========================================
        // BGM PLAYBACK API
        // ==========================================

        public void PlayOpeningBgm()
        {
            if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);
            if (_openingBgmClip != null)
            {
                PlayClipOnSource(_openingBgmClip, true);
            }
        }

        /// <summary>
        /// Phát nhạc nền theo chế độ chơi:
        /// - Thủ thành: Phát nhạc thủ thành, khi hết bài thì tự động chuyển sang phát lặp lại nhạc nền chung.
        /// - Công thành: Phát nhạc công thành, khi hết bài thì tự động chuyển sang phát lặp lại nhạc nền chung.
        /// </summary>
        public void PlayBgmForMode(PrehistoricGameMode mode)
        {
            if (_bgmCoroutine != null) StopCoroutine(_bgmCoroutine);

            AudioClip modeClip = (mode == PrehistoricGameMode.TowerDefense) ? _defenseBgmClip : _assaultBgmClip;
            AudioClip commonClip = _commonBgmClip != null ? _commonBgmClip : _alternativeBgmClip;

            _bgmCoroutine = StartCoroutine(PlayModeThenCommonRoutine(modeClip, commonClip));
        }

        private IEnumerator PlayModeThenCommonRoutine(AudioClip modeClip, AudioClip commonClip)
        {
            if (!_IsBgmEnabled) yield break;

            if (modeClip != null)
            {
                PlayClipOnSource(modeClip, false);

                // Chờ ít nhất 0.25s để AudioSource bắt đầu phát và cập nhật trạng thái isPlaying
                yield return new WaitForSecondsRealtime(0.25f);

                // Chờ cho đến khi bài nhạc riêng của chế độ phát hết
                while (_bgmSource != null && _bgmSource.isPlaying && _bgmSource.time < (modeClip.length - 0.5f))
                {
                    yield return null;
                }
            }

            // Sau khi hết bài của chế độ, phát tiếp tục và loop nhạc nền chung
            if (commonClip != null && _IsBgmEnabled)
            {
                PlayClipOnSource(commonClip, true);
            }
        }

        private void PlayClipOnSource(AudioClip clip, bool loop)
        {
            if (_bgmSource == null || clip == null) return;
            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }
            _bgmSource.clip = clip;
            _bgmSource.loop = loop;
            _bgmSource.time = 0f;
            _bgmSource.volume = _IsBgmEnabled ? _BgmVolume : 0f;
            if (_IsBgmEnabled)
            {
                _bgmSource.Play();
                Debug.Log($"<color=green>[AudioManager]</color> Playing BGM: {clip.name} (Loop: {loop}, Vol: {_bgmSource.volume:F2})");
            }
        }

        public void StopBGM()
        {
            if (_bgmCoroutine != null)
            {
                StopCoroutine(_bgmCoroutine);
                _bgmCoroutine = null;
            }
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Stop();
            }
        }

        // ==========================================
        // DINO SFX API
        // ==========================================

        /// <summary>
        /// Phát tiếng gầm của loài khủng long tương ứng khi người chơi chọn trong chế độ công thành
        /// </summary>
        public void PlayDinoSound(DinoSO dino)
        {
            if (dino == null) return;
            PlayDinoSound(dino.name);
        }

        /// <summary>
        /// Phát tiếng gầm của loài khủng long theo tên loài
        /// </summary>
        public void PlayDinoSound(string dinoName)
        {
            if (!_IsSfxEnabled || _sfxSource == null || string.IsNullOrEmpty(dinoName)) return;

            string lower = dinoName.ToLower();
            AudioClip clipToPlay = null;

            if (lower.Contains("ptero"))
            {
                clipToPlay = _pterodactylClip;
            }
            else if (lower.Contains("trex") || lower.Contains("t-rex") || lower.Contains("boss"))
            {
                clipToPlay = _trexClip;
            }
            else if (lower.Contains("anky") || lower.Contains("siege"))
            {
                clipToPlay = _ankylosaurusClip != null ? _ankylosaurusClip : _trexClip;
            }
            else if (lower.Contains("velo") || lower.Contains("raptor") || lower.Contains("runner") || lower.Contains("speed"))
            {
                clipToPlay = _raptorClip;
            }
            else
            {
                clipToPlay = _raptorClip != null ? _raptorClip : _trexClip;
            }

            if (clipToPlay != null)
            {
                if (clipToPlay.loadState != AudioDataLoadState.Loaded)
                {
                    clipToPlay.LoadAudioData();
                }
                if (_dinoSelectSource == null)
                {
                    _dinoSelectSource = gameObject.AddComponent<AudioSource>();
                    _dinoSelectSource.playOnAwake = false;
                    _dinoSelectSource.loop = false;
                }
                if (_dinoSelectSoundRoutine != null)
                {
                    StopCoroutine(_dinoSelectSoundRoutine);
                    _dinoSelectSoundRoutine = null;
                }
                _dinoSelectSource.Stop();
                _dinoSelectSource.clip = clipToPlay;
                _dinoSelectSource.volume = _SfxVolume * 0.90f;
                _dinoSelectSource.Play();
                Debug.Log($"<color=orange>[AudioManager]</color> Played Dino Sound for '{dinoName}' (full duration): {clipToPlay.name}");
            }
        }

        /// <summary>
        /// Khi bắt đầu một Wave trong chế độ Thủ thành, phát âm thanh lần lượt của từng loài khủng long xuất hiện
        /// </summary>
        private IEnumerator PlayDinoSoundShortRoutine(AudioClip clip, float duration)
        {
            if (_dinoSelectSource == null || clip == null) yield break;

            _dinoSelectSource.Stop();
            _dinoSelectSource.clip = clip;
            _dinoSelectSource.volume = _SfxVolume * 0.85f;
            _dinoSelectSource.time = 0f;
            _dinoSelectSource.Play();

            float elapsed = 0f;
            while (elapsed < duration && _dinoSelectSource.isPlaying)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Dập tắt dứt khoát sau đúng 0.2s để tránh ô nhiễm âm thanh
            _dinoSelectSource.Stop();
            _dinoSelectSoundRoutine = null;
        }

        public void PlayWaveDinoRoars(IEnumerable<string> dinoTypesInWave)
        {
            if (!_IsSfxEnabled) return;
            if (_dinoRoarsCoroutine != null) StopCoroutine(_dinoRoarsCoroutine);
            _dinoRoarsCoroutine = StartCoroutine(WaveDinoRoarsRoutine(dinoTypesInWave));
        }

        private IEnumerator WaveDinoRoarsRoutine(IEnumerable<string> dinoTypesInWave)
        {
            if (dinoTypesInWave == null) yield break;

            HashSet<string> playedCategories = new HashSet<string>();

            // Nghỉ nhẹ 0.4s sau tiếng tù và
            yield return new WaitForSeconds(0.4f);

            foreach (string dinoName in dinoTypesInWave)
            {
                if (string.IsNullOrEmpty(dinoName)) continue;
                string lower = dinoName.ToLower();

                string category = "other";
                if (lower.Contains("ptero")) category = "ptero";
                else if (lower.Contains("trex") || lower.Contains("t-rex")) category = "trex";
                else if (lower.Contains("anky")) category = "anky";
                else if (lower.Contains("velo") || lower.Contains("raptor")) category = "raptor";

                if (!playedCategories.Contains(category))
                {
                    playedCategories.Add(category);
                    PlayDinoSound(dinoName);
                    yield return new WaitForSeconds(0.65f);
                }
            }
        }

        // ==========================================
        // OTHER SFX PLAYBACK
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
            if (!_IsSfxEnabled || _sfxSource == null) return;
            AudioClip clip = _hornClip != null ? _hornClip : _defeatClip;
            if (clip != null)
            {
                _sfxSource.PlayOneShot(clip, _SfxVolume * 1.0f);
            }
        }

        public void PlayVictory()
        {
            if (!_IsSfxEnabled || _sfxSource == null) return;
            AudioClip clip = _victoryClip != null ? _victoryClip : _clickClip;
            if (clip != null)
            {
                _sfxSource.PlayOneShot(clip, _SfxVolume * 1.0f);
            }
        }

        public void PlayGameOver()
        {
            if (!_IsSfxEnabled || _sfxSource == null) return;
            AudioClip clip = _defeatClip != null ? _defeatClip : _clickClip;
            if (clip != null)
            {
                _sfxSource.PlayOneShot(clip, _SfxVolume * 1.0f);
            }
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
    }
}
