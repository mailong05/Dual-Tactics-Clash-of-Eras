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

        private void Awake()
        {
            if (_Instance != null && _Instance != this)
            {
                Debug.LogWarning($"Multiple Health Bar Canvases in scene! Destroying duplicate ({name})!");
                Destroy(gameObject);
                return;
            }

            _Instance = this;
        }

        private void Update()
        {
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
            unit.OnDeath -= HandleUnitDeath;
            unit.OnDeath += HandleUnitDeath;
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
