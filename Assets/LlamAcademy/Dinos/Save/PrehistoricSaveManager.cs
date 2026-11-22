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
        public int Gold = 1200;
        public int BaseHealth = 1000;
        public int MaxBaseHealth = 1000;
        public int TotalMonstersKilled = 0;
        public int TotalScore = 0;
        public int MapIndex = 0; // 0 = AncientValley, 1 = VolcanicGorge
        public int UnlockedMapMax = 0; // 0 = AncientValley, 1 = VolcanicGorge, 2 = GlacialTundra
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
        public int UnlockedMapMax = 0; // 0 = AncientValley, 1 = VolcanicGorge, 2 = GlacialTundra
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
    public class SaveSlotsCollection
    {
        public System.Collections.Generic.List<SaveSlotData> slots = new System.Collections.Generic.List<SaveSlotData>();
    }

    [Serializable]
    public class PrehistoricSaveData
    {
        // 2 Bản lưu độc lập hoàn toàn cho 2 chế độ chơi
        public DefenseSaveProfile DefenseProfile = new DefenseSaveProfile();
        public AssaultSaveProfile AssaultProfile = new AssaultSaveProfile();

        // Danh sách các khe lưu (Auto-Save + Khe 1..5) lưu trong bộ nhớ & đồng bộ 100% lên Cloud
        public SaveSlotsCollection SlotsCollection = new SaveSlotsCollection();

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
            get => DefenseProfile != null ? DefenseProfile.Gold : 1200;
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
        private static PrehistoricSaveManager _instance;
        private static bool s_isApplicationQuitting = false;

        public static PrehistoricSaveManager Instance
        {
            get
            {
                if (s_isApplicationQuitting) return null;
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PrehistoricSaveManager>();
                    if (_instance == null && !s_isApplicationQuitting)
                    {
                        GameObject go = new GameObject("PrehistoricSaveManager");
                        _instance = go.AddComponent<PrehistoricSaveManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }


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
            if (CurrentData == null || CurrentData.DefenseProfile == null) return false;
            return CurrentData.DefenseProfile.HasActiveRun || CurrentData.DefenseProfile.CurrentWave > 1 || CurrentData.DefenseProfile.HighestWaveReached > 1;
        }

        public bool HasActiveAssaultSave()
        {
            if (CurrentData == null || CurrentData.AssaultProfile == null) return false;
            return CurrentData.AssaultProfile.HasActiveRun || CurrentData.AssaultProfile.AssaultLevel > 1 || CurrentData.AssaultProfile.HighestLevelReached > 1;
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
            if (CurrentData.SlotsCollection == null) CurrentData.SlotsCollection = new SaveSlotsCollection();
            if (CurrentData.SlotsCollection.slots == null) CurrentData.SlotsCollection.slots = new System.Collections.Generic.List<SaveSlotData>();

            bool reached10 = (CurrentData.DefenseProfile.HighestWaveReached >= 10 || CurrentData.DefenseProfile.CurrentWave >= 10 ||
                              CurrentData.AssaultProfile.HighestLevelReached >= 10 || CurrentData.AssaultProfile.AssaultLevel >= 10);
            if (reached10 && CurrentData.UnlockedMapMax < 1)
            {
                CurrentData.UnlockedMapMax = 1;
                SaveDataToDisk();
            }

            bool reached20 = (CurrentData.DefenseProfile.HighestWaveReached >= 20 || CurrentData.DefenseProfile.CurrentWave >= 20 ||
                              CurrentData.AssaultProfile.HighestLevelReached >= 20 || CurrentData.AssaultProfile.AssaultLevel >= 20);
            if (reached20 && CurrentData.UnlockedMapMax < 2)
            {
                CurrentData.UnlockedMapMax = 2;
                SaveDataToDisk();
            }
        }

        public bool IsMapUnlocked(PrehistoricMapType map, PrehistoricGameMode? mode = null)
        {
            if (map == PrehistoricMapType.AncientValley) return true;
            EnsureProfilesInitialized();
            if (CurrentData == null) return false;

            PrehistoricGameMode targetMode = mode ?? (PrehistoricGameModeManager.Instance != null 
                ? PrehistoricGameModeManager.Instance.CurrentMode 
                : CurrentData.LastPlayedMode);

            if (targetMode == PrehistoricGameMode.TowerDefense)
            {
                if (CurrentData.DefenseProfile == null) return false;
                if (map == PrehistoricMapType.VolcanicGorge)
                {
                    bool defensePassed = CurrentData.DefenseProfile.HighestWaveReached >= 10 || CurrentData.DefenseProfile.CurrentWave >= 10;
                    return CurrentData.DefenseProfile.UnlockedMapMax >= 1 || defensePassed;
                }
                if (map == PrehistoricMapType.GlacialTundra)
                {
                    bool defensePassed = CurrentData.DefenseProfile.HighestWaveReached >= 20 || CurrentData.DefenseProfile.CurrentWave >= 20;
                    return CurrentData.DefenseProfile.UnlockedMapMax >= 2 || defensePassed;
                }
            }
            else // DinoAssault
            {
                if (CurrentData.AssaultProfile == null) return false;
                if (map == PrehistoricMapType.VolcanicGorge)
                {
                    bool assaultPassed = CurrentData.AssaultProfile.HighestLevelReached >= 10 || CurrentData.AssaultProfile.AssaultLevel >= 10;
                    return CurrentData.AssaultProfile.UnlockedMapMax >= 1 || assaultPassed;
                }
                if (map == PrehistoricMapType.GlacialTundra)
                {
                    bool assaultPassed = CurrentData.AssaultProfile.HighestLevelReached >= 20 || CurrentData.AssaultProfile.AssaultLevel >= 20;
                    return CurrentData.AssaultProfile.UnlockedMapMax >= 2 || assaultPassed;
                }
            }

            return false;
        }

        public void UnlockMap(PrehistoricMapType map, PrehistoricGameMode? mode = null)
        {
            EnsureProfilesInitialized();
            if (CurrentData == null) return;

            PrehistoricGameMode targetMode = mode ?? (PrehistoricGameModeManager.Instance != null 
                ? PrehistoricGameModeManager.Instance.CurrentMode 
                : CurrentData.LastPlayedMode);

            int required = map switch
            {
                PrehistoricMapType.VolcanicGorge => 1,
                PrehistoricMapType.GlacialTundra => 2,
                _ => 0
            };

            if (targetMode == PrehistoricGameMode.TowerDefense)
            {
                if (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.UnlockedMapMax < required)
                {
                    CurrentData.DefenseProfile.UnlockedMapMax = required;
                }
            }
            else
            {
                if (CurrentData.AssaultProfile != null && CurrentData.AssaultProfile.UnlockedMapMax < required)
                {
                    CurrentData.AssaultProfile.UnlockedMapMax = required;
                }
            }

            CurrentData.UnlockedMapMax = Math.Max(CurrentData.DefenseProfile?.UnlockedMapMax ?? 0, CurrentData.AssaultProfile?.UnlockedMapMax ?? 0);
            SaveDataToDisk();
            Debug.Log($"<color=orange>[SaveSystem]</color> 🎉 Đã mở khóa bản đồ mới: <b>{map}</b> cho chế độ <b>{targetMode}</b>!");
        }

        private void MigrateInitialSlotsIfMissing()
        {
            EnsureAutoSaveSlotPresent();
        }

        private void EnsureAutoSaveSlotPresent()
        {
            EnsureProfilesInitialized();
            if (CurrentData?.SlotsCollection == null) CurrentData.SlotsCollection = new SaveSlotsCollection();
            if (CurrentData.SlotsCollection.slots == null) CurrentData.SlotsCollection.slots = new System.Collections.Generic.List<SaveSlotData>();

            // Gieo mầm khe Auto-Save cho chế độ Thủ Thành nếu chưa có
            if (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.CurrentWave >= 1)
            {
                var defAuto = CurrentData.SlotsCollection.slots.Find(s => s != null && s.Mode == PrehistoricGameMode.TowerDefense && s.IsAutoSave);
                if (defAuto == null)
                {
                    int w = CurrentData.DefenseProfile.CurrentWave;
                    int g = Mathf.Max(CurrentData.DefenseProfile.Gold, PrehistoricGameplayManager.GetDefenseStartingGoldForWave(w));
                    CurrentData.SlotsCollection.slots.Add(new SaveSlotData
                    {
                        SlotId = "defense_autosave",
                        SlotIndex = 0,
                        Mode = PrehistoricGameMode.TowerDefense,
                        IsAutoSave = true,
                        WaveOrLevel = w,
                        Resources = g,
                        BaseHealth = 1000,
                        MaxBaseHealth = 1000,
                        ChiefName = CurrentData.PlayerChiefName,
                        SavedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        HasActiveRun = CurrentData.DefenseProfile.HasActiveRun,
                        CustomTitle = $"Tự động lưu - Đợt {w}"
                    });
                }
                else
                {
                    if (CurrentData.DefenseProfile.CurrentWave != defAuto.WaveOrLevel)
                    {
                        defAuto.WaveOrLevel = CurrentData.DefenseProfile.CurrentWave;
                        defAuto.Resources = Mathf.Max(CurrentData.DefenseProfile.Gold, PrehistoricGameplayManager.GetDefenseStartingGoldForWave(defAuto.WaveOrLevel));
                        defAuto.CustomTitle = $"Tự động lưu - Đợt {defAuto.WaveOrLevel}";
                    }
                }
            }

            // Gieo mầm khe Auto-Save cho chế độ Công Thành nếu chưa có
            if (CurrentData.AssaultProfile != null && CurrentData.AssaultProfile.AssaultLevel >= 1)
            {
                var assAuto = CurrentData.SlotsCollection.slots.Find(s => s != null && s.Mode == PrehistoricGameMode.DinoAssault && s.IsAutoSave);
                if (assAuto == null)
                {
                    int l = CurrentData.AssaultProfile.AssaultLevel;
                    int m = Mathf.Max(CurrentData.AssaultProfile.Meat, PrehistoricGameModeManager.GetFixedMeatForLevel(l));
                    int hp = 1000 + (l - 1) * 350;
                    CurrentData.SlotsCollection.slots.Add(new SaveSlotData
                    {
                        SlotId = "assault_autosave",
                        SlotIndex = 0,
                        Mode = PrehistoricGameMode.DinoAssault,
                        IsAutoSave = true,
                        WaveOrLevel = l,
                        Resources = m,
                        BaseHealth = hp,
                        MaxBaseHealth = hp,
                        ChiefName = CurrentData.PlayerChiefName,
                        SavedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        HasActiveRun = CurrentData.AssaultProfile.HasActiveRun,
                        CustomTitle = $"Tự động lưu - Màn {l}"
                    });
                }
                else
                {
                    if (CurrentData.AssaultProfile.AssaultLevel != assAuto.WaveOrLevel)
                    {
                        assAuto.WaveOrLevel = CurrentData.AssaultProfile.AssaultLevel;
                        assAuto.Resources = Mathf.Max(CurrentData.AssaultProfile.Meat, PrehistoricGameModeManager.GetFixedMeatForLevel(assAuto.WaveOrLevel));
                        assAuto.CustomTitle = $"Tự động lưu - Màn {assAuto.WaveOrLevel}";
                    }
                }
            }
        }

        // =========================================================
        // MULTI-SLOT CLOUD-FIRST SAVE & LOAD IMPLEMENTATION
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
            var s = GetSlotData(mode, slotIndex, isAutoSave);
            return s != null && s.WaveOrLevel > 0;
        }

        public SaveSlotData GetSlotData(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            EnsureProfilesInitialized();
            if (CurrentData?.SlotsCollection?.slots == null) return null;

            bool targetIsAuto = isAutoSave || slotIndex == 0;
            var found = CurrentData.SlotsCollection.slots.Find(s => s != null && s.Mode == mode && s.IsAutoSave == targetIsAuto && (targetIsAuto || s.SlotIndex == slotIndex));
            if (found != null && found.WaveOrLevel > 0) return found;

            // Nếu là Auto-Save mà chưa có thì tạo ngay từ profile
            if (targetIsAuto)
            {
                EnsureAutoSaveSlotPresent();
                return CurrentData.SlotsCollection.slots.Find(s => s != null && s.Mode == mode && s.IsAutoSave);
            }

            return null;
        }

        public bool SaveToSlot(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            EnsureProfilesInitialized();
            if (CurrentData.SlotsCollection == null) CurrentData.SlotsCollection = new SaveSlotsCollection();
            if (CurrentData.SlotsCollection.slots == null) CurrentData.SlotsCollection.slots = new System.Collections.Generic.List<SaveSlotData>();

            string prefix = mode == PrehistoricGameMode.TowerDefense ? "defense" : "assault";
            bool targetIsAuto = isAutoSave || slotIndex == 0;
            string slotId = targetIsAuto ? $"{prefix}_autosave" : $"{prefix}_slot_{slotIndex}";

            SaveSlotData slot = new SaveSlotData
            {
                SlotId = slotId,
                SlotIndex = targetIsAuto ? 0 : slotIndex,
                Mode = mode,
                IsAutoSave = targetIsAuto,
                ChiefName = CurrentData.PlayerChiefName,
                SavedTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                HasActiveRun = true
            };

            bool isGameplayActive = !UI.PrehistoricMainMenu.IsAnyMenuOrAuthOpen;

            if (mode == PrehistoricGameMode.TowerDefense)
            {
                int wave = (isGameplayActive && PrehistoricGameplayManager.Instance != null) 
                    ? PrehistoricGameplayManager.Instance.CurrentWave 
                    : (CurrentData.DefenseProfile != null && CurrentData.DefenseProfile.CurrentWave > 0 ? CurrentData.DefenseProfile.CurrentWave : 1);

                // Quy tắc chuẩn: Đã hoàn thành màn X (WaveCleared) -> lưu ở màn X + 1; Đang chơi hoặc thua -> lưu ở màn X
                if (isGameplayActive && PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.WaveCleared)
                {
                    wave = Mathf.Min(wave + 1, PrehistoricGameplayManager.Instance.MaxWaves);
                }

                int maxHp = 1000;
                int baseHp = 1000;
                int waveBudget = PrehistoricGameplayManager.GetDefenseStartingGoldForWave(wave);
                int killed = CurrentData.DefenseProfile.TotalMonstersKilled;
                int score = (wave * 1000) + (killed * 25) + waveBudget;

                slot.WaveOrLevel = wave;
                slot.Resources = waveBudget;
                slot.BaseHealth = baseHp;
                slot.MaxBaseHealth = maxHp;
                slot.TotalMonstersKilled = killed;
                slot.TotalScore = score;
                slot.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                slot.CustomTitle = targetIsAuto ? $"Tự động lưu - Đợt {wave} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})" : $"Thủ Thành - Đợt {wave} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})";

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
                CurrentData.DefenseProfile.Gold = waveBudget;
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

                // Quy tắc chuẩn: Đã hoàn thành màn X (Victory) -> lưu ở màn X + 1; Đang chơi hoặc thua -> lưu ở màn X
                if (isGameplayActive && PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.AssaultPhase == PrehistoricAssaultPhase.Victory)
                {
                    level = level + 1;
                }

                int targetMeat = 500 + (level - 1) * 200;
                int fullHp = 1000 + (level - 1) * 350;

                slot.WaveOrLevel = level;
                slot.Resources = targetMeat;
                slot.BaseHealth = fullHp;
                slot.MaxBaseHealth = fullHp;
                slot.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                slot.CustomTitle = targetIsAuto ? $"Tự động lưu - Màn {level} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})" : $"Công Thành - Màn {level} ({PrehistoricMapManager.GetMapDisplayName(PrehistoricMapManager.CurrentMap)})";

                // Đồng bộ cập nhật vào hồ sơ bộ nhớ
                CurrentData.AssaultProfile.AssaultLevel = level;
                CurrentData.AssaultProfile.HighestLevelReached = Mathf.Max(CurrentData.AssaultProfile.HighestLevelReached, level);
                CurrentData.AssaultProfile.Meat = targetMeat;
                CurrentData.AssaultProfile.BaseHealth = fullHp;
                CurrentData.AssaultProfile.MaxBaseHealth = fullHp;
                CurrentData.AssaultProfile.MapIndex = (int)PrehistoricMapManager.CurrentMap;
                CurrentData.AssaultProfile.HasActiveRun = true;
                CurrentData.AssaultProfile.LastSavedTimestamp = slot.SavedTimestamp;
            }

            // Cập nhật khe vào SlotsCollection
            CurrentData.SlotsCollection.slots.RemoveAll(s => s != null && s.Mode == mode && s.IsAutoSave == targetIsAuto && (targetIsAuto || s.SlotIndex == (targetIsAuto ? 0 : slotIndex)));
            CurrentData.SlotsCollection.slots.Add(slot);

            SaveDataToDisk();
            AutoSyncToCloudIfOnline();
            Debug.Log($"<color=green>[SaveSystem]</color> Đã lưu Khe: {slotId} (Mode: {mode}, Wave/Lvl: {slot.WaveOrLevel}, Res: {slot.Resources}) lên Cloud!");
            return true;
        }

        public bool DeleteSlot(PrehistoricGameMode mode, int slotIndex, bool isAutoSave = false)
        {
            EnsureProfilesInitialized();
            if (CurrentData?.SlotsCollection?.slots == null) return false;

            bool targetIsAuto = isAutoSave || slotIndex == 0;
            int removed = CurrentData.SlotsCollection.slots.RemoveAll(s => s != null && s.Mode == mode && s.IsAutoSave == targetIsAuto && (targetIsAuto || s.SlotIndex == slotIndex));
            if (removed > 0)
            {
                SaveDataToDisk();
                AutoSyncToCloudIfOnline();
                Debug.Log($"<color=orange>[SaveSystem]</color> Đã xóa Khe Lưu: Mode={mode}, Slot={slotIndex}, Auto={targetIsAuto}");
                return true;
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
                    int targetWave = Mathf.Max(1, slot.WaveOrLevel);
                    int minWaveBudget = PrehistoricGameplayManager.GetDefenseStartingGoldForWave(targetWave);
                    int finalGold = Mathf.Max(slot.Resources, minWaveBudget);

                    CurrentData.DefenseProfile.CurrentWave = targetWave;
                    CurrentData.DefenseProfile.Gold = finalGold;
                    CurrentData.DefenseProfile.BaseHealth = 1000;
                    CurrentData.DefenseProfile.MaxBaseHealth = 1000;
                    CurrentData.DefenseProfile.TotalMonstersKilled = slot.TotalMonstersKilled;
                    CurrentData.DefenseProfile.TotalScore = slot.TotalScore;
                    CurrentData.DefenseProfile.MapIndex = slot.MapIndex;
                    CurrentData.DefenseProfile.HasActiveRun = true;
                    CurrentData.LastPlayedMode = PrehistoricGameMode.TowerDefense;

                    SaveDataToDisk();
                    AutoSyncToCloudIfOnline();

                    var mgr = PrehistoricGameplayManager.Instance != null ? PrehistoricGameplayManager.Instance : UnityEngine.Object.FindFirstObjectByType<PrehistoricGameplayManager>();
                    if (mgr != null)
                    {
                        mgr.LoadDefenseWave(targetWave, finalGold, 1000);
                    }
                }
                else
                {
                    int targetLevel = Mathf.Max(1, slot.WaveOrLevel);
                    int targetMeat = Mathf.Max(slot.Resources, 500 + (targetLevel - 1) * 200);
                    int fullBaseHp = 1000 + (targetLevel - 1) * 350;

                    CurrentData.AssaultProfile.AssaultLevel = targetLevel;
                    CurrentData.AssaultProfile.Meat = targetMeat;
                    CurrentData.AssaultProfile.BaseHealth = fullBaseHp;
                    CurrentData.AssaultProfile.MaxBaseHealth = fullBaseHp;
                    CurrentData.AssaultProfile.MapIndex = slot.MapIndex;
                    CurrentData.AssaultProfile.HasActiveRun = true;
                    CurrentData.LastPlayedMode = PrehistoricGameMode.DinoAssault;

                    SaveDataToDisk();
                    AutoSyncToCloudIfOnline();

                    var gmm = PrehistoricGameModeManager.Instance != null ? PrehistoricGameModeManager.Instance : UnityEngine.Object.FindFirstObjectByType<PrehistoricGameModeManager>();
                    if (gmm != null)
                    {
                        gmm.LoadAssaultLevel(targetLevel, targetMeat, fullBaseHp);
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

                int maxHp = PrehistoricGameplayManager.Instance.MaxBaseHealth;
                // Khi ở giai đoạn chuẩn bị xây dựng, vừa thắng đợt hoặc thua trận, máu lưu luôn là 100% đầy đủ
                int baseHp = (PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.CombatPhase) 
                    ? PrehistoricGameplayManager.Instance.CurrentBaseHealth 
                    : maxHp;
                int waveBudget = PrehistoricGameplayManager.GetDefenseStartingGoldForWave(wave);
                int currentGold = TowerPlacer.Instance != null ? TowerPlacer.Instance.Gold : waveBudget;

                // Nếu thua trận (GameOver) hoặc không có ván đấu active (chuẩn bị đợt mới), vàng lưu luôn là ngân sách đầy đủ của đợt để khi chơi lại không bị kẹt vàng
                int gold = (PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.GameOver || !hasActiveRun)
                    ? waveBudget
                    : Mathf.Max(currentGold, waveBudget);

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
            AutoSyncToCloudIfOnline();
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

                int fullHp = 1000 + (level - 1) * 350;
                int baseHp = fullHp;
                int maxHp = fullHp;
                if (PrehistoricGameModeManager.Instance != null && PrehistoricGameModeManager.Instance.AssaultPhase == PrehistoricAssaultPhase.Combat && PrehistoricVillageBase.Instance != null)
                {
                    baseHp = PrehistoricVillageBase.Instance.Health;
                    maxHp = PrehistoricVillageBase.Instance.MaxHealth > 0 ? PrehistoricVillageBase.Instance.MaxHealth : fullHp;
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
            AutoSyncToCloudIfOnline();
            Debug.Log($"<color=green>[SaveSystem]</color> Auto-Saved Assault Run: Level {CurrentData.AssaultProfile.AssaultLevel}, Meat {CurrentData.AssaultProfile.Meat}, Active: {hasActiveRun}");
        }

        public void SaveDataToDisk()
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

        private float _lastAutoCloudSyncTime = -99f;
        private bool _cloudHooksBound = false;

        private void Start()
        {
            BindCloudHooks();
        }

        private void BindCloudHooks()
        {
            if (_cloudHooksBound || IOCloudManager.Instance == null) return;
            IOCloudManager.Instance.OnCloudSaveLoaded += HandleCloudSaveLoaded;
            IOCloudManager.Instance.OnCollectSaveSnapshot = CollectCloudSnapshot;
            _cloudHooksBound = true;
        }

        private (string playerName, int coins, int highestWave, string mode) CollectCloudSnapshot()
        {
            EnsureProfilesInitialized();
            bool isAssault = CurrentData.LastPlayedMode == PrehistoricGameMode.DinoAssault;
            int waveOrLvl = isAssault ? CurrentData.AssaultProfile.HighestLevelReached : CurrentData.DefenseProfile.HighestWaveReached;
            int res = isAssault ? CurrentData.AssaultProfile.Meat : CurrentData.DefenseProfile.Gold;
            return (CurrentData.PlayerChiefName, res, waveOrLvl, CurrentData.LastPlayedMode.ToString());
        }

        private void HandleCloudSaveLoaded(CloudSaveResponse loaded)
        {
            if (loaded == null) return;
            EnsureProfilesInitialized();

            if (!string.IsNullOrWhiteSpace(loaded.player_name))
            {
                CurrentData.PlayerChiefName = loaded.player_name;
            }

            // Chế độ Thủ Thành (Tower Defense) độc lập
            int defWave = loaded.defense_wave > 0 ? loaded.defense_wave : loaded.highest_wave;
            int defHighest = loaded.defense_highest_wave > 0 ? loaded.defense_highest_wave : loaded.highest_wave;
            int defGold = loaded.defense_gold > 0 ? loaded.defense_gold : (loaded.coins > 0 ? loaded.coins : 1200);
            CurrentData.DefenseProfile.CurrentWave = Mathf.Max(1, defWave);
            CurrentData.DefenseProfile.HighestWaveReached = Mathf.Max(CurrentData.DefenseProfile.HighestWaveReached, defHighest);
            CurrentData.DefenseProfile.Gold = Mathf.Max(CurrentData.DefenseProfile.Gold, defGold);
            CurrentData.DefenseProfile.HasActiveRun = loaded.defense_has_run;

            // Chế độ Công Thành (Dino Assault) độc lập
            int assLevel = loaded.assault_level > 0 ? loaded.assault_level : 1;
            int assHighest = loaded.assault_highest_level > 0 ? loaded.assault_highest_level : 1;
            int assMeat = loaded.assault_meat > 0 ? loaded.assault_meat : 500;
            CurrentData.AssaultProfile.AssaultLevel = Mathf.Max(1, assLevel);
            CurrentData.AssaultProfile.HighestLevelReached = Mathf.Max(CurrentData.AssaultProfile.HighestLevelReached, assHighest);
            CurrentData.AssaultProfile.Meat = Mathf.Max(CurrentData.AssaultProfile.Meat, assMeat);
            CurrentData.AssaultProfile.HasActiveRun = loaded.assault_has_run;

            // Tải danh sách Khe Lưu (SlotsCollection) trực tiếp từ Cloud
            if (!string.IsNullOrEmpty(loaded.slots_json))
            {
                try
                {
                    SaveSlotsCollection sc = JsonUtility.FromJson<SaveSlotsCollection>(loaded.slots_json);
                    if (sc != null && sc.slots != null && sc.slots.Count > 0)
                    {
                        CurrentData.SlotsCollection = sc;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveSystem] Error deserializing slots_json from Cloud: {ex.Message}");
                }
            }

            EnsureAutoSaveSlotPresent();

            SaveDataToDisk();
            OnGameLoaded?.Invoke(CurrentData);
            Debug.Log($"<color=cyan>[IO Cloud]</color> Đã nạp thành công bản lưu từ Cloud cho {CurrentData.PlayerChiefName} (Thủ Thành: Đợt {CurrentData.DefenseProfile.CurrentWave}, Công Thành: Màn {CurrentData.AssaultProfile.AssaultLevel}).");
        }

        public void SaveToCloudSyncNow()
        {
            if (IOCloudManager.Instance != null && IOCloudManager.Instance.IsCloudConnected)
            {
                var snap = CollectCloudSnapshot();
                IOCloudManager.Instance.SaveProgressToCloudSync(snap.playerName, snap.coins, snap.highestWave, snap.mode);
            }
        }

        private int _lastSubmittedScore = -1;

        private void AutoSyncToCloudIfOnline()
        {
            BindCloudHooks();
            if (s_isApplicationQuitting) return;
            if (Time.unscaledTime - _lastAutoCloudSyncTime < 2.0f) return;

            if (IOCloudManager.Instance != null && IOCloudManager.Instance.IsCloudConnected)
            {
                _lastAutoCloudSyncTime = Time.unscaledTime;
                var snap = CollectCloudSnapshot();
                IOCloudManager.Instance.SaveProgressToCloud(snap.playerName, snap.coins, snap.highestWave, snap.mode);
                if (CurrentData.TotalScore > 0 && CurrentData.TotalScore != _lastSubmittedScore)
                {
                    _lastSubmittedScore = CurrentData.TotalScore;
                    IOCloudManager.Instance.SubmitScoreToCloud(snap.playerName, CurrentData.TotalScore, snap.highestWave);
                }
            }
        }

        public void SyncToCloud(Action<bool, string> onComplete)
        {
            if (IOCloudManager.Instance != null && IOCloudManager.Instance.IsCloudConnected)
            {
                IOCloudManager.Instance.SaveProgressToCloud(CurrentData.PlayerChiefName, CurrentData.Gold, CurrentData.HighestWaveReached, CurrentData.LastPlayedMode.ToString());
                IOCloudManager.Instance.SubmitScoreToCloud(CurrentData.PlayerChiefName, CurrentData.TotalScore, CurrentData.HighestWaveReached);
                onComplete?.Invoke(true, $"Đã đồng bộ thành công hồ sơ [{CurrentData.PlayerChiefName}] ({CurrentData.TotalScore} điểm) lên IO Cloud!");
            }
            else
            {
                onComplete?.Invoke(false, $"Cloud backend offline. Điểm kỷ lục ({CurrentData.TotalScore} điểm) đã được lưu an toàn trên máy.");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                AutoSaveCurrentRun(true);
                SaveDataToDisk();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                AutoSaveCurrentRun(true);
                SaveDataToDisk();
            }
        }

        private void OnApplicationQuit()
        {
            AutoSaveCurrentRun(true);
            SaveDataToDisk();
            SaveToCloudSyncNow();
            s_isApplicationQuitting = true;
        }
    }
}
