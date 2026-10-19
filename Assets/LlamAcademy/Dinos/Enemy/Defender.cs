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

        public static readonly List<Defender> AllDefenders = new();
        public bool IsEmergencyBaseDefender { get; private set; } = false;
        public Unit.Unit AssignedInvaderTarget { get; private set; } = null;
        private Vector3 _DefaultPostPosition;
        private bool _HasRecordedDefaultPost = false;

        private float _LastAttackTime;
        private GameObject _HeldWeapon;
        private Transform _HandBone;
        private Collider[] _ScanBuffer = new Collider[24];
        private static int _SpawnCounter = 0;
        private bool _IsThrusting = false;
        private Vector3 _OriginalWeaponLocalPos;
        private Quaternion _OriginalWeaponLocalRot;
        private Vector3 _ThreatenedWallPos = Vector3.zero;
        private float _LastThreatTime = -99f;
        private Unit.Unit _ThreateningDino = null;

        public static void BroadcastWallThreat(Vector3 threatenedWallPos, Unit.Unit attackingDino = null)
        {
            for (int i = 0; i < AllDefenders.Count; i++)
            {
                var d = AllDefenders[i];
                if (d != null && d.Health > 0 && d.gameObject.activeInHierarchy)
                {
                    d.AlertWallThreat(threatenedWallPos, attackingDino);
                }
            }
        }

        public void AlertWallThreat(Vector3 threatenedWallPos, Unit.Unit attackingDino = null)
        {
            _ThreatenedWallPos = threatenedWallPos;
            _LastThreatTime = Time.time;
            if (attackingDino != null && attackingDino.Health > 0)
            {
                _ThreateningDino = attackingDino;
            }
        }

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

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.OnBreachOpened += HandleBreachOpened;
            }

            EquipWeaponVisual();
        }

        private void OnEnable()
        {
            if (!AllDefenders.Contains(this))
            {
                AllDefenders.Add(this);
            }
        }

        private void OnDisable()
        {
            AllDefenders.Remove(this);

            if (NavMeshManager.Instance != null)
            {
                NavMeshManager.Instance.OnNavMeshUpdated -= OnNavMeshUpdated;
            }

            if (LlamAcademy.Dinos.AI.TacticalBreachMemory.HasInstance)
            {
                LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.OnBreachOpened -= HandleBreachOpened;
            }
        }

        private void HandleBreachOpened(LlamAcademy.Dinos.AI.BreachPoint breach)
        {
            if (Health <= 0 || breach == null) return;
            float dist = Vector3.Distance(transform.position, breach.Position);
            if (dist > 38.0f) return; // Quá xa, giữ nguyên vị trí phân bổ làng

            // Lập tức cơ động chốt chặn cửa ngõ đột phá (Chokepoint Defense)
            Vector3 defensiveSpot = (Role == DefenderRole.Spearman)
                ? breach.IngressPoint + breach.Direction * 0.5f // Lính giáo dựng phên dậu chốt ngay mép trong
                : breach.IngressPoint + breach.Direction * 4.5f + Vector3.Cross(breach.Direction, Vector3.up) * (Random.value > 0.5f ? 2.5f : -2.5f); // Cung thủ đứng bọc lót sườn sau

            if (UnityEngine.AI.NavMesh.SamplePosition(defensiveSpot, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                SetDestination(hit.position);
            }
            else
            {
                SetDestination(breach.IngressPoint);
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

        public static Vector3 GetBasePosition()
        {
            if (PrehistoricVillageBase.Instance != null)
                return PrehistoricVillageBase.Instance.transform.position;
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.BaseTarget != null)
                return PrehistoricGameplayManager.Instance.BaseTarget.position;
            if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
                return RoundManager.Instance.DinoTarget.position;
            return new Vector3(-2.23f, 0f, -38.26f);
        }

        private static readonly List<Unit.Unit> s_InvadingDinos = new();
        private static float s_LastCoordinationTime = -99f;

        public static List<Unit.Unit> GetDinosaursInBase(Vector3 basePos, float baseRadius = 15.0f)
        {
            s_InvadingDinos.Clear();

            // 1. Quét từ ActiveDinos của PrehistoricDinoBase
            foreach (var dino in PrehistoricDinoBase.ActiveDinos)
            {
                if (dino == null || dino.Health <= 0 || !dino.gameObject.activeInHierarchy) continue;
                if (dino.HasReachedBase || Vector3.Distance(dino.transform.position, basePos) <= baseRadius)
                {
                    s_InvadingDinos.Add(dino);
                }
            }

            // 2. Quét từ ActiveMonsters của PrehistoricGameplayManager (bao gồm cả FlyingUnit và RunnerDino)
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.ActiveMonsters != null)
            {
                foreach (var monster in PrehistoricGameplayManager.Instance.ActiveMonsters)
                {
                    if (monster == null || monster.Health <= 0 || !monster.gameObject.activeInHierarchy) continue;
                    if (s_InvadingDinos.Contains(monster)) continue;

                    if (monster is PrehistoricDinoBase pdb && (pdb.HasReachedBase || Vector3.Distance(pdb.transform.position, basePos) <= baseRadius))
                    {
                        s_InvadingDinos.Add(monster);
                    }
                    else if (monster is FlyingUnit fu && (fu.HasReachedBase || Vector3.Distance(fu.transform.position, basePos) <= baseRadius))
                    {
                        s_InvadingDinos.Add(monster);
                    }
                    else if (Vector3.Distance(monster.transform.position, basePos) <= baseRadius)
                    {
                        s_InvadingDinos.Add(monster);
                    }
                }
            }

            return s_InvadingDinos;
        }

        public static void CoordinateBaseDefenders()
        {
            if (Time.time - s_LastCoordinationTime < 0.25f) return;
            s_LastCoordinationTime = Time.time;

            Vector3 basePos = GetBasePosition();
            var invadingDinos = GetDinosaursInBase(basePos, 15.0f);
            int dinoCount = invadingDinos.Count;

            // Dọn dẹp AllDefenders null hoặc đã chết
            AllDefenders.RemoveAll(d => d == null || d.Health <= 0 || !d.gameObject.activeInHierarchy);

            if (dinoCount == 0)
            {
                // Không có khủng long trong căn cứ: Giải phóng mọi NPC về nhiệm vụ thông thường
                for (int i = 0; i < AllDefenders.Count; i++)
                {
                    var d = AllDefenders[i];
                    if (d.IsEmergencyBaseDefender)
                    {
                        d.IsEmergencyBaseDefender = false;
                        d.AssignedInvaderTarget = null;
                        if (d._HasRecordedDefaultPost && d.Agent != null && d.Agent.enabled && d.Agent.isOnNavMesh)
                        {
                            d.Agent.SetDestination(d._DefaultPostPosition);
                        }
                    }
                }
                return;
            }

            // CÓ KHỦNG LONG XÂM NHẬP / CẮN PHÁ CĂN CỨ:
            // Phân công số lượng NPC dựa trên số lượng khủng long trong căn cứ (mỗi con 2 NPC)
            int quota = Mathf.Clamp(dinoCount * 2, 2, AllDefenders.Count);

            // Sắp xếp các NPC theo khoảng cách gần căn cứ nhất để điều động chi viện
            AllDefenders.Sort((a, b) =>
                Vector3.Distance(a.transform.position, basePos).CompareTo(Vector3.Distance(b.transform.position, basePos))
            );

            for (int i = 0; i < AllDefenders.Count; i++)
            {
                var def = AllDefenders[i];
                if (i < quota)
                {
                    def.IsEmergencyBaseDefender = true;
                    def.AssignedInvaderTarget = invadingDinos[i % dinoCount];
                }
                else
                {
                    // Các NPC còn lại tiếp tục bảo vệ tuyến ngoài làng
                    def.IsEmergencyBaseDefender = false;
                    def.AssignedInvaderTarget = null;
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (Health <= 0) return;

            if (!_HasRecordedDefaultPost)
            {
                _DefaultPostPosition = transform.position;
                _HasRecordedDefaultPost = true;
            }

            // Tự động điều phối NPC bảo vệ căn cứ theo số lượng khủng long đang xâm nhập
            CoordinateBaseDefenders();

            // Chu trình chiến đấu liên tục bảo vệ ngôi làng
            HandleCombatBehavior();
        }

        private void HandleCombatBehavior()
        {
            // 1. NẾU LÀ NPC ĐƯỢC CHỌN CHI VIỆN CĂN CỨ: ƯU TIÊN SỐ 1 HẠ GỤC KHỦNG LONG TRONG CĂN CỨ
            if (IsEmergencyBaseDefender)
            {
                Unit.Unit targetDino = (AssignedInvaderTarget != null && AssignedInvaderTarget.Health > 0 && AssignedInvaderTarget.gameObject.activeInHierarchy)
                    ? AssignedInvaderTarget
                    : null;

                if (targetDino == null)
                {
                    Vector3 basePos = GetBasePosition();
                    var invaders = GetDinosaursInBase(basePos, 16.0f);
                    if (invaders.Count > 0)
                    {
                        targetDino = invaders[0];
                        AssignedInvaderTarget = targetDino;
                    }
                }

                if (targetDino != null)
                {
                    Vector3 targetPos = targetDino.transform.position;
                    float distToTarget = Vector3.Distance(transform.position, targetPos);

                    // Xoay mặt về hướng khủng long đang cắn phá căn cứ
                    Vector3 faceDir = (targetPos - transform.position);
                    faceDir.y = 0;
                    if (faceDir.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 10f);
                    }

                    // Nếu đã vào tầm đánh
                    if (distToTarget <= AttackRange)
                    {
                        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                        {
                            Agent.isStopped = true;
                        }

                        if (Time.time >= _LastAttackTime + AttackInterval)
                        {
                            _LastAttackTime = Time.time;
                            Collider col = targetDino.GetComponentInChildren<Collider>();
                            PerformAttack(col, targetPos, distToTarget);
                        }
                    }
                    else
                    {
                        // Tiến nhanh về căn cứ tiếp cận khủng long để tiêu diệt
                        if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                        {
                            Agent.isStopped = false;
                            Agent.SetDestination(targetPos);
                        }
                    }
                    return;
                }
            }

            // 2. TÁC CHIẾN BÌNH THƯỜNG / PHÒNG THỦ PHÁO ĐÀI VÀ CĂN CỨ
            float searchRange = Mathf.Max(AttackRange + 8.0f, 26.0f);
            Collider targetCol = FindNearestHostileMonster(searchRange);

            // Nếu không có quái trong bán kính quét cá nhân, nhưng tường/pháo đài làng đang bị tấn công
            if (targetCol == null && Time.time - _LastThreatTime < 8.0f && _ThreatenedWallPos != Vector3.zero)
            {
                targetCol = FindHostileMonsterNearPoint(_ThreatenedWallPos, 18.0f);
            }

            if (targetCol != null)
            {
                Vector3 targetPos = targetCol.bounds.center;
                float distToTarget = Vector3.Distance(transform.position, targetPos);

                // Xoay dứt khoát và chính xác đối diện hướng khủng long đang đánh pháo đài/căn cứ
                Vector3 faceDir = (targetPos - transform.position);
                faceDir.y = 0;
                if (faceDir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(faceDir), 720f * Time.deltaTime);
                }

                // Nếu mục tiêu trong tầm đánh
                if (distToTarget <= AttackRange)
                {
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = true;
                    }

                    // Cung thủ kiting chiến thuật: nếu quái cận chiến áp sát (< 3.0m), lùi lại giữ cự ly an toàn
                    if (Role == DefenderRole.Archer && distToTarget < 3.0f)
                    {
                        Vector3 retreatDir = (transform.position - targetPos).normalized;
                        retreatDir.y = 0;
                        Vector3 kitePos = transform.position + retreatDir * 3.5f;
                        if (NavMesh.SamplePosition(kitePos, out NavMeshHit kHit, 3.5f, NavMesh.AllAreas))
                        {
                            if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                            {
                                Agent.isStopped = false;
                                Agent.SetDestination(kHit.position);
                            }
                        }
                    }

                    if (Time.time >= _LastAttackTime + AttackInterval)
                    {
                        _LastAttackTime = Time.time;
                        PerformAttack(targetCol, targetPos, distToTarget);
                    }
                }
                else
                {
                    // Di chuyển tiếp cận khủng long để tiêu diệt
                    if (Agent != null && Agent.enabled && Agent.isOnNavMesh)
                    {
                        Agent.isStopped = false;
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

            if (targetCol != null && IsHostileMonster(targetCol))
            {
                IDamageable dmg = targetCol.GetComponent<IDamageable>() ?? targetCol.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.Health > 0)
                {
                    dmg.TakeDamage(AttackDamage + 5);
                }
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

                if (t >= 1.0f || (target != null && Vector3.Distance(proj.transform.position, currentTargetPos) < 1.0f))
                {
                    if (target != null && IsHostileMonster(target))
                    {
                        IDamageable damageable = target.GetComponent<IDamageable>() ?? target.GetComponentInParent<IDamageable>();
                        if (damageable != null && damageable.Health > 0)
                        {
                            damageable.TakeDamage(dmgAmount);
                        }
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
            float bestPriorityScore = float.MinValue;

            Vector3 basePos = GetBasePosition();

            for (int i = 0; i < count; i++)
            {
                Collider col = _ScanBuffer[i];
                if (!IsHostileMonster(col)) continue;

                Vector3 targetPos = col.bounds.center;
                float dist = Vector3.Distance(transform.position, targetPos);

                // Điểm số phân cấp độ nguy hiểm chiến thuật (Tactical Threat Scoring)
                float score = 100f - dist;

                // 1. Khủng long đang trực tiếp cắn phá căn cứ hoặc tường/pháo đài làng
                PrehistoricDinoBase dino = col.GetComponent<PrehistoricDinoBase>() ?? col.GetComponentInParent<PrehistoricDinoBase>();
                if (dino != null)
                {
                    if (dino.HasReachedBase || dino.TargetDamageable is PrehistoricVillageBase)
                    {
                        score += 650f; // Đang đánh căn cứ chính!
                    }
                    else if (dino.TargetDamageable is Wall)
                    {
                        score += 380f; // Đang trực tiếp cắn phá tường thành / pháo đài!
                    }
                }

                // 2. Khủng long đang xâm nhập căn cứ
                bool isInvadingBase = Vector3.Distance(targetPos, basePos) <= 16.0f;
                if (dino != null && dino.HasReachedBase) isInvadingBase = true;
                if (col.TryGetComponent(out FlyingUnit fu) && fu.HasReachedBase) isInvadingBase = true;

                if (isInvadingBase)
                {
                    score += 500f;
                }

                // 3. Khủng long đang tiến sát tuyến pháo đài mặt trước làng (-10 <= Z <= 6)
                if (targetPos.z >= -10.0f && targetPos.z <= 6.0f)
                {
                    score += 260f; // Tiếp cận pháo đài tiền tiêu!
                }

                // 4. Khủng long vừa được báo động phá tường
                if (_ThreateningDino != null && _ThreateningDino.Health > 0 &&
                    (col.gameObject == _ThreateningDino.gameObject || col.transform.IsChildOf(_ThreateningDino.transform)))
                {
                    score += 320f;
                }

                string n = col.gameObject.name.ToLower();
                // 5. Quái chạy nhanh (Raptor) tiếp cận: nguy hiểm
                if (n.Contains("raptor") || n.Contains("runner")) score += 40f;

                // 6. Boss T-Rex
                if (n.Contains("trex") || n.Contains("t-rex") || n.Contains("boss")) score += 50f;

                // 7. Quái đang vượt qua khe hở thâm nhập làng
                if (LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance != null &&
                    LlamAcademy.Dinos.AI.TacticalBreachMemory.Instance.IsWallNearActiveBreach(targetPos, 5.0f))
                {
                    score += 60f;
                }

                if (score > bestPriorityScore)
                {
                    bestPriorityScore = score;
                    best = col;
                }
            }
            return best;
        }

        private Collider FindHostileMonsterNearPoint(Vector3 point, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(point, radius, _ScanBuffer);
            Collider best = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = _ScanBuffer[i];
                if (!IsHostileMonster(col)) continue;
                float d = Vector3.Distance(transform.position, col.bounds.center);
                if (d < closestDist)
                {
                    closestDist = d;
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
            AllDefenders.Remove(this);
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
