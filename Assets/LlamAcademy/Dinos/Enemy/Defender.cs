using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Behavior;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using LlamAcademy.Dinos.Utility;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Enemy
{
    public enum DefenderRole
    {
        Archer,
        Spearman
    }

    [RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    public class Defender : Unit.Unit
    {
        [Header("Defender Combat Configuration")]
        [SerializeField] public DefenderRole Role = DefenderRole.Archer;
        [SerializeField] private float AttackRange = 14.0f;
        [SerializeField] private float AttackInterval = 1.2f;
        [SerializeField] private int AttackDamage = 28;
        [SerializeField] private float ProjectileSpeed = 24.0f;

        public AIState State =>
            GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.COMMAND, out BlackboardVariable<AIState> stateVariable)
                ? stateVariable.Value
                : AIState.Idle;

        public Vector3 TargetLocation => Agent != null && Agent.enabled && Agent.isOnNavMesh ? Agent.destination : transform.position;

        private float _LastAttackTime;
        private GameObject _HeldWeapon;
        private Transform _HandBone;
        private Collider[] _ScanBuffer = new Collider[24];
        private static int _SpawnCounter = 0;
        private bool _IsThrusting = false;
        private Vector3 _OriginalWeaponLocalPos;
        private Quaternion _OriginalWeaponLocalRot;

        protected override void Awake()
        {
            base.Awake();
            if (MaxHealth <= 0)
            {
                MaxHealth = 180;
                Health = 180;
            }

            // Phân chia 50% NPC cầm cung bắn tên, 50% NPC cầm giáo phóng & đâm quái
            _SpawnCounter++;
            if (UnitType != null && UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Archer)
            {
                Role = DefenderRole.Archer;
            }
            else if (UnitType != null && (UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Cannoneer || UnitType.Type == LlamAcademy.Dinos.Unit.UnitType.Mage))
            {
                Role = DefenderRole.Spearman;
            }
            else
            {
                Role = (_SpawnCounter % 2 == 0) ? DefenderRole.Archer : DefenderRole.Spearman;
            }

            ConfigureRoleStats();
        }

        private void ConfigureRoleStats()
        {
            if (Role == DefenderRole.Archer)
            {
                AttackRange = 15.0f;
                AttackInterval = 1.2f;
                AttackDamage = 28;
                ProjectileSpeed = 24.0f;
            }
            else // Spearman
            {
                AttackRange = 10.0f; // Tầm phóng giáo xa 10m, cận chiến < 2.5m
                AttackInterval = 1.6f;
                AttackDamage = 38;
                ProjectileSpeed = 26.0f;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated += OnNavMeshUpdated;
            }

            EquipWeaponVisual();
        }

        private void OnDisable()
        {
            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated -= OnNavMeshUpdated;
            }
        }

        private void EquipWeaponVisual()
        {
            if (_HeldWeapon != null) return;

            _HandBone = FindHandBone(transform);

            if (Role == DefenderRole.Archer)
            {
                _HeldWeapon = CreateBowVisual();
            }
            else
            {
                _HeldWeapon = CreateSpearVisual();
            }

            if (_HeldWeapon != null)
            {
                if (_HandBone != null)
                {
                    _HeldWeapon.transform.SetParent(_HandBone, false);
                }
                else
                {
                    _HeldWeapon.transform.SetParent(transform, false);
                    _HeldWeapon.transform.localPosition = new Vector3(0.3f, 0.85f, 0.35f);
                    _HeldWeapon.transform.localRotation = Quaternion.Euler(15f, 10f, 0f);
                }

                _OriginalWeaponLocalPos = _HeldWeapon.transform.localPosition;
                _OriginalWeaponLocalRot = _HeldWeapon.transform.localRotation;
            }
        }

        private Transform FindHandBone(Transform root)
        {
            Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in allChildren)
            {
                string n = t.name.ToLower();
                if ((n.Contains("hand") && (n.Contains("r") || n.Contains("right"))) ||
                    n.Contains("wrist_r") || n.Contains("hand_r") || n.Contains("r_hand"))
                {
                    return t;
                }
            }
            foreach (Transform t in allChildren)
            {
                if (t.name.ToLower().Contains("hand")) return t;
            }
            return null;
        }

        private static Material CreateSafeWeaponMaterial(Color color, float smoothness = 0.2f)
        {
            return LlamAcademy.Dinos.Rendering.PrehistoricShaderUtility.CreateSafeMaterial(color, smoothness);
        }

        private GameObject CreateBowVisual()
        {
            GameObject bow = new GameObject("Weapon_Bow");
            bow.transform.localPosition = new Vector3(0.05f, 0.05f, 0.05f);
            bow.transform.localRotation = Quaternion.Euler(0f, 85f, 80f);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.42f, 0.25f, 0.12f), 0.15f);
            Material stringMat = CreateSafeWeaponMaterial(new Color(0.92f, 0.88f, 0.80f), 0.05f);

            // Cán cầm trung tâm
            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(grip.GetComponent<Collider>());
            grip.name = "Grip";
            grip.transform.SetParent(bow.transform, false);
            grip.transform.localScale = new Vector3(0.04f, 0.18f, 0.04f);
            grip.transform.localPosition = Vector3.zero;
            grip.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Cánh cung trên
            GameObject upper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(upper.GetComponent<Collider>());
            upper.name = "UpperLimb";
            upper.transform.SetParent(bow.transform, false);
            upper.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            upper.transform.localPosition = new Vector3(0, 0.38f, 0.08f);
            upper.transform.localRotation = Quaternion.Euler(22f, 0, 0);
            upper.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Cánh cung dưới
            GameObject lower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(lower.GetComponent<Collider>());
            lower.name = "LowerLimb";
            lower.transform.SetParent(bow.transform, false);
            lower.transform.localScale = new Vector3(0.035f, 0.28f, 0.035f);
            lower.transform.localPosition = new Vector3(0, -0.38f, 0.08f);
            lower.transform.localRotation = Quaternion.Euler(-22f, 0, 0);
            lower.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Dây cung căng chắc
            GameObject bString = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(bString.GetComponent<Collider>());
            bString.name = "String";
            bString.transform.SetParent(bow.transform, false);
            bString.transform.localScale = new Vector3(0.012f, 0.52f, 0.012f);
            bString.transform.localPosition = new Vector3(0, 0, 0.15f);
            bString.GetComponent<Renderer>().sharedMaterial = stringMat;

            bow.transform.localScale = Vector3.one * 0.9f;
            return bow;
        }

        private GameObject CreateSpearVisual()
        {
            GameObject spear = new GameObject("Weapon_Spear");
            spear.transform.localPosition = new Vector3(0.02f, 0f, 0.05f);
            spear.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.36f, 0.20f, 0.08f), 0.15f);
            Material flintMat = CreateSafeWeaponMaterial(new Color(0.22f, 0.24f, 0.26f), 0.35f);
            Material gripMat = CreateSafeWeaponMaterial(new Color(0.65f, 0.38f, 0.18f), 0.1f);

            // Cán giáo dài bằng gỗ
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.name = "Shaft";
            shaft.transform.SetParent(spear.transform, false);
            shaft.transform.localScale = new Vector3(0.045f, 0.95f, 0.045f);
            shaft.transform.localPosition = new Vector3(0, 0.2f, 0);
            shaft.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Bọc da tay cầm
            GameObject leather = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(leather.GetComponent<Collider>());
            leather.name = "Grip";
            leather.transform.SetParent(spear.transform, false);
            leather.transform.localScale = new Vector3(0.055f, 0.22f, 0.055f);
            leather.transform.localPosition = new Vector3(0, 0.05f, 0);
            leather.GetComponent<Renderer>().sharedMaterial = gripMat;

            // Mũi giáo đá/sắt sắc bén
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.name = "FlintTip";
            tip.transform.SetParent(spear.transform, false);
            tip.transform.localScale = new Vector3(0.075f, 0.24f, 0.025f);
            tip.transform.localPosition = new Vector3(0, 1.25f, 0);
            tip.GetComponent<Renderer>().sharedMaterial = flintMat;

            spear.transform.localScale = Vector3.one * 1.0f;
            return spear;
        }

        protected override void Update()
        {
            base.Update();

            if (Health <= 0) return;

            // Chu trình chiến đấu liên tục bảo vệ ngôi làng
            HandleCombatBehavior();
        }

        private void HandleCombatBehavior()
        {
            Collider targetCol = FindNearestHostileMonster(AttackRange + 3.0f);

            if (targetCol != null)
            {
                Vector3 targetPos = targetCol.bounds.center;
                float distToTarget = Vector3.Distance(transform.position, targetPos);

                // Xoay mượt mà đối diện hướng khủng long tấn công
                Vector3 faceDir = (targetPos - transform.position);
                faceDir.y = 0;
                if (faceDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);
                }

                // Nếu mục tiêu trong tầm đánh
                if (distToTarget <= AttackRange)
                {
                    if (Time.time >= _LastAttackTime + AttackInterval)
                    {
                        _LastAttackTime = Time.time;
                        PerformAttack(targetCol, targetPos, distToTarget);
                    }
                }
                else
                {
                    // Di chuyển tiếp cận khủng long để ngăn chặn
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.SetDestination(targetPos);
                    }
                }
            }
            else
            {
                if (Animator != null)
                {
                    Animator.SetBool(AnimationConstants.IS_ATTACKING_PARAMETER, false);
                }
            }
        }

        private void PerformAttack(Collider targetCol, Vector3 targetPos, float distance)
        {
            if (Animator != null)
            {
                Animator.SetBool(AnimationConstants.IS_ATTACKING_PARAMETER, true);
            }

            Vector3 spawnOrigin = (_HeldWeapon != null)
                ? _HeldWeapon.transform.position + transform.forward * 0.4f + Vector3.up * 0.2f
                : transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;

            if (Role == DefenderRole.Archer)
            {
                // Bắn tên bay về phía khủng long
                GameObject arrow = CreateFlyingArrow(spawnOrigin, (targetPos - spawnOrigin).normalized);
                StartCoroutine(AnimateProjectile(arrow, targetCol, targetPos, AttackDamage, ProjectileSpeed, 0.15f));
            }
            else // Spearman
            {
                if (distance <= 2.5f)
                {
                    // Đâm giáo cận chiến uy lực
                    StartCoroutine(AnimateMeleeThrust(targetCol));
                }
                else
                {
                    // Phóng ngọn giáo bay cắm vào khủng long
                    GameObject flyingSpear = CreateFlyingSpear(spawnOrigin, (targetPos - spawnOrigin).normalized);
                    StartCoroutine(AnimateProjectile(flyingSpear, targetCol, targetPos, AttackDamage, ProjectileSpeed, 0.12f));

                    // Tạm ẩn giáo trên tay rồi rút ngọn giáo mới ra phóng tiếp
                    StartCoroutine(AnimateSpearThrowRespawn());
                }
            }
        }

        private IEnumerator AnimateMeleeThrust(Collider targetCol)
        {
            if (_HeldWeapon == null || _IsThrusting) yield break;
            _IsThrusting = true;

            float elapsed = 0f;
            float duration = 0.22f;
            Vector3 startPos = _OriginalWeaponLocalPos;
            Vector3 thrustPos = _OriginalWeaponLocalPos + Vector3.forward * 0.5f + Vector3.up * 0.1f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float pingPong = Mathf.Sin(t * Mathf.PI);
                _HeldWeapon.transform.localPosition = Vector3.Lerp(startPos, thrustPos, pingPong);
                yield return null;
            }

            _HeldWeapon.transform.localPosition = startPos;
            _IsThrusting = false;

            if (targetCol != null && IsHostileMonster(targetCol) && targetCol.TryGetComponent(out IDamageable dmg))
            {
                dmg.TakeDamage(AttackDamage + 5);
            }
        }

        private IEnumerator AnimateSpearThrowRespawn()
        {
            if (_HeldWeapon == null) yield break;
            _HeldWeapon.SetActive(false);
            yield return new WaitForSeconds(0.45f);
            if (_HeldWeapon != null && Health > 0)
            {
                _HeldWeapon.SetActive(true);
            }
        }

        private IEnumerator AnimateProjectile(GameObject proj, Collider target, Vector3 lastKnownPos, int dmgAmount, float speed, float arcFactor)
        {
            float elapsed = 0f;
            Vector3 start = proj.transform.position;

            while (proj != null)
            {
                Vector3 currentTargetPos = (target != null && target.gameObject.activeInHierarchy)
                    ? target.bounds.center
                    : lastKnownPos;

                float distance = Vector3.Distance(start, currentTargetPos);
                float duration = Mathf.Max(0.06f, distance / speed);

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                Vector3 linear = Vector3.Lerp(start, currentTargetPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * (distance * arcFactor);
                proj.transform.position = new Vector3(linear.x, linear.y + arc, linear.z);

                Vector3 moveDir = (currentTargetPos - proj.transform.position).normalized;
                if (moveDir != Vector3.zero) proj.transform.rotation = Quaternion.LookRotation(moveDir);

                if (t >= 1.0f || (target != null && Vector3.Distance(proj.transform.position, currentTargetPos) < 0.8f))
                {
                    if (target != null && IsHostileMonster(target) && target.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.TakeDamage(dmgAmount);
                    }
                    Destroy(proj);
                    yield break;
                }

                yield return null;
            }
        }

        private GameObject CreateFlyingArrow(Vector3 pos, Vector3 dir)
        {
            GameObject arrow = new GameObject("Flying_Arrow");
            arrow.transform.position = pos;
            arrow.transform.rotation = Quaternion.LookRotation(dir);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(arrow.transform, false);
            shaft.transform.localScale = new Vector3(0.04f, 0.45f, 0.04f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            Material mat = CreateSafeWeaponMaterial(new Color(0.48f, 0.30f, 0.15f), 0.15f);
            shaft.GetComponent<Renderer>().sharedMaterial = mat;

            return arrow;
        }

        private GameObject CreateFlyingSpear(Vector3 pos, Vector3 dir)
        {
            GameObject spear = new GameObject("Flying_Spear");
            spear.transform.position = pos;
            spear.transform.rotation = Quaternion.LookRotation(dir);

            Material woodMat = CreateSafeWeaponMaterial(new Color(0.38f, 0.22f, 0.10f), 0.15f);
            Material flintMat = CreateSafeWeaponMaterial(new Color(0.20f, 0.22f, 0.24f), 0.35f);

            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(shaft.GetComponent<Collider>());
            shaft.transform.SetParent(spear.transform, false);
            shaft.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            shaft.GetComponent<Renderer>().sharedMaterial = woodMat;

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(tip.GetComponent<Collider>());
            tip.transform.SetParent(spear.transform, false);
            tip.transform.localScale = new Vector3(0.08f, 0.25f, 0.03f);
            tip.transform.localPosition = new Vector3(0, 0, 0.95f);
            tip.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            tip.GetComponent<Renderer>().sharedMaterial = flintMat;

            return spear;
        }

        private Collider FindNearestHostileMonster(float searchRange)
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, searchRange, _ScanBuffer);
            Collider best = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = _ScanBuffer[i];
                if (!IsHostileMonster(col)) continue;

                float dist = (col.transform.position - transform.position).sqrMagnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    best = col;
                }
            }
            return best;
        }

        private void OnNavMeshUpdated()
        {
            if (Agent == null || !Agent.enabled) return;

            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, Agent.radius,
                    new NavMeshQueryFilter() { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask }))
            {
                if (GraphAgent != null) GraphAgent.enabled = false;
                Agent.enabled = false;
                if (Rigidbody != null)
                {
                    Rigidbody.isKinematic = false;
                    Rigidbody.useGravity = true;
                }

                RaiseDeathEvent();
                Die();
            }
        }

        protected override void OnTargetExit(IDamageable target)
        {
            if (GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.NEARBY_ATTACKABLES,
                    out BlackboardVariable<List<GameObject>> nearbyAttackables))
            {
                nearbyAttackables.Value.Remove(target.Transform.gameObject);
                GraphAgent.SetVariableValue(EnemyGraphConstants.NEARBY_ATTACKABLES, nearbyAttackables.Value);
            }
        }

        protected override void OnTargetEnter(IDamageable target)
        {
            if (GraphAgent != null && GraphAgent.GetVariable(EnemyGraphConstants.NEARBY_ATTACKABLES,
                    out BlackboardVariable<List<GameObject>> nearbyAttackables))
            {
                nearbyAttackables.Value.Add(target.Transform.gameObject);
                GraphAgent.SetVariableValue(EnemyGraphConstants.NEARBY_ATTACKABLES, nearbyAttackables.Value);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Attacking);
            }
        }

        public void SetDestination(Vector3 position)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.TARGET_LOCATION, position);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.CommandedMove);
            }
            else if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.SetDestination(position);
            }
        }

        public void Patrol(List<Vector3> waypoints)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.PATROL_WAYPOINTS, waypoints);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Patrol);
            }
            else if (Agent != null && Agent.enabled && Agent.isOnNavMesh && waypoints != null && waypoints.Count > 0)
            {
                Agent.SetDestination(waypoints[0]);
            }
        }

        public void Idle()
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Idle);
            }
            else if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
            {
                Agent.ResetPath();
            }
        }

        public void Attack(IDamageable damageable)
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.TARGET, damageable);
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Attacking);
            }
        }

        public override void Die()
        {
            if (GraphAgent != null)
            {
                GraphAgent.SetVariableValue(EnemyGraphConstants.COMMAND, AIState.Idle);
            }
            if (Animator != null)
            {
                Animator.SetTrigger(AnimationConstants.DIE_PARAMETER);
            }
            if (_HeldWeapon != null)
            {
                Destroy(_HeldWeapon);
            }
            if (TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
            }
            Invoke(nameof(DestroyGO), 2.5f);
        }

        private void DestroyGO()
        {
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
