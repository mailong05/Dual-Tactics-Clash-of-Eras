using System;
using System.IO;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Cloud;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Save
{
    [Serializable]
    public class PrehistoricSaveData
    {
        public int CurrentWave = 1;
        public int HighestWaveReached = 1;
        public int Gold = 500;
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public int TotalMonstersKilled = 0;
        public int TotalScore = 0;
        public string PlayerChiefName = "Tù Trưởng Tiền Sử";
        public string LastSavedTimestamp = "";
        public bool HasActiveRun = false;

        // Audio Settings Backup
        public float MasterVolume = 0.85f;
        public float BgmVolume = 0.70f;
        public float SfxVolume = 0.90f;
        public bool IsBgmEnabled = true;
        public bool IsSfxEnabled = true;
    }

    /// <summary>
    /// Game Progression Save System.
    /// Hybrid Architecture:
    /// - 100% Offline Local Auto-Save (JSON via Application.persistentDataPath).
    /// - Optional Cloud Synchronization (IOCloud Leaderboard submission).
    /// - Auto-saves on Wave Clear, Game Over, Pause, and Exit.
    /// </summary>
    [DefaultExecutionOrder(-15)]
    public class PrehistoricSaveManager : MonoBehaviour
    {
        public static PrehistoricSaveManager Instance { get; private set; }

        private const string SAVE_FILE_NAME = "PrehistoricGameSave.json";
        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        public PrehistoricSaveData CurrentData { get; private set; } = new();

        public event Action<PrehistoricSaveData> OnGameSaved;
        public event Action<PrehistoricSaveData> OnGameLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSaveData();
        }

        public bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public PrehistoricSaveData LoadSaveData()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    string json = File.ReadAllText(SaveFilePath);
                    CurrentData = JsonUtility.FromJson<PrehistoricSaveData>(json);
                    if (CurrentData == null) CurrentData = new PrehistoricSaveData();
                    Debug.Log($"<color=green>[SaveSystem]</color> Loaded Save: Wave {CurrentData.CurrentWave}, HighScore {CurrentData.TotalScore}, Gold {CurrentData.Gold}g");
                    OnGameLoaded?.Invoke(CurrentData);
                    return CurrentData;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error reading save file: {ex.Message}. Initializing fresh data.");
            }

            CurrentData = new PrehistoricSaveData();
            return CurrentData;
        }

        public void AutoSaveCurrentRun(bool hasActiveRun = true)
        {
            if (PrehistoricGameplayManager.Instance != null)
            {
                int wave = PrehistoricGameplayManager.Instance.CurrentWave;
                int baseHp = PrehistoricGameplayManager.Instance.CurrentBaseHealth;
                int maxHp = PrehistoricGameplayManager.Instance.MaxBaseHealth;
                int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 500;

                CurrentData.CurrentWave = wave;
                CurrentData.HighestWaveReached = Mathf.Max(CurrentData.HighestWaveReached, wave);
                CurrentData.BaseHealth = baseHp;
                CurrentData.MaxBaseHealth = maxHp;
                CurrentData.Gold = gold;
                CurrentData.HasActiveRun = hasActiveRun;
                CurrentData.TotalScore = (CurrentData.HighestWaveReached * 1000) + (CurrentData.TotalMonstersKilled * 25) + gold;
            }

            if (PrehistoricAudioManager.Instance != null)
            {
                CurrentData.MasterVolume = PrehistoricAudioManager.Instance.MasterVolume;
                CurrentData.BgmVolume = PrehistoricAudioManager.Instance.BgmVolume;
                CurrentData.SfxVolume = PrehistoricAudioManager.Instance.SfxVolume;
                CurrentData.IsBgmEnabled = PrehistoricAudioManager.Instance.IsBgmEnabled;
                CurrentData.IsSfxEnabled = PrehistoricAudioManager.Instance.IsSfxEnabled;
            }

            CurrentData.LastSavedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                string json = JsonUtility.ToJson(CurrentData, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"<color=green>[SaveSystem]</color> Auto-Saved successfully to: {SaveFilePath}");
                OnGameSaved?.Invoke(CurrentData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to write save file: {ex.Message}");
            }
        }

        public void RecordMonsterKilled()
        {
            CurrentData.TotalMonstersKilled++;
            CurrentData.TotalScore += 25;
        }

        public void SetPlayerChiefName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            CurrentData.PlayerChiefName = newName.Trim();
            AutoSaveCurrentRun(CurrentData.HasActiveRun);
        }

        public void ResetSaveData()
        {
            try
            {
                if (File.Exists(SaveFilePath))
                {
                    File.Delete(SaveFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error deleting save file: {ex.Message}");
            }

            CurrentData = new PrehistoricSaveData();
            Debug.Log("<color=orange>[SaveSystem]</color> Save progress has been completely reset.");
            OnGameSaved?.Invoke(CurrentData);
        }

        public void SyncToCloud(Action<bool, string> onComplete)
        {
            if (IOCloudManager.Instance != null && IOCloudManager.Instance.IsCloudConnected)
            {
                IOCloudManager.Instance.SubmitScoreToCloud(CurrentData.PlayerChiefName, CurrentData.TotalScore, CurrentData.HighestWaveReached);
                onComplete?.Invoke(true, $"Đã đồng bộ thành công hồ sơ [{CurrentData.PlayerChiefName}] ({CurrentData.TotalScore} điểm) lên IO Cloud!");
            }
            else
            {
                // Standby local notification
                onComplete?.Invoke(false, $"Cloud backend offline. Điểm kỷ lục ({CurrentData.TotalScore} điểm) đã được lưu an toàn trên máy.");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                AutoSaveCurrentRun(CurrentData.HasActiveRun);
            }
        }

        private void OnApplicationQuit()
        {
            AutoSaveCurrentRun(CurrentData.HasActiveRun);
        }
    }
}
