using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class SiegeDino : PrehistoricDinoBase
    {
        [Header("Siege Breaker Specialization")]
        [SerializeField] private float WallDetectRadius = 4.5f;

        protected override void Awake()
        {
            MoveSpeed = 3.0f;
            base.Awake();
            ConfigureNavMeshAgent();
            BuildingDamageMultiplier = 3.5f;
            AttackDamage = 25;
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 1275;
            Health = MaxHealth;
            GoldReward = 40;
        }

        protected override IDamageable ScanForBlockers()
        {
            if (_IsNavigatingBreach) return null;

            // If path is pending or complete through a breach, navigate without stopping!
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh && (Agent.pathPending || (Agent.hasPath && Agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete)))
            {
                return base.ScanForBlockers();
            }

            // Nếu đã có khe hở gần đây, tuân thủ theo base để điều hướng qua khe hở thay vì đập thêm tường!
            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null && TargetBase != null)
            {
                var nearbyBreach = LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.GetBestBreachTowards(transform.position, TargetBase.position, 20.0f);
                if (nearbyBreach != null)
                {
                    return base.ScanForBlockers();
                }
            }

            // When path is blocked (PathPartial), prioritize walls in front using WallDetectRadius
            int hits = Physics.OverlapSphereNonAlloc(transform.position, WallDetectRadius, ScanBuffer, TargetLayers);
            Vector3 targetDir = TargetBase != null ? (TargetBase.position - transform.position).normalized : transform.forward;
            IDamageable bestWall = null;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                Collider col = ScanBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;

                Wall wall = col.GetComponentInParent<Wall>();
                if (wall != null && wall.Health > 0)
                {
                    // Bỏ qua tường nằm sát khe hở đã mở
                    if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null && LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.IsWallNearActiveBreach(wall.transform.position, 7.5f))
                    {
                        continue;
                    }

                    Vector3 closest;
                    try { closest = col.ClosestPoint(transform.position); }
                    catch { closest = col.bounds.ClosestPoint(transform.position); }

                    Vector3 dir = (closest - transform.position).normalized;
                    if (Vector3.Dot(targetDir, dir) > -0.2f)
                    {
                        float d = Vector3.Distance(transform.position, closest);
                        if (d < minDistance)
                        {
                            minDistance = d;
                            bestWall = wall;
                        }
                    }
                }
            }

            return bestWall != null ? bestWall : base.ScanForBlockers();
        }
    }
}
