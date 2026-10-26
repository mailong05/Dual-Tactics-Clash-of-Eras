using System;
using System.IO;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Cloud;
using LlamAcademy.Dinos.Environment;
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
        public int MapIndex = 0; // 0 = AncientValley, 1 = VolcanicGorge
        public bool HasActiveRun = false;
        public string LastSavedTimestamp = "";
    }

    [Serializable]
    public class AssaultSaveProfile
    {
        public int AssaultLevel = 1;
        public int HighestLevelReached = 1;
        public int Meat = 500;
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public int MapIndex = 0; // 0 = AncientValley, 1 = VolcanicGorge
        public bool HasActiveRun = false;
        public string LastSavedTimestamp = "";
    }

    /// <summary>
    /// Independent Save Slot Data structure for Multi-Slot Save & Load (Red Dead Redemption / Woodcutter style).
    /// </summary>
    [Serializable]
    public class SaveSlotData
    {
        public string SlotId = "";
        public int SlotIndex = 0; // 0 = AutoSave, 1..5 = Manual slots
        public PrehistoricGameMode Mode = PrehistoricGameMode.TowerDefense;
        public bool IsAutoSave = false;

        // Progress details
        public int WaveOrLevel = 1;
        public int Resources = 500; // Gold for TD, Meat for Assault
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public int TotalMonstersKilled = 0;
        public int TotalScore = 0;
        public int MapIndex = 0; // 0 = AncientValley, 1 = VolcanicGorge
        public bool HasActiveRun = true;

        // Metadata
        public string SavedTimestamp = "";
        public string ChiefName = "Tù Trưởng Tiền Sử";
        public string CustomTitle = "";
    }

    [Serializable]
    public class PrehistoricSaveData
    {
        // 2 Bản lưu độc lập hoàn toàn cho 2 chế độ chơi
        public DefenseSaveProfile DefenseProfile = new DefenseSaveProfile();
        public AssaultSaveProfile AssaultProfile = new AssaultSaveProfile();

        public PrehistoricGameMode LastPlayedMode = PrehistoricGameMode.TowerDefense;
        public string PlayerChiefName = "Tù Trưởng Tiền Sử";

        // Tiến trình mở khóa bản đồ: 0 = Thung Lũng Cổ Đại, 1 = Núi Dung Nham (Vượt Đợt 10), 2 = Núi Băng Tuyết (Vượt Đợt 20)
        public int UnlockedMapMax = 0;

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

        public static PrehistoricSaveManager EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("PrehistoricSaveManager");
                Instance = go.AddComponent<PrehistoricSaveManager>();
            }
            return Instance;
        }

        public const int MAX_MANUAL_SLOTS = 5;

        private const string SAVE_FILE_NAME = "PrehistoricGameSave_v2.json";
        private const string LEGACY_SAVE_FILE_NAME = "PrehistoricGameSave.json";

        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        private string LegacySaveFilePath => Path.Combine(Application.persistentDataPath, LEGACY_SAVE_FILE_NAME);
        public string SaveSlotsDirectory => Path.Combine(Application.persistentDataPath, "SaveSlots");

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

            EnsureSlotsDirectoryExists();
            LoadSaveData();
            MigrateInitialSlotsIfMissing();
        }

        private void EnsureSlotsDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(SaveSlotsDirectory))
                {
                    Directory.CreateDirectory(SaveSlotsDirectory);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error creating slots directory: {ex.Message}");
            }
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
            if (CurrentData == null) CurrentData = new PrehistoricSaveData();
            if (CurrentData.DefenseProfile == null) CurrentData.DefenseProfile = new DefenseSaveProfile();
            if (CurrentData.AssaultProfile == null) CurrentData.AssaultProfile = new AssaultSaveProfile();

            // Tự động kiểm tra nếu người chơi đã từng vượt qua đợt 10 ở bản lưu cũ
            if (CurrentData.DefenseProfile.HighestWaveReached >= 10 && CurrentData.UnlockedMapMax < 1)
            {
                CurrentData.UnlockedMapMax = 1;
            }
            if (CurrentData.DefenseProfile.HighestWaveReached >= 20 && CurrentData.UnlockedMapMax < 2)
            {
                CurrentData.UnlockedMapMax = 2;
            }
        }

        public bool IsMapUnlocked(PrehistoricMapType map)
        {
            if (map == PrehistoricMapType.AncientValley) return true;
            EnsureProfilesInitialized();
            if (CurrentData == null) return false;

            if (map == PrehistoricMapType.VolcanicGorge)
            {
                return CurrentData.UnlockedMapMax >= 1 || 
                       (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.HighestWaveReached >= 10);
            }
            if (map == PrehistoricMapType.GlacialTundra)
            {
                return CurrentData.UnlockedMapMax >= 2 || 
                       (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.HighestWaveReached >= 20);
            }
            return true;
        }

        public void UnlockMap(PrehistoricMapType map)
        {
            EnsureProfilesInitialized();
            if (CurrentData == null) return;

            int required = map switch
            {
                PrehistoricMapType.VolcanicGorge => 1,
                PrehistoricMapType.GlacialTundra => 2,
                _ => 0
            };

            if (CurrentData.UnlockedMapMax < required)
            {
                CurrentData.UnlockedMapMax = required;
                SaveDataToDisk();
                Debug.Log($"<color=orange>[SaveSystem]</color> 🎉 Chúc mừng! Đã mở khóa bản đồ mới: <b>{map}</b>!");
            }
        }

        private void MigrateInitialSlotsIfMissing()
        {
            // Tự động gieo mầm khe Auto-Save nếu người chơi đã có tiến trình chơi mà chưa có file slot 0
            if (CurrentData?.DefenseProfile != null && CurrentData.DefenseProfile.CurrentWave >= 1)
            {
                if (!HasSlotData(PrehistoricGameMode.TowerDefense, 0, isAutoSave: true))
                {
                    SaveToSlot(PrehistoricGameMode.TowerDefense, 0, isAutoSave: true);
                }
            }
            if (CurrentData?.AssaultProfile != null && CurrentData.AssaultProfile.AssaultLevel >= 1)
            {
                if (!HasSlotData(PrehistoricGameMode.DinoAssault, 0, isAutoSave: true))
                {
                    SaveToSlot(PrehistoricGameMode.DinoAssault, 0, isAutoSave: true);
                }
            }
        }

        // =========================================================
        // MULTI-SLOT SAVE & LOAD IMPLEMENTATION
        // =========================================================

        public string GetSlotFilePath(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            EnsureSlotsDirectoryExists();
            string prefix = mode == PrehistoricGameMode.TowerDefense ? "defense" : "assault";
            string fileName = (isAutoSave || slotIndex == 0)
                ? $"{prefix}_autosave.json"
                : $"{prefix}_slot_{slotIndex}.json";
            return Path.Combine(SaveSlotsDirectory, fileName);
        }

        public bool HasSlotData(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            return File.Exists(GetSlotFilePath(mode, slotIndex, isAutoSave));
        }

        public SaveSlotData GetSlotData(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            string path = GetSlotFilePath(mode, slotIndex, isAutoSave);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<SaveSlotData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error reading slot file {path}: {ex.Message}");
                return null;
            }
        }

        public bool SaveToSlot(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            EnsureSlotsDirectoryExists();
            EnsureProfilesInitialized();

            string prefix = mode == PrehistoricGameMode.TowerDefense ? "defense" : "assault";
            string slotId = (isAutoSave || slotIndex == 0) ? $"{prefix}_autosave" : $"{prefix}_slot_{slotIndex}";

            SaveSlotData slot = new SaveSlotData
            {
                SlotId = slotId,
                SlotIndex = slotIndex,
                Mode = mode,
                IsAutoSave = isAutoSave || slotIndex == 0,
                ChiefName = CurrentData.PlayerChiefName,
                SavedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                HasActiveRun = true
            };

            bool isGameplayActive = UI.PrehistoricMainMenu.Instance == null || !UI.PrehistoricMainMenu.Instance.IsOpen;

            if (mode == PrehistoricGameMode.TowerDefense)
            {
                int wave = (isGameplayActive && PrehistoricGameplayManager.Instance != null) 
                    ? PrehistoricGameplayManager.Instance.CurrentWave 
                    : (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.CurrentWave > 0 ? CurrentData.DefenseProfile.CurrentWave : 1);

                if (isGameplayActive && PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.WaveCleared)
                {
                    // Khi vừa chiến thắng đợt, lưu lại đợt tiếp theo để khi tải/tiếp tục chơi sẽ vào ngay đợt mới
                    wave = Mathf.Min(wave + 1, PrehistoricGameplayManager.Instance.MaxWaves);
                }

                int baseHp = (isGameplayActive && PrehistoricGameplayManager.Instance != null) ? PrehistoricGameplayManager.Instance.CurrentBaseHealth : CurrentData.DefenseProfile.BaseHealth;
                int maxHp = (isGameplayActive && PrehistoricGameplayManager.Instance != null) ? PrehistoricGameplayManager.Instance.MaxBaseHealth : CurrentData.DefenseProfile.MaxBaseHealth;
                int gold = (isGameplayActive && TowerPlacer.Instance != null) ? TowerPlacer.Instance.Gold : CurrentData.DefenseProfile.Gold;
                int killed = CurrentData.DefenseProfile.TotalMonstersKilled;
                int score = (wave * 1000) + (killed * 25) + gold;

                slot.WaveOrLevel = wave;
                slot.Resources = gold;
                slot.BaseHealth = baseHp;
                slot.MaxBaseHealth = maxHp;
                slot.TotalMonstersKilled = killed;
                slot.TotalScore = score;
                slot.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                slot.CustomTitle = isAutoSave ? $"Tự động lưu - Đợt {wave} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})" : $"Thủ Thành - Đợt {wave} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})";

                // Đồng bộ cập nhật vào hồ sơ bộ nhớ
                CurrentData.DefenseProfile.CurrentWave = wave;
                CurrentData.DefenseProfile.HighestWaveReached = Mathf.Max(CurrentData.DefenseProfile.HighestWaveReached, wave);
                if (CurrentData.DefenseProfile.HighestWaveReached >= 10 && CurrentData.UnlockedMapMax < 1)
                {
                    CurrentData.UnlockedMapMax = 1;
                }
                if (CurrentData.DefenseProfile.HighestWaveReached >= 20 && CurrentData.UnlockedMapMax < 2)
                {
                    CurrentData.UnlockedMapMax = 2;
                }
                CurrentData.DefenseProfile.BaseHealth = baseHp;
                CurrentData.DefenseProfile.MaxBaseHealth = maxHp;
                CurrentData.DefenseProfile.Gold = gold;
                CurrentData.DefenseProfile.TotalScore = score;
                CurrentData.DefenseProfile.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                CurrentData.DefenseProfile.HasActiveRun = true;
                CurrentData.DefenseProfile.LastSavedTimestamp = slot.SavedTimestamp;
            }
            else
            {
                int level = (isGameplayActive && PrehistoricGameModeManager.Instance != null) 
                    ? PrehistoricGameModeManager.Instance.AssaultLevel 
                    : (CurrentData.AssaultProfile != null && CurrentData.AssaultProfile.AssaultLevel > 0 ? CurrentData.AssaultProfile.AssaultLevel : 1);

                if (isGameplayActive && PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.AssaultPhase == PrehistoricAssaultPhase.Victory)
                {
                    // Khi vừa chiếm thành thành công, lưu lại màn kế tiếp
                    level = level + 1;
                }

                int meat = 250;
                DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindAnyObjectByType<DinoSpawner>();
                if (isGameplayActive && spawner != null) meat = spawner.ResourcesToSpend;
                else if (CurrentData.AssaultProfile != null && CurrentData.AssaultProfile.Meat > 0) meat = CurrentData.AssaultProfile.Meat;

                int baseHp = 1000;
                int maxHp = 1000;
                if (isGameplayActive && PrehistoricVillageBase.Instance != null)
                {
                    baseHp = PrehistoricVillageBase.Instance.Health;
                    maxHp = PrehistoricVillageBase.Instance.MaxHealth;
                }
                else if (CurrentData.AssaultProfile != null)
                {
                    baseHp = CurrentData.AssaultProfile.BaseHealth;
                    maxHp = CurrentData.AssaultProfile.MaxBaseHealth;
                }

                slot.WaveOrLevel = level;
                slot.Resources = meat;
                slot.BaseHealth = baseHp;
                slot.MaxBaseHealth = maxHp;
                slot.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                slot.CustomTitle = isAutoSave ? $"Tự động lưu - Màn {level} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})" : $"Công Thành - Màn {level} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})";

                // Đồng bộ cập nhật vào hồ sơ bộ nhớ
                CurrentData.AssaultProfile.AssaultLevel = level;
                CurrentData.AssaultProfile.HighestLevelReached = Mathf.Max(CurrentData.AssaultProfile.HighestLevelReached, level);
                CurrentData.AssaultProfile.Meat = meat;
                CurrentData.AssaultProfile.BaseHealth = baseHp;
                CurrentData.AssaultProfile.MaxBaseHealth = maxHp;
                CurrentData.AssaultProfile.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                CurrentData.AssaultProfile.HasActiveRun = true;
                CurrentData.AssaultProfile.LastSavedTimestamp = slot.SavedTimestamp;
            }

            try
            {
                string filePath = GetSlotFilePath(mode, slotIndex, isAutoSave);
                string json = JsonUtility.ToJson(slot, true);
                File.WriteAllText(filePath, json);

                SaveDataToDisk();
                Debug.Log($"<color=green>[SaveSystem]</color> Saved Slot: {slotId} (Mode: {mode}, Wave/Lvl: {slot.WaveOrLevel}, Res: {slot.Resources})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveSystem] Error saving to slot {slotId}: {ex.Message}");
                return false;
            }
        }

        public bool DeleteSlot(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            string path = GetSlotFilePath(mode, slotIndex, isAutoSave);
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    Debug.Log($"<color=orange>[SaveSystem]</color> Deleted save slot: {path}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SaveSystem] Error deleting slot: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        public void LoadGameFromSlot(SaveSlotData slot)
        {
            if (slot == null) return;

            PrehistoricMapType targetMap = (PrehistoricMapType)slot.MapIndex;
            PrehistoricMapManager.LoadMap(targetMap, () =>
            {
                if (slot.Mode == PrehistoricGameMode.TowerDefense)
                {
                    CurrentData.DefenseProfile.CurrentWave = slot.WaveOrLevel;
                    CurrentData.DefenseProfile.Gold = slot.Resources;
                    CurrentData.DefenseProfile.BaseHealth = slot.BaseHealth;
                    CurrentData.DefenseProfile.MaxBaseHealth = slot.MaxBaseHealth;
                    CurrentData.DefenseProfile.TotalMonstersKilled = slot.TotalMonstersKilled;
                    CurrentData.DefenseProfile.TotalScore = slot.TotalScore;
                    CurrentData.DefenseProfile.MapIndex = slot.MapIndex;
                    CurrentData.DefenseProfile.HasActiveRun = true;
                    CurrentData.LastPlayedMode = PrehistoricGameMode.TowerDefense;

                    SaveDataToDisk();

                    var mgr = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance : UnityEngine.Object.FindFirstObjectByType<PrehistoricGameplayManager>();
                    if (mgr != null)
                    {
                        mgr.LoadDefenseWave(slot.WaveOrLevel, slot.Resources, slot.BaseHealth);
                    }
                }
                else
                {
                    CurrentData.AssaultProfile.AssaultLevel = slot.WaveOrLevel;
                    CurrentData.AssaultProfile.Meat = slot.Resources;
                    CurrentData.AssaultProfile.BaseHealth = slot.BaseHealth;
                    CurrentData.AssaultProfile.MaxBaseHealth = slot.MaxBaseHealth;
                    CurrentData.AssaultProfile.MapIndex = slot.MapIndex;
                    CurrentData.AssaultProfile.HasActiveRun = true;
                    CurrentData.LastPlayedMode = PrehistoricGameMode.DinoAssault;

                    SaveDataToDisk();

                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : UnityEngine.Object.FindFirstObjectByType<PrehistoricGameModeManager>();
                    if (gmm != null)
                    {
                        gmm.LoadAssaultLevel(slot.WaveOrLevel, slot.Resources, slot.BaseHealth);
                    }
                }
            });
        }

        // =========================================================
        // AUTO-SAVE INTEGRATIONS
        // =========================================================

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
                if (PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.WaveCleared)
                {
                    // Vừa thắng đợt: Tiến trình tự động lưu cho lần sau chơi tiếp là đợt mới
                    wave = Mathf.Min(wave + 1, PrehistoricGameplayManager.Instance.MaxWaves);
                }

                int baseHp = PrehistoricGameplayManager.Instance.CurrentBaseHealth;
                int maxHp = PrehistoricGameplayManager.Instance.MaxBaseHealth;
                int gold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : 500;

                CurrentData.DefenseProfile.CurrentWave = wave;
                CurrentData.DefenseProfile.HighestWaveReached = Mathf.Max(CurrentData.DefenseProfile.HighestWaveReached, wave);
                if (CurrentData.DefenseProfile.HighestWaveReached >= 10 && CurrentData.UnlockedMapMax < 1)
                {
                    CurrentData.UnlockedMapMax = 1;
                }
                if (CurrentData.DefenseProfile.HighestWaveReached >= 20 && CurrentData.UnlockedMapMax < 2)
                {
                    CurrentData.UnlockedMapMax = 2;
                }
                CurrentData.DefenseProfile.BaseHealth = baseHp;
                CurrentData.DefenseProfile.MaxBaseHealth = maxHp;
                CurrentData.DefenseProfile.Gold = gold;
                CurrentData.DefenseProfile.HasActiveRun = hasActiveRun;
                CurrentData.DefenseProfile.TotalScore = (CurrentData.DefenseProfile.HighestWaveReached * 1000) + (CurrentData.DefenseProfile.TotalMonstersKilled * 25) + gold;
            }

            SaveDataToDisk();
            SaveToSlot(PrehistoricGameMode.TowerDefense, 0, isAutoSave: true);
            Debug.Log($"<color=green>[SaveSystem]</color> Auto-Saved Defense Run: Wave {CurrentData.DefenseProfile.CurrentWave}, Gold {CurrentData.DefenseProfile.Gold}g, Active: {hasActiveRun}");
        }

        public void AutoSaveAssaultRun(bool hasActiveRun = true)
        {
            EnsureProfilesInitialized();

            if (PrehistoricGameModeManager.Instance != null)
            {
                int level = PrehistoricGameModeManager.Instance.AssaultLevel;
                if (PrehistoricGameModeManager.Instance.AssaultPhase == PrehistoricAssaultPhase.Victory)
                {
                    // Vừa chiếm thành thắng lợi: Tiến trình lưu là màn kế tiếp
                    level = level + 1;
                }

                int meat = 500;
                DinoSpawner spawner = DinoSpawner.Instance != null ? DinoSpawner.Instance : FindAnyObjectByType<DinoSpawner>();
                if (spawner != null) meat = Mathf.Max(500, spawner.ResourcesToSpend);

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
            SaveToSlot(PrehistoricGameMode.DinoAssault, 0, isAutoSave: true);
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
            DeleteSlot(PrehistoricGameMode.TowerDefense, 0, isAutoSave: true);
            Debug.Log("<color=orange>[SaveSystem]</color> Reset Defense save profile.");
        }

        public void ResetAssaultSave()
        {
            EnsureProfilesInitialized();
            CurrentData.AssaultProfile = new AssaultSaveProfile();
            SaveDataToDisk();
            DeleteSlot(PrehistoricGameMode.DinoAssault, 0, isAutoSave: true);
            Debug.Log("<color=orange>[SaveSystem]</color> Reset Assault save profile.");
        }

        public void ResetSaveData()
        {
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
                if (File.Exists(LegacySaveFilePath)) File.Delete(LegacySaveFilePath);
                if (Directory.Exists(SaveSlotsDirectory)) Directory.Delete(SaveSlotsDirectory, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Error deleting save files: {ex.Message}");
            }

            CurrentData = new PrehistoricSaveData();
            Debug.Log("<color=orange>[SaveSystem]</color> All save data and slot files have been completely reset.");
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
