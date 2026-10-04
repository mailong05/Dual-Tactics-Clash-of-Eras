using UnityEngine;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Image FillImage;
        [SerializeField] private Gradient Gradient;
        [field: SerializeField] public DeathBehavior OnDeathBehavior { get; private set; }
        [field: SerializeField] public Vector3 FollowOffset { get; set; } = new (0, 3, 0);

        [SerializeField] private Text NameLabel;
        private string _CurrentUnitName = "";

        public enum DeathBehavior
        {
            Disable,
            Destroy
        }

        private void Awake()
        {
            // Đảm bảo thanh máu luôn có tỉ lệ dương, tránh lật mặt polygon gây culling tàng hình
            transform.localScale = Vector3.one;

            foreach (var cr in GetComponentsInChildren<CanvasRenderer>(true))
            {
                cr.cullTransparentMesh = false;
            }
        }

        public void SetUnitName(string unitName, Color? customColor = null)
        {
            _CurrentUnitName = unitName;
            EnsureNameLabel();
            if (NameLabel != null)
            {
                NameLabel.text = unitName;
                if (customColor.HasValue)
                {
                    NameLabel.color = customColor.Value;
                }
            }
        }

        public void EnsureNameLabel()
        {
            if (NameLabel != null) return;

            NameLabel = GetComponentInChildren<Text>();
            if (NameLabel != null) return;

            GameObject labelObj = new GameObject("UnitNameLabel");
            labelObj.transform.SetParent(transform, false);

            CanvasRenderer cr = labelObj.AddComponent<CanvasRenderer>();
            cr.cullTransparentMesh = false;

            NameLabel = labelObj.AddComponent<Text>();

            // Tải font dự phòng đa tầng đảm bảo không bao giờ bị null
            Font font = Resources.Load<Font>("DefaultFont");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 24); } catch { }
            }
            if (font != null) NameLabel.font = font;

            NameLabel.fontSize = 24;
            NameLabel.fontStyle = FontStyle.Bold;
            NameLabel.alignment = TextAnchor.MiddleCenter;
            NameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            NameLabel.verticalOverflow = VerticalWrapMode.Overflow;
            NameLabel.color = Color.white;
            NameLabel.raycastTarget = false;

            // Đổ bóng và viền chữ đen đậm nét giúp đọc rõ từ mọi góc nhìn 3D
            Outline outline = labelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Căn chỉnh vị trí chữ nổi lên trên thanh máu chuẩn xác
            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 60f);
            rect.anchoredPosition = new Vector2(0f, 1.4f);
            rect.localScale = new Vector3(0.007f, 0.007f, 1f);
        }

        public void SetProgress(float progress)
        {
            if (FillImage == null)
            {
                FillImage = GetComponentInChildren<Image>();
                if (FillImage == null) return;
            }

            FillImage.fillAmount = Mathf.Clamp01(progress);

            if (Gradient != null)
            {
                Color evalColor = Gradient.Evaluate(FillImage.fillAmount);
                if (evalColor.a < 0.2f) evalColor.a = 1.0f; // Đảm bảo không bị trong suốt
                FillImage.color = evalColor;
            }
            else
            {
                // Fallback: Xanh lá -> Vàng -> Đỏ
                float p = FillImage.fillAmount;
                FillImage.color = p > 0.5f
                    ? Color.Lerp(Color.yellow, Color.green, (p - 0.5f) * 2f)
                    : Color.Lerp(Color.red, Color.yellow, p * 2f);
            }
        }
    }
}
