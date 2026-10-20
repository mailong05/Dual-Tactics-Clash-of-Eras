using System;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Mobile;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Save;
using UnityEngine;

namespace LlamAcademy.Dinos.UI
{
    public enum SaveLoadDialogMode
    {
        Save,
        Load
    }

    /// <summary>
    /// Multi-Slot Save & Load Game Dialog (Similar to Red Dead Redemption / The Witcher / Woodcutter).
    /// Provides:
    /// 1. Save Mode: Lists 5 manual slots for active game mode. Shows empty slots for new saves or existing slots for overwrite with confirmation.
    /// 2. Load Mode: 2 separate tabs for Tower Defense and Dino Assault. Lists Auto-Save slot and Slots 1..5. One-click load into exact Wave/Level.
    /// 3. Overwrite & Delete confirmations, responsive layout for PC & Mobile touchscreen.
    /// </summary>
    [DefaultExecutionOrder(-4)]
    public class PrehistoricSaveLoadDialog : MonoBehaviour
    {
        public static PrehistoricSaveLoadDialog Instance { get; private set; }

        public bool IsOpen { get; private set; } = false;
        public SaveLoadDialogMode CurrentDialogMode { get; private set; } = SaveLoadDialogMode.Load;
        public PrehistoricGameMode ActiveTabMode { get; private set; } = PrehistoricGameMode.TowerDefense;

        private Vector2 _scrollPos = Vector2.zero;
        private string _feedbackMessage = "";
        private float _feedbackTimer = 0f;

        // Confirmation Modal State
        private bool _isConfirming = false;
        private string _confirmTitle = "";
        private string _confirmMessage = "";
        private Action _onConfirmAction = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static PrehistoricSaveLoadDialog EnsureInstance()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("PrehistoricSaveLoadDialog");
                Instance = go.AddComponent<PrehistoricSaveLoadDialog>();
            }
            return Instance;
        }

        public void OpenSaveDialog(PrehistoricGameMode mode)
        {
            CurrentDialogMode = SaveLoadDialogMode.Save;
            ActiveTabMode = mode;
            IsOpen = true;
            _isConfirming = false;
            _feedbackMessage = "";
            _scrollPos = Vector2.zero;
            PrehistoricAudioManager.Instance?.PlayButtonClick();
        }

        public void OpenLoadDialog(PrehistoricGameMode mode)
        {
            CurrentDialogMode = SaveLoadDialogMode.Load;
            ActiveTabMode = mode;
            IsOpen = true;
            _isConfirming = false;
            _feedbackMessage = "";
            _scrollPos = Vector2.zero;
            PrehistoricAudioManager.Instance?.PlayButtonClick();
        }

        public void CloseDialog()
        {
            IsOpen = false;
            _isConfirming = false;
            _feedbackMessage = "";
            PrehistoricAudioManager.Instance?.PlayButtonClick();
        }

        private void Update()
        {
            if (!IsOpen) return;

            if (_feedbackTimer > 0f)
            {
                _feedbackTimer -= Time.unscaledDeltaTime;
                if (_feedbackTimer <= 0f)
                {
                    _feedbackMessage = "";
                }
            }
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            MobileUIHelper.BeginScaling();
            try
            {
                DrawDialogBackdrop();
                DrawMainDialogWindow();

                if (_isConfirming)
                {
                    DrawConfirmationModal();
                }
            }
            finally
            {
                MobileUIHelper.EndScaling();
            }
        }

        private void DrawDialogBackdrop()
        {
            GUI.depth = -1200;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Semi-transparent dark full-screen overlay
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void DrawMainDialogWindow()
        {
            float screenW = MobileUIHelper.ScreenWidth;
            float screenH = MobileUIHelper.ScreenHeight;

            float winW = Mathf.Clamp(screenW * 0.72f, 540f, 840f);
            winW = Mathf.Min(winW, screenW - 20f);
            float winH = Mathf.Clamp(screenH * 0.82f, 480f, 720f);
            winH = Mathf.Min(winH, screenH - 24f);

            float x = (screenW - winW) / 2f;
            float y = (screenH - winH) / 2f;

            Rect winRect = new Rect(x, y, winW, winH);
            MobileUIHelper.RegisterUIRect(winRect);

            // Window background
            GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.98f);
            GUI.Box(winRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(x + 16, y + 14, winW - 32, winH - 28));
            GUILayout.BeginVertical();

            // 1. Header Bar: Title + Close Button
            GUILayout.BeginHorizontal();
            string dialogTitle = CurrentDialogMode == SaveLoadDialogMode.Save
                ? "💾 <color=#00FFAA><b>LƯU TRÒ CHƠI (CHỌN KHE LƯU)</b></color>"
                : "📂 <color=#38BDF8><b>TẢI TRÒ CHƠI (DANH SÁCH FILE LƯU)</b></color>";
            GUILayout.Label($"<size=17>{dialogTitle}</size>", GUI.skin.label);

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 0.95f);
            if (GUILayout.Button("<b>✕ ĐÓNG</b>", GUILayout.Width(85), GUILayout.Height(30)))
            {
                CloseDialog();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. Tabs Bar (Only in Load Mode)
            if (CurrentDialogMode == SaveLoadDialogMode.Load)
            {
                GUILayout.BeginHorizontal();

                bool isDefense = ActiveTabMode == PrehistoricGameMode.TowerDefense;
                GUI.backgroundColor = isDefense ? new Color(0.18f, 0.65f, 0.92f, 1f) : new Color(0.2f, 0.26f, 0.35f, 0.75f);
                if (GUILayout.Button("<b>🛡️ CHẾ ĐỘ THỦ THÀNH (DEFENSE)</b>", GUILayout.Height(36)))
                {
                    ActiveTabMode = PrehistoricGameMode.TowerDefense;
                    _scrollPos = Vector2.zero;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                bool isAssault = ActiveTabMode == PrehistoricGameMode.DinoAssault;
                GUI.backgroundColor = isAssault ? new Color(0.95f, 0.45f, 0.15f, 1f) : new Color(0.2f, 0.26f, 0.35f, 0.75f);
                if (GUILayout.Button("<b>🦖 CHẾ ĐỘ CÔNG THÀNH (ASSAULT)</b>", GUILayout.Height(36)))
                {
                    ActiveTabMode = PrehistoricGameMode.DinoAssault;
                    _scrollPos = Vector2.zero;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }
            else
            {
                // In Save Mode: Show current mode description
                string modeDesc = ActiveTabMode == PrehistoricGameMode.TowerDefense
                    ? "🛡️ Đang lưu tiến trình: <b>Chế độ Thủ Thành (Tower Defense)</b>"
                    : "🦖 Đang lưu tiến trình: <b>Chế độ Công Thành (Dino Assault)</b>";
                GUILayout.Label($"<size=12><color=#FCD34D>{modeDesc}</color></size>");
                GUILayout.Space(4);
            }

            // Feedback Message Bar (if any)
            if (!string.IsNullOrEmpty(_feedbackMessage))
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"<size=12><color=#55FF55><b>{_feedbackMessage}</b></color></size>");
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            // 3. Scroll View of Slots
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, false, true);

            var saveMgr = PrehistoricSaveManager.Instance;
            if (saveMgr != null)
            {
                if (CurrentDialogMode == SaveLoadDialogMode.Load)
                {
                    // In Load Mode: Show Auto-Save slot first (Slot 0)
                    SaveSlotData autoSave = saveMgr.GetSlotData(ActiveTabMode, 0, isAutoSave: true);
                    if (autoSave != null && autoSave.HasActiveRun)
                    {
                        DrawSlotCard(0, autoSave, isAutoSave: true);
                        GUILayout.Space(6);
                    }
                }

                // Show Manual Slots 1 to 5
                for (int slotIdx = 1; slotIdx <= PrehistoricSaveManager.MAX_MANUAL_SLOTS; slotIdx++)
                {
                    SaveSlotData slotData = saveMgr.GetSlotData(ActiveTabMode, slotIdx, isAutoSave: false);
                    DrawSlotCard(slotIdx, slotData, isAutoSave: false);
                    GUILayout.Space(6);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(8);

            // Bottom Action / Guide
            GUILayout.BeginHorizontal();
            string guideText = CurrentDialogMode == SaveLoadDialogMode.Save
                ? "💡 <i>Chọn một khe trống để lưu trận mới, hoặc bấm 'Ghi đè' để lưu đè lên khe có sẵn.</i>"
                : "💡 <i>Bấm 'Tải Trận Này' để nạp lại đúng Đợt/Màn đã lưu và tiếp tục thử nghiệm chiến thuật!</i>";
            GUILayout.Label($"<size=11><color=#94A3B8>{guideText}</color></size>");
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = new Color(0.4f, 0.45f, 0.55f);
            if (GUILayout.Button("<b>◀ QUAY LẠI</b>", GUILayout.Width(110), GUILayout.Height(32)))
            {
                CloseDialog();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawSlotCard(int slotIndex, SaveSlotData slotData, bool isAutoSave)
        {
            bool isOccupied = slotData != null && slotData.HasActiveRun;

            Color boxBg = isAutoSave
                ? new Color(0.12f, 0.22f, 0.32f, 0.95f)
                : (isOccupied ? new Color(0.12f, 0.16f, 0.22f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.70f));

            GUI.backgroundColor = boxBg;
            GUILayout.BeginVertical(GUI.skin.box);
            GUI.backgroundColor = Color.white;

            GUILayout.BeginHorizontal();

            // Left side: Slot Info
            GUILayout.BeginVertical(GUILayout.Width(380));

            string slotTag = isAutoSave ? "⚡ <color=#FBBF24><b>TỰ ĐỘNG LƯU (AUTOSAVE)</b></color>" : $"📁 <b>KHE LƯU SỐ {slotIndex}</b>";
            GUILayout.Label($"<size=13>{slotTag}</size>");

            if (isOccupied)
            {
                string modeLabel = slotData.Mode == PrehistoricGameMode.TowerDefense ? "Thủ Thành" : "Công Thành";
                string waveOrLevelLabel = slotData.Mode == PrehistoricGameMode.TowerDefense
                    ? $"<b><color=#00FFAA>ĐỢT {slotData.WaveOrLevel}</color></b>"
                    : $"<b><color=#FB923C>MÀN {slotData.WaveOrLevel}</color></b>";

                string resourceLabel = slotData.Mode == PrehistoricGameMode.TowerDefense
                    ? $"💰 <b>{slotData.Resources} Vàng</b>"
                    : $"🍖 <b>{slotData.Resources} Thịt</b>";

                string healthLabel = $"❤️ Thành: <b>{slotData.BaseHealth}/{slotData.MaxBaseHealth} HP</b>";

                GUILayout.Label($"<size=12>Chiến dịch: <b>{modeLabel}</b> | Tiến trình: {waveOrLevelLabel}</size>");
                GUILayout.Label($"<size=11><color=#E2E8F0>{resourceLabel} | {healthLabel}</color></size>");

                if (!string.IsNullOrEmpty(slotData.SavedTimestamp))
                {
                    GUILayout.Label($"<size=10><color=#94A3B8>🕒 Ngày giờ lưu: {slotData.SavedTimestamp} ({slotData.ChiefName})</color></size>");
                }
            }
            else
            {
                GUILayout.Label("<size=12><color=#64748B><i>(Khe lưu trống - Chưa có dữ liệu)</i></color></size>");
            }

            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Right side: Action Buttons based on Dialog Mode
            if (CurrentDialogMode == SaveLoadDialogMode.Save)
            {
                // In Save Mode: Can save to empty or overwrite occupied (Manual slots only)
                if (isAutoSave)
                {
                    GUILayout.Label("<size=11><color=#FBBF24><i>(Tự động cập nhật khi qua màn)</i></color></size>");
                }
                else if (!isOccupied)
                {
                    GUI.backgroundColor = new Color(0.15f, 0.75f, 0.45f, 0.98f);
                    if (GUILayout.Button("<b>💾 LƯU MỚI VÀO ĐÂY</b>", GUILayout.Width(170), GUILayout.Height(42)))
                    {
                        ExecuteSaveToSlot(slotIndex, isOverwrite: false);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.95f, 0.55f, 0.15f, 0.98f);
                    if (GUILayout.Button("<b>덮 GHI ĐÈ KHE NÀY</b>", GUILayout.Width(140), GUILayout.Height(42)))
                    {
                        PromptOverwriteSlot(slotIndex, slotData);
                    }

                    GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f, 0.90f);
                    if (GUILayout.Button("<b>🗑 Xóa</b>", GUILayout.Width(55), GUILayout.Height(42)))
                    {
                        PromptDeleteSlot(slotIndex, slotData, isAutoSave: false);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            else
            {
                // In Load Mode: Can load occupied or delete occupied
                if (isOccupied)
                {
                    GUI.backgroundColor = new Color(0.15f, 0.78f, 0.40f, 0.98f);
                    if (GUILayout.Button("<b>▶ TẢI TRẬN NÀY</b>", GUILayout.Width(140), GUILayout.Height(42)))
                    {
                        ExecuteLoadSlot(slotData);
                    }

                    if (!isAutoSave)
                    {
                        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f, 0.90f);
                        if (GUILayout.Button("<b>🗑 Xóa</b>", GUILayout.Width(55), GUILayout.Height(42)))
                        {
                            PromptDeleteSlot(slotIndex, slotData, isAutoSave: false);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.enabled = false;
                    GUILayout.Button("<b>Chưa có file</b>", GUILayout.Width(130), GUILayout.Height(36));
                    GUI.enabled = true;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void ExecuteSaveToSlot(int slotIndex, bool isOverwrite)
        {
            var saveMgr = PrehistoricSaveManager.Instance;
            if (saveMgr == null) return;

            bool success = saveMgr.SaveToSlot(ActiveTabMode, slotIndex, isAutoSave: false);
            if (success)
            {
                PrehistoricAudioManager.Instance?.PlayButtonClick();
                _feedbackMessage = isOverwrite
                    ? $"✓ Đã ghi đè thành công vào Khe Lưu {slotIndex}!"
                    : $"✓ Đã lưu thành công vào Khe Lưu {slotIndex}!";
                _feedbackTimer = 3.5f;
            }
            else
            {
                _feedbackMessage = "❌ Lưu thất bại, vui lòng kiểm tra dung lượng ổ đĩa!";
                _feedbackTimer = 3.5f;
            }
        }

        private void PromptOverwriteSlot(int slotIndex, SaveSlotData oldData)
        {
            _isConfirming = true;
            _confirmTitle = "⚠️ XÁC NHẬN GHI ĐÈ KHE LƯU";
            string modeName = oldData.Mode == PrehistoricGameMode.TowerDefense ? "Đợt " : "Màn ";
            _confirmMessage = $"Khe Lưu Số {slotIndex} hiện đang chứa dữ liệu của <b>{modeName}{oldData.WaveOrLevel}</b> (Lưu lúc {oldData.SavedTimestamp}).\n\nBạn có chắc chắn muốn <b>GHI ĐÈ</b> tiến trình trận đấu hiện tại lên khe này không?";
            _onConfirmAction = () =>
            {
                ExecuteSaveToSlot(slotIndex, isOverwrite: true);
            };
        }

        private void PromptDeleteSlot(int slotIndex, SaveSlotData data, bool isAutoSave)
        {
            _isConfirming = true;
            _confirmTitle = "🗑️ XÁC NHẬN XÓA KHE LƯU";
            string modeName = data.Mode == PrehistoricGameMode.TowerDefense ? "Đợt " : "Màn ";
            _confirmMessage = $"Bạn có chắc chắn muốn <b>XÓA BỎ VĨNH VIỄN</b> bản lưu Khe Số {slotIndex} ({modeName}{data.WaveOrLevel}) không?\nThao tác này không thể hoàn tác.";
            _onConfirmAction = () =>
            {
                var saveMgr = PrehistoricSaveManager.Instance;
                if (saveMgr != null)
                {
                    saveMgr.DeleteSlot(ActiveTabMode, slotIndex, isAutoSave);
                    _feedbackMessage = $"✓ Đã xóa dữ liệu Khe Lưu {slotIndex}.";
                    _feedbackTimer = 3.0f;
                    PrehistoricAudioManager.Instance?.PlayButtonClick();
                }
            };
        }

        private void ExecuteLoadSlot(SaveSlotData slotData)
        {
            if (slotData == null) return;

            PrehistoricAudioManager.Instance?.PlayButtonClick();
            CloseDialog();

            var saveMgr = PrehistoricSaveManager.Instance;
            if (saveMgr != null)
            {
                saveMgr.LoadGameFromSlot(slotData);
            }
        }

        private void DrawConfirmationModal()
        {
            GUI.depth = -1300;

            float modalW = Mathf.Min(500f, MobileUIHelper.ScreenWidth - 20f);
            float modalH = 220f;
            float mx = (MobileUIHelper.ScreenWidth - modalW) / 2f;
            float my = (MobileUIHelper.ScreenHeight - modalH) / 2f;

            Rect modalRect = new Rect(mx, my, modalW, modalH);
            MobileUIHelper.RegisterUIRect(modalRect);

            // Dark semi-transparent overlay over the dialog
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, MobileUIHelper.ScreenWidth, MobileUIHelper.ScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.backgroundColor = new Color(0.12f, 0.16f, 0.22f, 0.99f);
            GUI.Box(modalRect, GUIContent.none);

            GUILayout.BeginArea(new Rect(mx + 16, my + 14, modalW - 32, modalH - 28));
            GUILayout.BeginVertical();

            GUILayout.Label($"<size=15><color=#FBBF24><b>{_confirmTitle}</b></color></size>");
            GUILayout.Space(8);

            GUILayout.Label($"<size=12><color=#F1F5F9>{_confirmMessage}</color></size>", GUI.skin.label);
            GUILayout.FlexibleSpace();

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.78f, 0.4f, 0.98f);
            if (GUILayout.Button("<b>✓ ĐỒNG Ý</b>", GUILayout.Height(36)))
            {
                _isConfirming = false;
                _onConfirmAction?.Invoke();
                _onConfirmAction = null;
            }

            GUILayout.Space(10);

            GUI.backgroundColor = new Color(0.75f, 0.3f, 0.3f, 0.98f);
            if (GUILayout.Button("<b>✕ HỦY BỎ</b>", GUILayout.Height(36)))
            {
                _isConfirming = false;
                _onConfirmAction = null;
                PrehistoricAudioManager.Instance?.PlayButtonClick();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
}
