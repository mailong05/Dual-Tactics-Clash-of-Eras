using System;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Utility;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    /// <summary>
    /// Nhà chính / Căn cứ làng bộ lạc trong chế độ Khủng Long Công Thành (Dino Assault).
    /// Khủng long sẽ tấn công căn cứ này; khi HP về 0, người chơi giành chiến thắng (Chiếm thành thành công).
    /// </summary>
    public class PrehistoricVillageBase : Unit
    {
        public static PrehistoricVillageBase Instance { get; private set; }

        public event Action<int, int> OnBaseHealthChanged;
        public event Action OnBaseDestroyed;

        [Header("Village Base Configuration")]
        [SerializeField] private float HealthBarHeight = 3.8f;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            if (MaxHealth <= 0)
            {
                MaxHealth = 1000;
                Health = 1000;
            }
        }

        protected override void Start()
        {
            base.Start();
            EnsureHealthBarAttached();
        }

        public void SetBaseStats(int maxHp)
        {
            MaxHealth = maxHp;
            Health = maxHp;
            if (HealthBar != null)
            {
                HealthBar.SetProgress(1.0f);
            }
            OnBaseHealthChanged?.Invoke(Health, MaxHealth);
        }

        public void EnsureHealthBarAttached()
        {
            if (HealthBar == null)
            {
                HealthBar = GetComponentInChildren<HealthBar>();
            }

            if (HealthBar == null && HealthBarCanvas.Instance != null)
            {
                HealthBar = HealthBarCanvas.Instance.CreateHealthBarForUnit(this);
            }
            else if (HealthBar != null && HealthBarCanvas.Instance != null)
            {
                HealthBarCanvas.Instance.Register(HealthBar, this);
            }

            if (HealthBar != null)
            {
                HealthBar.FollowOffset = new Vector3(0f, HealthBarHeight, 0f);
                HealthBar.SetUnitName("🏛️ CĂN CỨ LÀNG BỘ LẠC", new Color(1f, 0.85f, 0.2f));
                HealthBar.SetProgress((float)Health / Mathf.Max(1, MaxHealth));
            }
        }

        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);
            OnBaseHealthChanged?.Invoke(Health, MaxHealth);
            if (HealthBar != null)
            {
                HealthBar.SetProgress((float)Health / Mathf.Max(1, MaxHealth));
            }

            if (Health <= 0)
            {
                Die();
            }
        }

        public override void Die()
        {
            Debug.Log("<color=red>[Village Base]</color> Nhà chính làng bộ lạc đã bị san bằng hoàn toàn!");
            OnBaseDestroyed?.Invoke();
            gameObject.SetActive(false);
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}
    }
}
