using System;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using UnityEngine;

namespace LlamAcademy.Dinos.Utility
{
    [RequireComponent(typeof(Canvas))]
    public class HealthBarCanvas : MonoBehaviour
    {
        private static HealthBarCanvas _Instance;
        public static HealthBarCanvas Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = FindFirstObjectByType<HealthBarCanvas>();
                    if (_Instance == null)
                    {
                        GameObject canvasObj = new GameObject("HealthBarCanvas");
                        Canvas canvas = canvasObj.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.WorldSpace;
                        _Instance = canvasObj.AddComponent<HealthBarCanvas>();
                    }
                }
                return _Instance;
            }
            private set => _Instance = value;
        }

        private Dictionary<Unit.Unit, HealthBar> HealthBars = new();

        private Canvas _canvas;

        private void Awake()
        {
            if (_Instance != null && _Instance != this)
            {
                Debug.LogWarning($"Multiple Health Bar Canvases in scene! Destroying duplicate ({name})!");
                Destroy(gameObject);
                return;
            }

            _Instance = this;
            _canvas = GetComponent<Canvas>();
            if (_canvas != null && _canvas.worldCamera == null)
            {
                _canvas.worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (_canvas != null && _canvas.worldCamera == null && cam != null)
            {
                _canvas.worldCamera = cam;
            }
            Quaternion camRot = cam != null ? cam.transform.rotation : Quaternion.identity;

            bool missedCleaningAUnit = false;
            foreach (KeyValuePair<Unit.Unit, HealthBar> keyValuePair in HealthBars)
            {
                if (keyValuePair.Key == null)
                {
                    missedCleaningAUnit = true;
                }
                else if (keyValuePair.Value != null)
                {
                    keyValuePair.Value.transform.position = keyValuePair.Key.Transform.position + keyValuePair.Value.FollowOffset;
                    if (cam != null)
                    {
                        keyValuePair.Value.transform.rotation = camRot;
                    }
                }
            }

            if (missedCleaningAUnit)
            {
                IEnumerable<KeyValuePair<Unit.Unit, HealthBar>> keyValuePairs = HealthBars.Where((kvp) => kvp.Key == null).ToArray();
                for (int i = keyValuePairs.Count() - 1; i >= 0; i--)
                {
                    var kvp = keyValuePairs.ElementAt(i);
                    if (kvp.Value != null && kvp.Value.gameObject != null)
                    {
                        Destroy(kvp.Value.gameObject);
                    }

                    HealthBars.Remove(kvp.Key);
                }
            }
        }

        public void Register(HealthBar healthBar, Unit.Unit unit)
        {
            if (unit == null || healthBar == null) return;

            if (HealthBars.ContainsKey(unit))
            {
                HealthBars[unit] = healthBar;
            }
            else
            {
                HealthBars.Add(unit, healthBar);
            }

            healthBar.transform.SetParent(transform);
            healthBar.transform.localRotation = Quaternion.identity;

            // Thiết lập tên hiển thị của đối tượng trên thanh máu
            string displayName = GetUnitDisplayName(unit);
            Collider unitCol = unit.GetComponentInChildren<Collider>();
            bool isHostile = unitCol != null && Unit.Unit.IsHostileMonster(unitCol);
            Color nameColor = isHostile ? new Color(1f, 0.45f, 0.35f) : new Color(0.45f, 1f, 0.55f);
            if (displayName.Contains("T-Rex"))
            {
                nameColor = new Color(1f, 0.85f, 0.2f);
            }
            healthBar.SetUnitName(displayName, nameColor);

            unit.OnDeath -= HandleUnitDeath;
            unit.OnDeath += HandleUnitDeath;
        }

        public static string GetUnitDisplayName(Unit.Unit unit)
        {
            if (unit == null) return "";

            if (unit.UnitType is Config.TowerSO tower && !string.IsNullOrEmpty(tower.DisplayName))
            {
                return tower.DisplayName;
            }

            if (unit is Enemy.Defender def)
            {
                return def.Role == Enemy.DefenderRole.Archer ? "Cung Thủ Làng" : "Chiến Binh Phóng Giáo";
            }

            if (unit is ArcherTower) return "Chòi Cung Thủ Gỗ";
            if (unit is BallistaTower) return "Tháp Nỏ Khổng Lồ";
            if (unit is CatapultTower) return "Máy Bắn Đá";
            if (unit is TeslaTower) return "Trụ Sấm Sét";
            if (unit is FrostTower) return "Tháp Băng Tiền Sử";
            if (unit is Wall) return "Rào Cọc Gỗ";

            string lower = unit.gameObject.name.ToLower();
            if (lower.Contains("trex") || lower.Contains("t-rex")) return "👑 T-Rex Bạo Chúa";
            if (lower.Contains("ptero")) return "🦅 Thằn Lằn Bay";
            if (lower.Contains("ankyl")) return "🛡️ Khủng Long Thiết Giáp";
            if (lower.Contains("raptor")) return "🦖 Velociraptor";
            if (lower.Contains("mud") || lower.Contains("tar")) return "Vũng Lầy Tiền Sử";
            if (lower.Contains("spike")) return "Bẫy Chông Độc";
            if (lower.Contains("egg") || lower.Contains("base") || lower.Contains("village")) return "Trứng Rồng Căn Cứ";

            if (unit.UnitType != null && !string.IsNullOrEmpty(unit.UnitType.name))
            {
                return unit.UnitType.name.Replace("Unit_", "").Replace("Dino_", "").Replace("Tower_", "");
            }

            return unit.gameObject.name.Replace("(Clone)", "").Trim();
        }

        public void Unregister(Unit.Unit unit)
        {
            if (unit == null) return;

            unit.OnDeath -= HandleUnitDeath;
            if (HealthBars.Remove(unit, out HealthBar healthBar))
            {
                if (healthBar != null && healthBar.gameObject != null)
                {
                    if (healthBar.OnDeathBehavior == HealthBar.DeathBehavior.Disable)
                    {
                        healthBar.gameObject.SetActive(false);
                    }
                    else
                    {
                        Destroy(healthBar.gameObject);
                    }
                }
            }
        }

        private void HandleUnitDeath(IDamageable diedobject)
        {
            if (diedobject != null && diedobject.Transform != null)
            {
                Unit.Unit unit = diedobject.Transform.GetComponent<Unit.Unit>();
                Unregister(unit);
            }
        }
    }
}
