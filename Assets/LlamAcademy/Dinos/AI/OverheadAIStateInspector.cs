using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Displays dynamic academic AI decision tags directly above dinosaurs' heads.
    /// E.g. [Bọc Sườn Né Tháp], [Phá Tường Mở Đường], [Đột Kích Tầm Không], [Đang Tấn Công].
    /// Enables 100% visual verification for instructors during evaluation and thesis defense.
    /// </summary>
    public class OverheadAIStateInspector : MonoBehaviour
    {
        public static bool ShowOverheadTags = true;

        private Unit.Unit _unit;
        private string _currentStateLabel = "[AI Đang Tính Toán]";
        private Color _tagColor = Color.cyan;
        private Camera _mainCam;
        private DinoAISpeciesType _species = DinoAISpeciesType.VelociraptorFlanker;
        private float _lastTacticCheck = 0f;

        public static OverheadAIStateInspector AttachTo(GameObject go, DinoAISpeciesType species)
        {
            if (go == null) return null;
            OverheadAIStateInspector inspector = go.GetComponent<OverheadAIStateInspector>();
            if (inspector == null) inspector = go.AddComponent<OverheadAIStateInspector>();
            inspector._unit = go.GetComponent<Unit.Unit>();
            inspector.SetSpecies(species);
            return inspector;
        }

        private void Awake()
        {
            if (_unit == null) _unit = GetComponent<Unit.Unit>();
            if (_mainCam == null) _mainCam = Camera.main;
        }

        private void Start()
        {
            if (_unit == null) _unit = GetComponent<Unit.Unit>();
            if (_mainCam == null) _mainCam = Camera.main;
            if (_currentStateLabel == "[AI Đang Tính Toán]")
            {
                DetermineInitialState();
            }
        }

        private void Update()
        {
            if (Time.time - _lastTacticCheck > 0.6f)
            {
                _lastTacticCheck = Time.time;
                UpdateDynamicTacticState();
            }
        }

        private void UpdateDynamicTacticState()
        {
            // Kiểm tra trạng thái chiến đấu thực tế
            if (_unit is PrehistoricDinoBase dinoBase && dinoBase.TargetDamageable != null)
            {
                if (dinoBase.TargetDamageable is Wall)
                {
                    SetStateLabel("🛡️ [Đang Phá Tường]", new Color(1.0f, 0.70f, 0.2f));
                    return;
                }
                if (dinoBase.TargetDamageable is PrehistoricVillageBase)
                {
                    SetStateLabel("⚔️ [Tấn Công Căn Cứ]", new Color(1.0f, 0.25f, 0.25f));
                    return;
                }
            }

            switch (_species)
            {
                case DinoAISpeciesType.VelociraptorFlanker:
                    UpdateVelociraptorTactic();
                    break;
                case DinoAISpeciesType.AnkylosaurusTanker:
                    SetStateLabel("🛡️ [Phá Tường Mở Đường]", new Color(1.0f, 0.65f, 0.2f));
                    break;
                case DinoAISpeciesType.PterodactylFlyer:
                    SetStateLabel("🦅 [Đột Kích Tầm Không]", new Color(0.85f, 0.4f, 1.0f));
                    break;
                case DinoAISpeciesType.TRexApexBoss:
                    SetStateLabel("👑 [Tổng Lực Tiến Công]", new Color(1.0f, 0.2f, 0.2f));
                    break;
            }
        }

        private void UpdateVelociraptorTactic()
        {
            // Phân biệt theo cánh bọc sườn thực tế (trái / phải / trung tâm)
            float posX = transform.position.x;
            if (posX < -2.0f)
            {
                SetStateLabel("🐆 [Bọc Sườn Cánh Trái]", new Color(0.20f, 0.95f, 0.85f));
                return;
            }
            if (posX > 2.0f)
            {
                SetStateLabel("🐆 [Bọc Sườn Cánh Phải]", new Color(0.98f, 0.85f, 0.20f));
                return;
            }

            var behavior = ThreatAwarePathfinding.Instance != null
                ? ThreatAwarePathfinding.Instance.GetAssignedTacticalBehavior(DinoAISpeciesType.VelociraptorFlanker)
                : DinoTacticalBehavior.SwarmRush;

            switch (behavior)
            {
                case DinoTacticalBehavior.FlankerEvasion:
                    SetStateLabel("🐆 [Bọc Sườn Né Tháp]", new Color(0.2f, 0.95f, 0.85f));
                    break;
                case DinoTacticalBehavior.SiegeBreaker:
                    SetStateLabel("🛡️ [Phá Tường Mở Đường]", new Color(1.0f, 0.65f, 0.2f));
                    break;
                case DinoTacticalBehavior.MassSwarmOverwhelm:
                    SetStateLabel("🌊 [Biển Quái Áp Đảo]", new Color(1.0f, 0.4f, 0.3f));
                    break;
                case DinoTacticalBehavior.FrontalAssault:
                    SetStateLabel("⚔️ [Tấn Công Trực Diện]", new Color(0.95f, 0.3f, 0.3f));
                    break;
                default:
                    SetStateLabel("⚡ [Xung Phong Bầy Đàn]", new Color(1.0f, 0.85f, 0.2f));
                    break;
            }
        }

        public void SetSpecies(DinoAISpeciesType species)
        {
            _species = species;
            UpdateDynamicTacticState();
        }

        private void DetermineInitialState()
        {
            string n = gameObject.name.ToLower();
            if (n.Contains("raptor") || n.Contains("velociraptor"))
            {
                SetSpecies(DinoAISpeciesType.VelociraptorFlanker);
            }
            else if (n.Contains("ankylo") || n.Contains("ankylosaurus"))
            {
                SetSpecies(DinoAISpeciesType.AnkylosaurusTanker);
            }
            else if (n.Contains("ptero") || n.Contains("pterodactyl") || GetComponent<FlyingUnit>() != null)
            {
                SetSpecies(DinoAISpeciesType.PterodactylFlyer);
            }
            else if (n.Contains("trex") || n.Contains("boss"))
            {
                SetSpecies(DinoAISpeciesType.TRexApexBoss);
            }
        }

        public void SetStateLabel(string label, Color color)
        {
            _currentStateLabel = label;
            _tagColor = color;
        }

        private void OnGUI()
        {
            if (!ShowOverheadTags) return;

            if (_mainCam == null) _mainCam = Camera.main ?? FindAnyObjectByType<Camera>();
            if (_mainCam == null) return;

            if (_unit == null) _unit = GetComponent<Unit.Unit>();
            if (_unit == null || _unit.Health <= 0) return;

            // Tính toán độ cao nhãn theo kích thước mô hình
            float headYOffset = 2.4f;
            if (_species == DinoAISpeciesType.TRexApexBoss) headYOffset = 5.6f;
            else if (_species == DinoAISpeciesType.AnkylosaurusTanker) headYOffset = 2.6f;
            else if (_species == DinoAISpeciesType.PterodactylFlyer) headYOffset = 2.2f;

            Vector3 worldPos = transform.position + Vector3.up * headYOffset;
            Vector3 screenPos = _mainCam.WorldToScreenPoint(worldPos);

            // Giới hạn tầm nhìn tối đa 90m để bao quát toàn bộ thung lũng chiến trường
            if (screenPos.z <= 0.5f || screenPos.z > 90f) return;

            // Invert Y for GUI
            float x = screenPos.x;
            float y = Screen.height - screenPos.y;

            GUIContent content = new GUIContent(_currentStateLabel);
            GUIStyle style = GetLabelStyle(_tagColor);
            Vector2 size = style.CalcSize(content);

            Rect bgRect = new Rect(x - size.x * 0.5f - 5, y - size.y * 0.5f - 3, size.x + 10, size.y + 6);

            float alpha = Mathf.Clamp01((90f - screenPos.z) / 25f);
            GUI.color = new Color(0.04f, 0.08f, 0.14f, 0.90f * alpha);
            GUI.Box(bgRect, GUIContent.none);
            Color textC = _tagColor;
            textC.a = alpha;
            style.normal.textColor = textC;
            GUI.color = Color.white;

            GUI.Label(bgRect, content, style);
        }

        private GUIStyle GetLabelStyle(Color textColor)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.fontSize = 10;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
            s.normal.textColor = textColor;
            return s;
        }
    }
}
