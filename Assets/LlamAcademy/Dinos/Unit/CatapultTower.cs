using System.Collections;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class CatapultTower : Unit
    {
        [Header("Catapult Mechanics")]
        [SerializeField] private float AttackRange = 14.0f;
        [SerializeField] private float MinRange = 3.5f;
        [SerializeField] private float FireInterval = 3.0f;
        [SerializeField] private int ExplosionDamage = 50;
        [SerializeField] private float ExplosionRadius = 4.0f;
        [SerializeField] private LayerMask EnemyLayers;

        [Header("Procedural Animation Rig")]
        [SerializeField] private Transform TurretBase;       // Rotates horizontally toward target
        [SerializeField] private Transform ThrowingArm;     // Rotates vertically to launch rock
        [SerializeField] private Transform LaunchPoint;     // Point where rock spawns
        [SerializeField] private float RestAngleX = 0f;
        [SerializeField] private float LaunchAngleX = -65f;
        [SerializeField] private GameObject BoulderPrefab;
        [SerializeField] private ParticleSystem ImpactExplosionVFX;

        private float LastFireTime;
        private Collider[] OverlapBuffer = new Collider[25];
        private bool IsLaunching = false;

        protected override void Start()
        {
            base.Start();
            if (EnemyLayers.value == 0) EnemyLayers = ~LayerMask.GetMask("Ignore Raycast");
            if (TurretBase == null) TurretBase = transform;
            if (LaunchPoint == null) LaunchPoint = ThrowingArm != null ? ThrowingArm : transform;
        }

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;
            if (IsLaunching) return;

            Collider target = FindBestTarget();
            if (target != null)
            {
                // Smoothly aim TurretBase horizontally
                Vector3 lookDir = (target.transform.position - TurretBase.position);
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    TurretBase.rotation = Quaternion.Slerp(TurretBase.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 4f);
                }

                if (Time.time >= LastFireTime + FireInterval)
                {
                    StartCoroutine(LaunchSequence(target.bounds.center));
                }
            }
        }

        private Collider FindBestTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
            Collider bestTarget = null;
            float maxScore = -1f;

            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == null) continue;
                if (!IsEnemyTarget(OverlapBuffer[i])) continue;
                float dist = Vector3.Distance(transform.position, OverlapBuffer[i].transform.position);
                if (dist < MinRange) continue; // Inside deadzone

                // Prefer targets with many neighbors (density clustering)
                int neighbors = Physics.OverlapSphereNonAlloc(OverlapBuffer[i].transform.position, ExplosionRadius, new Collider[10], EnemyLayers);
                float score = neighbors * 10f - dist;
                if (score > maxScore)
                {
                    maxScore = score;
                    bestTarget = OverlapBuffer[i];
                }
            }

            return bestTarget;
        }

        private bool IsEnemyTarget(Collider col)
        {
            if (col == null || !col.gameObject.activeInHierarchy) return false;
            if (col.TryGetComponent(out Unit u))
            {
                if (u is Wall || u is ArcherTower || u is BallistaTower || u is CatapultTower 
                    || u is TeslaTower || u is FrostTower || u is GroundTrap)
                {
                    return false;
                }
            }
            return col.TryGetComponent(out IDamageable d) && d.Health > 0;
        }

        private IEnumerator LaunchSequence(Vector3 targetPosition)
        {
            IsLaunching = true;
            LastFireTime = Time.time;

            // Step 1: Wind-up pullback slightly
            if (ThrowingArm != null)
            {
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 5f;
                    ThrowingArm.localRotation = Quaternion.Euler(Mathf.Lerp(RestAngleX, RestAngleX + 15f, t), 0, 0);
                    yield return null;
                }

                // Step 2: Rapid violent forward swing (Launch)
                t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 12f;
                    ThrowingArm.localRotation = Quaternion.Euler(Mathf.Lerp(RestAngleX + 15f, LaunchAngleX, t), 0, 0);
                    yield return null;
                }
            }

            // Step 3: Spawn and throw boulder along parabolic arc
            StartCoroutine(SimulateBoulderFlight(LaunchPoint.position, targetPosition));

            // Step 4: Elastic Recoil bounce at the stop bar
            if (ThrowingArm != null)
            {
                float recoilAngle = LaunchAngleX + 12f;
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 10f;
                    ThrowingArm.localRotation = Quaternion.Euler(Mathf.Lerp(LaunchAngleX, recoilAngle, t), 0, 0);
                    yield return null;
                }

                // Step 5: Smooth reset back to rest position
                t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 2.5f;
                    ThrowingArm.localRotation = Quaternion.Euler(Mathf.Lerp(recoilAngle, RestAngleX, t), 0, 0);
                    yield return null;
                }

                ThrowingArm.localRotation = Quaternion.Euler(RestAngleX, 0, 0);
            }

            IsLaunching = false;
        }

        private IEnumerator SimulateBoulderFlight(Vector3 start, Vector3 target)
        {
            GameObject boulder = null;
            if (BoulderPrefab != null)
            {
                boulder = Instantiate(BoulderPrefab, start, Quaternion.identity);
            }
            else
            {
                boulder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                boulder.name = "Flaming_Boulder";
                boulder.transform.localScale = Vector3.one * 0.7f;
                boulder.GetComponent<Renderer>().material.color = new Color(0.9f, 0.4f, 0.1f);
                Destroy(boulder.GetComponent<Collider>());
            }

            float flightTime = 1.0f;
            float elapsed = 0f;
            float arcHeight = Mathf.Max(3f, Vector3.Distance(start, target) * 0.35f);

            while (elapsed < flightTime)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / flightTime;

                // Parabolic trajectory formula: base lerp + sin curve arc
                Vector3 currentPos = Vector3.Lerp(start, target, progress);
                currentPos.y += Mathf.Sin(progress * Mathf.PI) * arcHeight;

                if (boulder != null)
                {
                    boulder.transform.position = currentPos;
                    boulder.transform.Rotate(Vector3.right, 360f * Time.deltaTime * 2f);
                }

                yield return null;
            }

            // Explosion on impact
            if (boulder != null)
            {
                Vector3 impactPos = boulder.transform.position;
                Destroy(boulder);

                if (ImpactExplosionVFX != null)
                {
                    Instantiate(ImpactExplosionVFX, impactPos, Quaternion.identity);
                }

                int hitCount = Physics.OverlapSphereNonAlloc(impactPos, ExplosionRadius, OverlapBuffer, EnemyLayers);
                for (int i = 0; i < hitCount; i++)
                {
                    if (OverlapBuffer[i] == null) continue;
                    if (!IsEnemyTarget(OverlapBuffer[i])) continue;
                    if (OverlapBuffer[i].TryGetComponent(out IDamageable damageable))
                    {
                        float dist = Vector3.Distance(impactPos, OverlapBuffer[i].transform.position);
                        float falloff = Mathf.Clamp01(1f - (dist / ExplosionRadius));
                        int finalDamage = Mathf.CeilToInt(ExplosionDamage * (0.5f + 0.5f * falloff));
                        damageable.TakeDamage(finalDamage);
                    }
                }
            }
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}
        public override void Die()
        {
            Destroy(gameObject);
        }
    }
}
