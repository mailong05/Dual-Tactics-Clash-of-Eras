using UnityEngine;
using UnityEngine.UI;

namespace LlamAcademy.Dinos.UI
{
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private Image FillImage;
        [SerializeField] private Gradient Gradient;
        [field: SerializeField] public DeathBehavior OnDeathBehavior { get; private set; }
        [field: SerializeField] public Vector3 FollowOffset { get; private set; }= new (0, 3, 0);

        [SerializeField] private Text NameLabel;
        private string _CurrentUnitName = "";

        public enum DeathBehavior
        {
            Disable,
            Destroy
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

            // CanvasRenderer bắt buộc phải có để render UI trên world-space Canvas
            if (!labelObj.TryGetComponent<CanvasRenderer>(out _))
                labelObj.AddComponent<CanvasRenderer>();

            NameLabel = labelObj.AddComponent<Text>();

            // Tải font từ Resources hoặc tạo dynamic font từ OS
            Font font = Resources.Load<Font>("DefaultFont");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 22); } catch { }
            }
            if (font != null) NameLabel.font = font;

            NameLabel.fontSize = 20;
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

            // QUAN TRỌNG: Trong world-space Canvas, 1 unit sizeDelta = 1 MÉT thực!
            // → localScale rất nhỏ (0.005) để chuyển pixel → mét hợp lý
            // → fontSize 20 * 0.005 = 0.1m (10cm) mỗi ký tự — đọc rõ từ camera TD
            // → sizeDelta (400, 50) * 0.005 = 2m × 0.25m vùng hiển thị
            // → Parent HealthBar có localScale.y = -1 (lật trục Y)
            //   nên child cần Y âm để bù: -1 * -0.005 = +0.005 (chữ đúng chiều)
            RectTransform rect = labelObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, 50f);
            rect.anchoredPosition = new Vector2(0f, -1.4f);
            rect.localScale = new Vector3(0.005f, -0.005f, 1f);
        }

        public void SetProgress(float progress)
        {
            FillImage.fillAmount = Mathf.Clamp01(progress);
            FillImage.color = Gradient.Evaluate(FillImage.fillAmount);
        }
    }
}
