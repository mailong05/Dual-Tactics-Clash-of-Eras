using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class BallistaTower : Unit
    {
        [Header("Ballista Mechanics")]
        [SerializeField] private float AttackRange = 38.0f;
        [SerializeField] private float FireInterval = 2.2f;
        [SerializeField] private int SpearDamage = 70;
        public void SetDamage(int dmg) => SpearDamage = dmg;
        [SerializeField] private float SpearSpeed = 28.0f;
        [SerializeField] private int PierceCount = 2;
        [SerializeField] private LayerMask EnemyLayers;

        [Header("Procedural Rig")]
        [SerializeField] private Transform TurretHead;        // Aims at target (Pitch & Yaw)
        [SerializeField] private Transform BowString;         // Stretches back during reload
        [SerializeField] private Transform SpearLaunchPoint;
        [SerializeField] private GameObject SpearPrefab;

        private float LastFireTime;
        private Collider[] OverlapBuffer = new Collider[64];
        private bool IsFiring = false;

        protected override void Start()
        {
            base.Start();
            AttackRange = Mathf.Max(AttackRange, 38.0f);
            if (EnemyLayers.value == 0)
            {
                EnemyLayers = (1 << 8) | (1 << 7) | LayerMask.GetMask("Dinos", "Default");
            }
            if (TurretHead == null) TurretHead = transform;
            if (SpearLaunchPoint == null) SpearLaunchPoint = TurretHead;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsCombatActive()) return;
            if (IsFiring) return;

            Collider target = FindPriorityTarget();
            if (target != null)
            {
                Vector3 aimPos = target.bounds.center;
                Vector3 dir = (aimPos - TurretHead.position).normalized;
                if (dir != Vector3.zero)
                {
                    TurretHead.rotation = Quaternion.Slerp(TurretHead.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                }

                if (Time.time >= LastFireTime + FireInterval)
                {
                    StartCoroutine(FireSequence(aimPos));
                }
            }
        }

        private Collider FindPriorityTarget()
        {
            Vector3 basePos = PrehistoricVillageBase.Instance != null 
                ? PrehistoricVillageBase.Instance.transform.position 
                : new Vector3(-2.23f, 0f, -38.26f);

            Collider priorityTarget = null;
            float bestScore = float.MinValue;

            // 1. Quét ActiveDinos (ưu tiên quái máu trâu/Boss đang tiến sát nhà chính)
            if (PrehistoricDinoBase.ActiveDinos != null)
            {
                foreach (var dino in PrehistoricDinoBase.ActiveDinos)
                {
                    if (dino == null || dino.Health <= 0 || !dino.gameObject.activeInHierarchy) continue;
                    float distToTower = Vector3.Distance(transform.position, dino.transform.position);
                    if (distToTower > AttackRange) continue;

                    Collider col = dino.GetComponent<Collider>() ?? dino.GetComponentInChildren<Collider>();
                    if (col == null || !IsHostileMonster(col)) continue;

                    float distToBase = Vector3.Distance(dino.transform.position, basePos);
                    float score = (dino.Health * 0.1f) + (1500f - distToBase * 12f) - distToTower;
                    if (dino.HasReachedBase) score += 500f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        priorityTarget = col;
                    }
                }
            }

            // 2. Quét FlyingUnit
            if (FlyingUnit.ActiveFlyers != null)
            {
                foreach (var fu in FlyingUnit.ActiveFlyers)
                {
                    if (fu == null || fu.Health <= 0 || !fu.gameObject.activeInHierarchy) continue;
                    float distToTower = Vector3.Distance(transform.position, fu.transform.position);
                    if (distToTower > AttackRange) continue;

                    Collider col = fu.GetComponent<Collider>() ?? fu.GetComponentInChildren<Collider>();
                    if (col == null || !IsHostileMonster(col)) continue;

                    float distToBase = Vector3.Distance(fu.transform.position, basePos);
                    float score = (fu.Health * 0.1f) + (1600f - distToBase * 12f) - distToTower;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        priorityTarget = col;
                    }
                }
            }

            if (priorityTarget != null) return priorityTarget;

            // 3. Fallback PhysX OverlapSphere
            int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == null) continue;
                if (!IsHostileMonster(OverlapBuffer[i])) continue;

                float distToTower = Vector3.Distance(transform.position, OverlapBuffer[i].transform.position);
                float distToBase = Vector3.Distance(OverlapBuffer[i].transform.position, basePos);
                int hp = 100;
                if (OverlapBuffer[i].TryGetComponent(out Unit enemyUnit)) hp = enemyUnit.Health;
                float score = (hp * 0.1f) + (1000f - distToBase * 10f) - distToTower;

                if (score > bestScore)
                {
                    bestScore = score;
                    priorityTarget = OverlapBuffer[i];
                }
            }
            return priorityTarget;
        }

        private IEnumerator FireSequence(Vector3 targetCenter)
        {
            IsFiring = true;
            LastFireTime = Time.time;

            // Pull string back
            if (BowString != null)
            {
                Vector3 originalStringPos = BowString.localPosition;
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 4f;
                    BowString.localPosition = originalStringPos + Vector3.back * 0.4f * t;
                    yield return null;
                }

                // Snap string forward (Shoot)
                BowString.localPosition = originalStringPos;
            }

            // Launch Spear
            Vector3 fireOrigin = SpearLaunchPoint.position;
            Vector3 direction = (targetCenter - fireOrigin).normalized;

            StartCoroutine(SimulateSpearProjectile(fireOrigin, direction));

            yield return new WaitForSeconds(0.2f);
            IsFiring = false;
        }

        private IEnumerator SimulateSpearProjectile(Vector3 origin, Vector3 direction)
        {
            GameObject spear = null;
            if (SpearPrefab != null)
            {
                spear = Instantiate(SpearPrefab, origin, Quaternion.LookRotation(direction));
            }
            else
            {
                spear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spear.name = "Giant_Spear";
                spear.transform.localScale = new Vector3(0.08f, 0.8f, 0.08f);
                spear.transform.position = origin;
                spear.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);
                spear.GetComponent<Renderer>().sharedMaterial = Rendering.PrehistoricShaderUtility.CreateSafeMaterial(new Color(0.38f, 0.22f, 0.10f), 0.2f);
                Destroy(spear.GetComponent<Collider>());
            }

            if (spear != null) Destroy(spear, 5.0f);

            float maxTravelDist = AttackRange + 5f;
            float traveled = 0f;
            int remainingPierces = PierceCount;
            HashSet<Collider> piercedTargets = new();

            while (traveled < maxTravelDist && remainingPierces > 0)
            {
                float step = SpearSpeed * Time.deltaTime;
                Vector3 nextPos = spear.transform.position + direction * step;

                // Raycast along step to hit enemies
                RaycastHit[] hits = Physics.RaycastAll(spear.transform.position, direction, step, EnemyLayers);
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider != null && !piercedTargets.Contains(hit.collider) && IsHostileMonster(hit.collider))
                    {
                        piercedTargets.Add(hit.collider);
                        if (hit.collider.TryGetComponent(out IDamageable damageable))
                        {
                            damageable.TakeDamage(SpearDamage);
                        }
                        remainingPierces--;
                        if (remainingPierces <= 0) break;
                    }
                }

                spear.transform.position = nextPos;
                traveled += step;
                yield return null;
            }

            if (spear != null) Destroy(spear);
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}
        public override void Die()
        {
            Destroy(gameObject);
        }
    }
}
