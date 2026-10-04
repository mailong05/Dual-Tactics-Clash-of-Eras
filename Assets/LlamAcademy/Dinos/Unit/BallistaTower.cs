using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class BallistaTower : Unit
    {
        [Header("Ballista Mechanics")]
        [SerializeField] private float AttackRange = 16.0f;
        [SerializeField] private float FireInterval = 2.2f;
        [SerializeField] private int SpearDamage = 70;
        [SerializeField] private float SpearSpeed = 28.0f;
        [SerializeField] private int PierceCount = 2;
        [SerializeField] private LayerMask EnemyLayers;

        [Header("Procedural Rig")]
        [SerializeField] private Transform TurretHead;        // Aims at target (Pitch & Yaw)
        [SerializeField] private Transform BowString;         // Stretches back during reload
        [SerializeField] private Transform SpearLaunchPoint;
        [SerializeField] private GameObject SpearPrefab;

        private float LastFireTime;
        private Collider[] OverlapBuffer = new Collider[20];
        private bool IsFiring = false;

        protected override void Start()
        {
            base.Start();
            if (EnemyLayers.value == 0) EnemyLayers = ~LayerMask.GetMask("Ignore Raycast");
            if (TurretHead == null) TurretHead = transform;
            if (SpearLaunchPoint == null) SpearLaunchPoint = TurretHead;
        }

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;
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
            int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
            Collider priorityTarget = null;
            float highestHp = -1f;

            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == null) continue;
                if (!IsHostileMonster(OverlapBuffer[i])) continue;

                if (OverlapBuffer[i].TryGetComponent(out Unit enemyUnit))
                {
                    // Ballista prioritizes high-health armored targets or boss units
                    if (enemyUnit.Health > highestHp)
                    {
                        highestHp = enemyUnit.Health;
                        priorityTarget = OverlapBuffer[i];
                    }
                }
                else
                {
                    if (priorityTarget == null) priorityTarget = OverlapBuffer[i];
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
                spear.GetComponent<Renderer>().material.color = new Color(0.4f, 0.25f, 0.15f);
                Destroy(spear.GetComponent<Collider>());
            }

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
