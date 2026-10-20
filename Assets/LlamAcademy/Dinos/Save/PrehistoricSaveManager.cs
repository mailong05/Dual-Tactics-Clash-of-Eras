using System;
using System.IO;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Cloud;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.Save
{
    [Serializable]
    public class DefenseSaveProfile
    {
        public int CurrentWave = 1;
        public int HighestWaveReached = 1;
        public int Gold = 500;
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public int TotalMonstersKilled = 0;
        public int TotalScore = 0;
        public bool HasActiveRun = false;
        public string LastSavedTimestamp = "";
    }

    [Serializable]
    public class AssaultSaveProfile
    {
        public int AssaultLevel = 1;
        public int HighestLevelReached = 1;
        public int Meat = 250;
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public bool HasActiveRun = false;
        public string LastSavedTimestamp = "";
    }


    [Serializable]
    public class PrehistoricSaveData
    {
        // 2 Bản lưu độc lập hoàn toàn cho 2 chế độ chơi
        public DefenseSaveProfile DefenseProfile = new DefenseSaveProfile();
        public AssaultSaveProfile AssaultProfile = new AssaultSaveProfile();

        public PrehistoricGameMode LastPlayedMode = PrehistoricGameMode.TowerDefense;
        public string PlayerChiefName = "Tù Trưởng Tiền Sử";

        // Backwards compatibility shortcuts cho code cũ
        public int CurrentWave
        {
            get => DefenseProfile != null ? DefenseProfile.CurrentWave : 1;
            set { if (DefenseProfile != null) DefenseProfile.CurrentWave = value; }
        }
        public int HighestWaveReached
        {
            get => DefenseProfile != null ? DefenseProfile.HighestWaveReached : 1;
            set { if (DefenseProfile != null) DefenseProfile.HighestWaveReached = value; }
        }
        public int Gold
        {
            get => DefenseProfile != null ? DefenseProfile.Gold : 500;
            set { if (DefenseProfile != null) DefenseProfile.Gold = value; }
        }
        public int BaseHealth
        {
            get => DefenseProfile != null ? DefenseProfile.BaseHealth : 1000;
            set { if (DefenseProfile != null) DefenseProfile.BaseHealth = value; }
        }
        public int MaxBaseHealth
        {
            get => DefenseProfile != null ? DefenseProfile.MaxBaseHealth : 1000;
            set { if (DefenseProfile != null) DefenseProfile.MaxBaseHealth = value; }
        }
        public int TotalScore
        {
            get => DefenseProfile != null ? DefenseProfile.TotalScore : 0;
            set { if (DefenseProfile != null) DefenseProfile.TotalScore = value; }
        }
        public int TotalMonstersKilled
        {
            get => DefenseProfile != null ? DefenseProfile.TotalMonstersKilled : 0;
            set { if (DefenseProfile != null) DefenseProfile.TotalMonstersKilled = value; }
        }
        public bool HasActiveRun
        {
            get => DefenseProfile != null && DefenseProfile.HasActiveRun;
            set { if (DefenseProfile != null) DefenseProfile.HasActiveRun = value; }
        }

        public string LastSavedTimestamp
        {
            get
            {
                if (LastPlayedMode == PrehistoricGameMode.DinoAssault && AssaultProfile != null)
                    return AssaultProfile.LastSavedTimestamp;
                return DefenseProfile != null ? DefenseProfile.LastSavedTimestamp : "";
            }
            set
            {
                if (DefenseProfile != null) DefenseProfile.LastSavedTimestamp = value;
                if (AssaultProfile != null) AssaultProfile.LastSavedTimestamp = value;
            }
        }

        // Audio Settings Backup
        public float MasterVolume = 0.85f;
        public float BgmVolume = 0.70f;
        public float SfxVolume = 0.90f;
        public bool IsBgmEnabled = true;
        public bool IsSfxEnabled = true;
    }

    [DefaultExecutionOrder(-15)]
    public class PrehistoricSaveManager : MonoBehaviour
    {
        public static PrehistoricSaveManager Instance { get; private set; }

        private const string SAVE_FILE_NAME = "PrehistoricGameSave_v2.json";
        private const string LEGACY_SAVE_FILE_NAME = "PrehistoricGameSave.json";

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        private string LegacySaveFilePath => Path.Combine(Application.persistentDataPath, LEGACY_SAVE_FILE_NAME);

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
            return File.Exists(SaveFilePath) || File.Exists(LegacySaveFilePath);
        }

        public bool HasActiveDefenseSave()
        {
            return CurrentData != null && CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.HasActiveRun;
        }

        public bool HasActiveAssaultSave()
        {
            return CurrentData != null && CurrentData.AssaultProfile != null && CurrentData.AssaultProfile.HasActiveRun;
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
                    EnsureProfilesInitialized();
                    Debug.Log($"<color=green>[SaveSystem]</color> Loaded Save v2: Defense Wave {CurrentData.DefenseProfile.CurrentWave}, Assault Lvl {CurrentData.AssaultProfile.AssaultLevel}");
                    OnGameLoaded?.Invoke(CurrentData);
                    return CurrentData;
                }
                else if (File.Exists(LegacySaveFilePath))
                {
                    // Migrate legacy single-save file
                    string json = File.ReadAllText(LegacySaveFilePath);
                    CurrentData = JsonUtility.FromJson<PrehistoricSaveData>(json);
                    if (CurrentData == null) CurrentData = new PrehistoricSaveData();
                    EnsureProfilesInitialized();
                    Debug.Log("<color=green>[SaveSystem]</color> Migrated legacy save file to multi-mode v2.");
                    AutoSaveCurrentRun(CurrentData.DefenseProfile.HasActiveRun);
                    OnGameLoaded?.Invoke(CurrentData);
                    return CurrentData;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error reading save file: {ex.Message}. Initializing fresh data.");
            }

            CurrentData = new PrehistoricSaveData();
            EnsureProfilesInitialized();
            return CurrentData;
        }

        private void EnsureProfilesInitialized()
        {
            if (CurrentData.DefenseProfile == null) CurrentData.DefenseProfile = new DefenseSaveProfile();
            if (CurrentData.AssaultProfile == null) CurrentData.AssaultProfile = new AssaultSaveProfile();
        }

        public void AutoSaveCurrentRun(bool hasActiveRun = true)
        {
            PrehistoricGameMode currentMode = PrehistoricGameMode.TowerDefense;
            if (PrehistoricGameModeManager.Instance != null)
            {
                currentMode = PrehistoricGameModeManager.Instance.CurrentMode;
            }

            CurrentData.LastPlayedMode = currentMode;

            if (currentMode == PrehistoricGameMode.TowerDefense)
            {
                AutoSaveDefenseRun(hasActiveRun);
            }
            else
            {
                AutoSaveAssaultRun(hasActiveRun);
            }
        }

        public void AutoSaveDefenseRun(bool hasActiveRun = true)
        {
            EnsureProfilesInitialized();

            if (PrehistoricGameplayManager.Instance != null)
            {
                int wave = PrehistoricGameplayManager.Instance.CurrentWave;
                int baseHp = PrehistoricGameplayManager.Instance.CurrentBaseHealth;
                int maxHp = PrehistoricGameplayManager.Instance.MaxBaseHealth;
                int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 500;

                CurrentData.DefenseProfile.CurrentWave = wave;
                CurrentData.DefenseProfile.HighestWaveReached = Mathf.Max(CurrentData.DefenseProfile.HighestWaveReached, wave);
                CurrentData.DefenseProfile.BaseHealth = baseHp;
                CurrentData.DefenseProfile.MaxBaseHealth = maxHp;
                CurrentData.DefenseProfile.Gold = gold;
                CurrentData.DefenseProfile.HasActiveRun = hasActiveRun;
                CurrentData.DefenseProfile.TotalScore = (CurrentData.DefenseProfile.HighestWaveReached * 1000) + (CurrentData.DefenseProfile.TotalMonstersKilled * 25) + gold;
            }

            SaveDataToDisk();
            Debug.Log($"<color=green>[SaveSystem]</color> Auto-Saved Defense Run: Wave {CurrentData.DefenseProfile.CurrentWave}, Gold {CurrentData.DefenseProfile.Gold}g, Active: {hasActiveRun}");
        }

        public void AutoSaveAssaultRun(bool hasActiveRun = true)
        {
            EnsureProfilesInitialized();

            if (PrehistoricGameModeManager.Instance != null)
            {
                int level = PrehistoricGameModeManager.Instance.AssaultLevel;
                int meat = 250;
                DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindFirstObjectByType<DinoSpawner>();
                if (spawner != null) meat = spawner.ResourcesToSpend;

                int baseHp = 1000;
                int maxHp = 1000;
                if (PrehistoricVillageBase.Instance != null)
                {
                    baseHp = PrehistoricVillageBase.Instance.Health;
                    maxHp = PrehistoricVillageBase.Instance.MaxHealth;
                }

                CurrentData.AssaultProfile.AssaultLevel = level;
                CurrentData.AssaultProfile.HighestLevelReached = Mathf.Max(CurrentData.AssaultProfile.HighestLevelReached, level);
                CurrentData.AssaultProfile.Meat = meat;
                CurrentData.AssaultProfile.BaseHealth = baseHp;
                CurrentData.AssaultProfile.MaxBaseHealth = maxHp;
                CurrentData.AssaultProfile.HasActiveRun = hasActiveRun;
            }

            SaveDataToDisk();
            Debug.Log($"<color=green>[SaveSystem]</color> Auto-Saved Assault Run: Level {CurrentData.AssaultProfile.AssaultLevel}, Meat {CurrentData.AssaultProfile.Meat}, Active: {hasActiveRun}");
        }

        private void SaveDataToDisk()
        {
            if (PrehistoricAudioManager.Instance != null)
            {
                CurrentData.MasterVolume = PrehistoricAudioManager.Instance.MasterVolume;
                CurrentData.BgmVolume = PrehistoricAudioManager.Instance.BgmVolume;
                CurrentData.SfxVolume = PrehistoricAudioManager.Instance.SfxVolume;
                CurrentData.IsBgmEnabled = PrehistoricAudioManager.Instance.IsBgmEnabled;
                CurrentData.IsSfxEnabled = PrehistoricAudioManager.Instance.IsSfxEnabled;
            }

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            if (CurrentData.DefenseProfile != null) CurrentData.DefenseProfile.LastSavedTimestamp = timestamp;
            if (CurrentData.AssaultProfile != null) CurrentData.AssaultProfile.LastSavedTimestamp = timestamp;

            try
            {
                string json = JsonUtility.ToJson(CurrentData, true);
                File.WriteAllText(SaveFilePath, json);
                OnGameSaved?.Invoke(CurrentData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Failed to write save file: {ex.Message}");
            }
        }

        public void RecordMonsterKilled()
        {
            EnsureProfilesInitialized();
            CurrentData.DefenseProfile.TotalMonstersKilled++;
            CurrentData.DefenseProfile.TotalScore += 25;
        }

        public void SetPlayerChiefName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            CurrentData.PlayerChiefName = newName.Trim();
            AutoSaveCurrentRun(CurrentData.HasActiveRun);
        }

        public void ResetDefenseSave()
        {
            EnsureProfilesInitialized();
            CurrentData.DefenseProfile = new DefenseSaveProfile();
            SaveDataToDisk();
            Debug.Log("<color=orange>[SaveSystem]</color> Reset Defense save profile.");
        }

        public void ResetAssaultSave()
        {
            EnsureProfilesInitialized();
            CurrentData.AssaultProfile = new AssaultSaveProfile();
            SaveDataToDisk();
            Debug.Log("<color=orange>[SaveSystem]</color> Reset Assault save profile.");
        }

        public void ResetSaveData()
        {
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
                if (File.Exists(LegacySaveFilePath)) File.Delete(LegacySaveFilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error deleting save file: {ex.Message}");
            }

            CurrentData = new PrehistoricSaveData();
            Debug.Log("<color=orange>[SaveSystem]</color> All save data has been completely reset.");
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
                onComplete?.Invoke(false, $"Cloud backend offline. Điểm kỷ lục ({CurrentData.TotalScore} điểm) đã được lưu an toàn trên máy.");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                AutoSaveCurrentRun(true);
            }
        }

        private void OnApplicationQuit()
        {
            AutoSaveCurrentRun(true);
        }
    }
}
