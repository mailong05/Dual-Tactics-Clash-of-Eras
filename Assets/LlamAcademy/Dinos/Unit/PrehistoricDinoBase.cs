using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class PrehistoricDinoBase : Unit
    {
        [Header("Prehistoric Dino Stats")]
        [SerializeField] protected float MoveSpeed = 3.5f;
        [SerializeField] protected float AttackRange = 2.2f;
        [SerializeField] protected float AttackInterval = 1.2f;
        [SerializeField] protected int AttackDamage = 18;
        [SerializeField] protected float BuildingDamageMultiplier = 1.0f;
        [SerializeField] protected int GoldReward = 15;
        [SerializeField] protected LayerMask TargetLayers;

        protected Transform TargetBase;
        protected float LastAttackTime;
        protected IDamageable CurrentTarget;
        protected Collider[] ScanBuffer = new Collider[15];

        protected override void Awake()
        {
            base.Awake();
            if (Agent == null) Agent = GetComponent<NavMeshAgent>();
        }

        protected override void Start()
        {
            base.Start();

            if (TargetLayers.value == 0) TargetLayers = ~LayerMask.GetMask("Ignore Raycast");

            if (Agent != null)
            {
                Agent.speed = MoveSpeed;
                Agent.stoppingDistance = AttackRange * 0.8f;
            }

            FindTargetBase();

            if (TargetBase != null && Agent != null && Agent.isOnNavMesh)
            {
                Agent.SetDestination(TargetBase.position);
            }
        }

        public void SetDestination(Vector3 destination)
        {
            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.isStopped = false;
                Agent.SetDestination(destination);
            }
        }

        protected virtual void FindTargetBase()
        {
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
            {
                TargetBase = RoundManager.Instance.DinoTarget;
                return;
            }

            GameObject eggObj = GameObject.Find("Dino Egg Spawn");
            if (eggObj != null)
            {
                TargetBase = eggObj.transform;
                return;
            }

            GameObject eggTag = GameObject.FindWithTag("Finish");
            if (eggTag != null)
            {
                TargetBase = eggTag.transform;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running)
            {
                if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;
                return;
            }

            if (TargetBase == null)
            {
                FindTargetBase();
                if (TargetBase == null) return;
            }

            // Check if current target is valid
            if (CurrentTarget != null && CurrentTarget.Health > 0)
            {
                float distToTarget = Vector3.Distance(transform.position, CurrentTarget.Transform.position);
                if (distToTarget <= AttackRange + 0.5f)
                {
                    if (Agent != null && Agent.isOnNavMesh) Agent.isStopped = true;

                    // Face target
                    Vector3 look = (CurrentTarget.Transform.position - transform.position);
                    look.y = 0;
                    if (look != Vector3.zero) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);

                    if (Time.time >= LastAttackTime + AttackInterval)
                    {
                        LastAttackTime = Time.time;
                        PerformAttack(CurrentTarget);
                    }
                    return;
                }
            }

            // Look for blocking walls or defenders in path
            CurrentTarget = ScanForBlockers();

            if (CurrentTarget == null)
            {
                // Resume movement to base
                if (Agent != null && Agent.isOnNavMesh)
                {
                    Agent.isStopped = false;
                    Agent.SetDestination(TargetBase.position);
                }

                // Check if reached base
                float distToBase = Vector3.Distance(transform.position, TargetBase.position);
                if (distToBase <= AttackRange + 1.2f)
                {
                    if (PrehistoricGameplayManager.Instance != null)
                    {
                        PrehistoricGameplayManager.Instance.OnDinoReachedBase(this, AttackDamage);
                        return;
                    }
                    if (TargetBase.TryGetComponent(out IDamageable baseDamageable))
                    {
                        CurrentTarget = baseDamageable;
                    }
                }
            }
        }

        protected virtual IDamageable ScanForBlockers()
        {
            int hits = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, ScanBuffer, TargetLayers);
            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                if (col.TryGetComponent(out Wall wall) && wall.Health > 0)
                {
                    return wall;
                }
                if (col.TryGetComponent(out Unit tower) && !(tower is PrehistoricDinoBase) && tower.Health > 0)
                {
                    return tower;
                }
            }
            return null;
        }

        protected virtual void PerformAttack(IDamageable target)
        {
            int damage = target is Wall ? Mathf.CeilToInt(AttackDamage * BuildingDamageMultiplier) : AttackDamage;
            target.TakeDamage(damage);

            StartCoroutine(AttackLungeRoutine());
        }

        protected virtual IEnumerator AttackLungeRoutine()
        {
            Vector3 startPos = transform.position;
            Vector3 forward = transform.forward * 0.4f;

            float t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, startPos + forward, t / 0.15f);
                yield return null;
            }

            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos + forward, startPos, t / 0.2f);
                yield return null;
            }
            transform.position = startPos;
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}

        public override void Die()
        {
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.HandleMonsterDeath(this);
            }
            else if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(GoldReward);
            }

            RaiseDeathEvent();
            Destroy(gameObject, 0.1f);
        }
    }
}
