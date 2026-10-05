using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Utility;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    public abstract class Unit : MonoBehaviour, IDamageable
    {
        [field: SerializeField] public int MaxHealth { get; set; }
        [field: SerializeField] public int Health { get; set; }

        public Transform Transform => transform;
        [field: SerializeField] public UnitSO UnitType { get; set; }
        [SerializeField] protected AttackRadius AttackRadius;
        [SerializeField] protected HealthBar HealthBar;
        public event IDamageable.TakeDamageEvent OnTakeDamage;
        public event IDamageable.DeathEvent OnDeath;

        public NavMeshAgent Agent { get; protected set; }
        protected Animator Animator;
        protected Rigidbody Rigidbody;
        protected BehaviorGraphAgent GraphAgent;
        private float LastSpeed;

        protected virtual void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Animator = GetComponent<Animator>();
            GraphAgent = GetComponent<BehaviorGraphAgent>();
            Rigidbody = GetComponent<Rigidbody>();
        }

        protected virtual void Start()
        {
            if (GraphAgent != null)
            {
                GraphAgent.enabled = true;
            }

            if (Agent != null)
            {
                Agent.enabled = true;
            }

            if (AttackRadius != null)
            {
                AttackRadius.OnTargetEnter += OnTargetEnter;
                AttackRadius.OnTargetExit += OnTargetExit;
            }

            if (UnitType != null)
            {
                MaxHealth = UnitType.Health;
                Health = UnitType.Health;
            }

            EnsureHealthBarAttached();
        }

        public virtual void EnsureHealthBarAttached()
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
            if (HealthBar != null && MaxHealth > 0)
            {
                HealthBar.SetProgress((float)Health / MaxHealth);
            }
        }

        protected abstract void OnTargetEnter(IDamageable target);
        protected abstract void OnTargetExit(IDamageable target);

        public virtual void TakeDamage(int damage)
        {
            if (damage >= Health)
            {
                Health = 0;
                if (HealthBar != null)
                {
                    HealthBar.SetProgress((float)Health / MaxHealth);
                }
                RaiseDamageEvent(Health);
                RaiseDeathEvent();
                Die();
            }
            else
            {
                Health -= damage;
                if (HealthBar != null)
                {
                    HealthBar.SetProgress((float)Health / MaxHealth);
                }
                RaiseDamageEvent(damage);
            }
        }

        protected virtual void Update()
        {
            if (Animator == null)
            {
                Animator = GetComponentInChildren<Animator>();
            }

            if (Animator != null && Agent != null)
            {
                Animator.SetFloat(AnimationConstants.SPEED_PARAMETER, Agent.enabled ? Agent.velocity.magnitude : 0);
            }
        }

        public abstract void Die();

        protected virtual void OnDestroy()
        {
            if (AttackRadius != null)
            {
                AttackRadius.OnTargetEnter -= OnTargetEnter;
                AttackRadius.OnTargetExit -= OnTargetExit;
            }

            if (HealthBarCanvas.Instance != null)
            {
                HealthBarCanvas.Instance.Unregister(this);
            }
        }

        protected void RaiseDamageEvent(int damage) => OnTakeDamage?.Invoke(this, damage);
        protected void RaiseDeathEvent() => OnDeath?.Invoke(this);

        /// <summary>
        /// Bộ lọc mục tiêu chuẩn xác 100% giúp các tháp và bẫy KHÔNG BAO GIỜ bắn nhầm đồng minh (Friendly Fire).
        /// Loại trừ: Defender (lính NPC làng), Wall, Mọi loại tháp, Căn cứ/Trứng, Player.
        /// Chỉ chấp nhận: Khủng long / Quái vật thù địch còn sống.
        /// </summary>
        public static bool IsHostileMonster(Collider col)
        {
            if (col == null || !col.gameObject.activeInHierarchy) return false;

            // 1. LOẠI TRỪ 100% CÁC ĐỒNG MINH VÀ CÔNG TRÌNH PHE PHÒNG THỦ:
            if (col.GetComponentInParent<Defender>() != null || col.GetComponent<Defender>() != null) return false;
            if (col.GetComponentInParent<Wall>() != null || col.GetComponent<Wall>() != null) return false;
            if (col.GetComponentInParent<ArcherTower>() != null || col.GetComponent<ArcherTower>() != null) return false;
            if (col.GetComponentInParent<BallistaTower>() != null || col.GetComponent<BallistaTower>() != null) return false;
            if (col.GetComponentInParent<CatapultTower>() != null || col.GetComponent<CatapultTower>() != null) return false;
            if (col.GetComponentInParent<TeslaTower>() != null || col.GetComponent<TeslaTower>() != null) return false;
            if (col.GetComponentInParent<FrostTower>() != null || col.GetComponent<FrostTower>() != null) return false;
            if (col.GetComponentInParent<GroundTrap>() != null || col.GetComponent<GroundTrap>() != null) return false;

            // Loại trừ Người chơi và Căn cứ/Trứng
            if (col.CompareTag("Player") || col.name.Contains("Egg") || col.name.Contains("Base")) return false;

            // Kiểm tra IDamageable còn máu
            IDamageable d = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (d == null || d.Health <= 0) return false;

            // 2. XÁC NHẬN MỤC TIÊU LÀ KHỦNG LONG / QUÁI VẬT:
            if (col.GetComponentInParent<PrehistoricDinoBase>() != null || col.GetComponent<PrehistoricDinoBase>() != null) return true;
            if (col.GetComponentInParent<Dino>() != null || col.GetComponent<Dino>() != null) return true;
            if (col.GetComponentInParent<RunnerDino>() != null || col.GetComponent<RunnerDino>() != null) return true;
            if (col.GetComponentInParent<FlyingUnit>() != null || col.GetComponent<FlyingUnit>() != null) return true;
            if (col.GetComponentInParent<SiegeDino>() != null || col.GetComponent<SiegeDino>() != null) return true;
            if (col.GetComponentInParent<BossDino>() != null || col.GetComponent<BossDino>() != null) return true;

            // Kiểm tra theo danh sách quái đang hoạt động
            if (PrehistoricGameplayManager.Instance != null)
            {
                Unit u = col.GetComponent<Unit>() ?? col.GetComponentInParent<Unit>();
                if (u != null && PrehistoricGameplayManager.Instance.IsActiveMonster(u)) return true;
            }

            return false;
        }
    }
}
