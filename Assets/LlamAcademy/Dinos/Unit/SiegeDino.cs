using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    /// <summary>
    /// Khủng long Thiết Giáp (Ankylosaurus) - Xe tăng công thành hạng nặng.
    /// Trang bị hệ thống mô phỏng dáng đi 4 chân (Procedural Quadruped Gait) thực tế:
    /// - 4 chân sải bước nhịp nhàng đan chéo (Chân Trước Trái + Chân Sau Phải, Chân Trước Phải + Chân Sau Trái)
    /// - Nhấc chân khi bước tới, đuôi gai lắc lư uyển chuyển, đầu gật gù theo nhịp bước
    /// - Quật đuôi dũng mãnh khi tấn công phá hủy công trình làng
    /// </summary>
    public class SiegeDino : PrehistoricDinoBase
    {
        [Header("Siege Breaker Specialization")]
        [SerializeField] private float WallDetectRadius = 8.5f;

        protected override float WallScanRadius => Mathf.Max(WallDetectRadius, 9.5f);

        // --- Hệ thống Bone Animation 4 Chân & Đuôi Độc Lập ---
        private Transform _boneFrontLegL;
        private Transform _boneFrontLegR;
        private Transform _boneBackLegL;
        private Transform _boneBackLegR;
        private Transform _boneFrontLowLegL;
        private Transform _boneFrontLowLegR;
        private Transform _boneBackLowLegL;
        private Transform _boneBackLowLegR;
        private Transform _boneTail1;
        private Transform _boneTail2;
        private Transform _boneTail3;
        private Transform _boneNeck;
        private Transform _boneHead;
        private Transform _boneBody;

        private Quaternion _rotInitFrontLegL;
        private Quaternion _rotInitFrontLegR;
        private Quaternion _rotInitBackLegL;
        private Quaternion _rotInitBackLegR;
        private Quaternion _rotInitFrontLowL;
        private Quaternion _rotInitFrontLowR;
        private Quaternion _rotInitBackLowL;
        private Quaternion _rotInitBackLowR;
        private Quaternion _rotInitTail1;
        private Quaternion _rotInitTail2;
        private Quaternion _rotInitTail3;
        private Quaternion _rotInitNeck;
        private Quaternion _rotInitHead;
        private Quaternion _rotInitBody;

        private bool _bonesInitialized = false;
        private float _walkPhase = 0f;
        private float _tailWagPhase = 0f;
        private float _attackProgress = -1f;

        protected override void Awake()
        {
            MoveSpeed = 3.0f;
            base.Awake();
            ConfigureNavMeshAgent();
            BuildingDamageMultiplier = 3.5f;
            AttackDamage = 25;
            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 1912;
            Health = MaxHealth;
            GoldReward = 40;

            // Đánh dấu rõ ràng là khủng long 4 chân
            _IsQuadruped = true;

            // Tắt ModelAnimator vì mô hình Ankylosaurus dùng hệ thống 4 chân procedural độc quyền
            if (ModelAnimator != null)
            {
                ModelAnimator.enabled = false;
            }
            if (ModelLegacyAnimation != null)
            {
                ModelLegacyAnimation.enabled = false;
            }
        }

        protected override void Start()
        {
            base.Start();
            InitializeBones();
        }

        private void InitializeBones()
        {
            if (_bonesInitialized) return;

            Transform searchRoot = VisualModel != null ? VisualModel : transform;
            Transform[] allBones = searchRoot.GetComponentsInChildren<Transform>(true);

            foreach (var b in allBones)
            {
                if (b == null) continue;
                string n = b.name.ToLower();

                // Chân trước trái (Front Leg Left)
                if (_boneFrontLegL == null && (n.Contains("frontleg.l") || n.Contains("frontupleg.l")))
                {
                    _boneFrontLegL = b;
                    _rotInitFrontLegL = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Chân trước phải (Front Leg Right)
                else if (_boneFrontLegR == null && (n.Contains("frontleg.r") || n.Contains("frontupleg.r")))
                {
                    _boneFrontLegR = b;
                    _rotInitFrontLegR = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Cẳng chân trước trái
                else if (_boneFrontLowLegL == null && n.Contains("frontlowleg.l"))
                {
                    _boneFrontLowLegL = b;
                    _rotInitFrontLowL = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Cẳng chân trước phải
                else if (_boneFrontLowLegR == null && n.Contains("frontlowleg.r"))
                {
                    _boneFrontLowLegR = b;
                    _rotInitFrontLowR = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Chân sau trái (Back Leg Left)
                else if (_boneBackLegL == null && (n.Contains("backleg.l") || n.Contains("backupleg.l")))
                {
                    _boneBackLegL = b;
                    _rotInitBackLegL = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Chân sau phải (Back Leg Right)
                else if (_boneBackLegR == null && (n.Contains("backleg.r") || n.Contains("backupleg.r")))
                {
                    _boneBackLegR = b;
                    _rotInitBackLegR = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Cẳng chân sau trái
                else if (_boneBackLowLegL == null && n.Contains("backlowleg.l"))
                {
                    _boneBackLowLegL = b;
                    _rotInitBackLowL = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Cẳng chân sau phải
                else if (_boneBackLowLegR == null && n.Contains("backlowleg.r"))
                {
                    _boneBackLowLegR = b;
                    _rotInitBackLowR = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Đuôi các đoạn (Tail segments)
                else if (_boneTail1 == null && n.Contains("tail1"))
                {
                    _boneTail1 = b;
                    _rotInitTail1 = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                else if (_boneTail2 == null && (n.Contains("tail2") || n.Contains("tail3")))
                {
                    _boneTail2 = b;
                    _rotInitTail2 = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                else if (_boneTail3 == null && (n.Contains("tail4") || n.Contains("tail5")))
                {
                    _boneTail3 = b;
                    _rotInitTail3 = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Cổ & Đầu (Neck & Head)
                else if (_boneNeck == null && n.Contains("neck"))
                {
                    _boneNeck = b;
                    _rotInitNeck = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                else if (_boneHead == null && n.Contains("head"))
                {
                    _boneHead = b;
                    _rotInitHead = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
                // Thân mình (Body/Torso)
                else if (_boneBody == null && (n.Contains("body") || n.Contains("torso") || n.Contains("hips")))
                {
                    _boneBody = b;
                    _rotInitBody = Quaternion.Inverse(transform.rotation) * b.rotation;
                }
            }

            _bonesInitialized = (_boneFrontLegL != null || _boneBackLegL != null);
        }

        protected override IDamageable ScanForBlockers()
        {
            if (_IsNavigatingBreach) return null;
            return base.ScanForBlockers();
        }

        protected override void PerformAttack(IDamageable target)
        {
            if (target == null) return;
            bool isStructure = (target is Wall) || (target is PrehistoricVillageBase);
            int damage = isStructure ? Mathf.CeilToInt(AttackDamage * BuildingDamageMultiplier) : AttackDamage;
            target.TakeDamage(damage);

            if (target is Wall wallTarget)
            {
                Enemy.Defender.BroadcastWallThreat(wallTarget.transform.position, this);
            }
            else if (target is PrehistoricVillageBase baseTarget)
            {
                Enemy.Defender.BroadcastWallThreat(baseTarget.transform.position, this);
            }

            _IsAttackingAnim = true;
            if (_attackProgress < 0f)
            {
                StartCoroutine(AnkyTailSmashRoutine());
            }
        }

        private System.Collections.IEnumerator AnkyTailSmashRoutine()
        {
            _attackProgress = 0f;
            float duration = 0.55f;
            float timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                _attackProgress = Mathf.Clamp01(timer / duration);
                yield return null;
            }

            _attackProgress = -1f;
            _IsAttackingAnim = false;
        }

        protected override void LateUpdate()
        {
            if (!_bonesInitialized)
            {
                InitializeBones();
                if (!_bonesInitialized) return;
            }

            float speed = Agent != null && Agent.isOnNavMesh ? Agent.velocity.magnitude : 0f;
            bool isMoving = (Agent != null && Agent.isOnNavMesh && !Agent.isStopped && (speed > 0.12f || (Agent.hasPath && Agent.remainingDistance > 0.35f)));
            Quaternion rootRot = transform.rotation;

            // 1. XỬ LÝ ĐỘNG TÁC TẤN CÔNG ĐẬP ĐUÔI (Tail Smash Attack)
            if (_attackProgress >= 0f)
            {
                float smashCurve = Mathf.Sin(_attackProgress * Mathf.PI);
                float tailSwing = smashCurve * 48f;
                float headRam = -smashCurve * 15f;

                if (_boneTail1 != null) _boneTail1.rotation = rootRot * Quaternion.AngleAxis(tailSwing * 0.4f, Vector3.up) * _rotInitTail1;
                if (_boneTail2 != null) _boneTail2.rotation = rootRot * Quaternion.AngleAxis(tailSwing * 0.7f, Vector3.up) * _rotInitTail2;
                if (_boneTail3 != null) _boneTail3.rotation = rootRot * Quaternion.AngleAxis(tailSwing, Vector3.up) * _rotInitTail3;
                if (_boneHead != null) _boneHead.rotation = rootRot * Quaternion.AngleAxis(headRam, Vector3.right) * Quaternion.AngleAxis(tailSwing * 0.2f, Vector3.up) * _rotInitHead;
                if (_boneBody != null) _boneBody.rotation = rootRot * Quaternion.AngleAxis(-tailSwing * 0.25f, Vector3.up) * _rotInitBody;
                return;
            }

            // 2. DÁNG ĐI 4 CHÂN CHUYÊN NGHIỆP (Quadruped Stride Gait)
            if (isMoving)
            {
                float gaitSpeed = Mathf.Clamp(speed * 3.8f, 3.0f, 7.5f);
                _walkPhase += Time.deltaTime * gaitSpeed;
                _tailWagPhase += Time.deltaTime * (gaitSpeed * 0.65f);

                // Góc sải bước chân trước và sau (22 độ swing) quanh trục pitch (Vector3.right)
                float strideAngle = Mathf.Sin(_walkPhase) * 22f;

                // Độ nhấc khớp gối khi chân bước tới (knee lift)
                float liftFL = Mathf.Max(0f, Mathf.Sin(_walkPhase)) * 16f;
                float liftFR = Mathf.Max(0f, -Mathf.Sin(_walkPhase)) * 16f;
                float liftBL = Mathf.Max(0f, -Mathf.Sin(_walkPhase)) * 14f;
                float liftBR = Mathf.Max(0f, Mathf.Sin(_walkPhase)) * 14f;

                // --- Bước đi đan chéo (Diagonal Pair: FL+BR vs FR+BL) ---
                // Cặp 1: Chân Trước Trái (FL) + Chân Sau Phải (BR) cùng tiến
                if (_boneFrontLegL != null)
                {
                    _boneFrontLegL.rotation = rootRot * Quaternion.AngleAxis(strideAngle, Vector3.right) * _rotInitFrontLegL;
                }
                if (_boneFrontLowLegL != null)
                {
                    _boneFrontLowLegL.rotation = rootRot * Quaternion.AngleAxis(strideAngle - liftFL, Vector3.right) * _rotInitFrontLowL;
                }

                if (_boneBackLegR != null)
                {
                    _boneBackLegR.rotation = rootRot * Quaternion.AngleAxis(strideAngle, Vector3.right) * _rotInitBackLegR;
                }
                if (_boneBackLowLegR != null)
                {
                    _boneBackLowLegR.rotation = rootRot * Quaternion.AngleAxis(strideAngle - liftBR, Vector3.right) * _rotInitBackLowR;
                }

                // Cặp 2: Chân Trước Phải (FR) + Chân Sau Trái (BL) cùng lùi/nghịch pha
                if (_boneFrontLegR != null)
                {
                    _boneFrontLegR.rotation = rootRot * Quaternion.AngleAxis(-strideAngle, Vector3.right) * _rotInitFrontLegR;
                }
                if (_boneFrontLowLegR != null)
                {
                    _boneFrontLowLegR.rotation = rootRot * Quaternion.AngleAxis(-strideAngle - liftFR, Vector3.right) * _rotInitFrontLowR;
                }

                if (_boneBackLegL != null)
                {
                    _boneBackLegL.rotation = rootRot * Quaternion.AngleAxis(-strideAngle, Vector3.right) * _rotInitBackLegL;
                }
                if (_boneBackLowLegL != null)
                {
                    _boneBackLowLegL.rotation = rootRot * Quaternion.AngleAxis(-strideAngle - liftBL, Vector3.right) * _rotInitBackLowL;
                }

                // --- Đuôi gai lắc lư uyển chuyển quanh trục Y (Up) ---
                float tailYaw = Mathf.Sin(_tailWagPhase) * 16f;
                if (_boneTail1 != null) _boneTail1.rotation = rootRot * Quaternion.AngleAxis(tailYaw * 0.4f, Vector3.up) * _rotInitTail1;
                if (_boneTail2 != null) _boneTail2.rotation = rootRot * Quaternion.AngleAxis(tailYaw * 0.7f, Vector3.up) * _rotInitTail2;
                if (_boneTail3 != null) _boneTail3.rotation = rootRot * Quaternion.AngleAxis(tailYaw, Vector3.up) * _rotInitTail3;

                // --- Đầu & Cổ gật gù theo nhịp bước ---
                float headPitch = Mathf.Sin(_walkPhase * 2f) * 4.5f;
                float headYaw = Mathf.Cos(_walkPhase) * 3.0f;
                if (_boneNeck != null) _boneNeck.rotation = rootRot * Quaternion.AngleAxis(headPitch * 0.5f, Vector3.right) * Quaternion.AngleAxis(headYaw * 0.5f, Vector3.up) * _rotInitNeck;
                if (_boneHead != null) _boneHead.rotation = rootRot * Quaternion.AngleAxis(headPitch, Vector3.right) * Quaternion.AngleAxis(headYaw, Vector3.up) * _rotInitHead;

                // --- Thân mình chuyển dịch trọng tâm ---
                float bodyRoll = Mathf.Sin(_walkPhase) * 2.8f;
                if (_boneBody != null) _boneBody.rotation = rootRot * Quaternion.AngleAxis(bodyRoll, Vector3.forward) * _rotInitBody;

                if (VisualModel != null && _HasBaseModelTransform)
                {
                    float bobY = Mathf.Abs(Mathf.Sin(_walkPhase * 2f)) * 0.04f;
                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                    VisualModel.localRotation = _BaseModelLocalRot;
                }
            }
            else
            {
                // Khi đứng yên (Idle): hồi phục mượt mà về tư thế tự nhiên chuẩn
                float lerpSpeed = Time.deltaTime * 6f;

                if (_boneFrontLegL != null) _boneFrontLegL.rotation = Quaternion.Slerp(_boneFrontLegL.rotation, rootRot * _rotInitFrontLegL, lerpSpeed);
                if (_boneFrontLowLegL != null) _boneFrontLowLegL.rotation = Quaternion.Slerp(_boneFrontLowLegL.rotation, rootRot * _rotInitFrontLowL, lerpSpeed);
                if (_boneFrontLegR != null) _boneFrontLegR.rotation = Quaternion.Slerp(_boneFrontLegR.rotation, rootRot * _rotInitFrontLegR, lerpSpeed);
                if (_boneFrontLowLegR != null) _boneFrontLowLegR.rotation = Quaternion.Slerp(_boneFrontLowLegR.rotation, rootRot * _rotInitFrontLowR, lerpSpeed);

                if (_boneBackLegL != null) _boneBackLegL.rotation = Quaternion.Slerp(_boneBackLegL.rotation, rootRot * _rotInitBackLegL, lerpSpeed);
                if (_boneBackLowLegL != null) _boneBackLowLegL.rotation = Quaternion.Slerp(_boneBackLowLegL.rotation, rootRot * _rotInitBackLowL, lerpSpeed);
                if (_boneBackLegR != null) _boneBackLegR.rotation = Quaternion.Slerp(_boneBackLegR.rotation, rootRot * _rotInitBackLegR, lerpSpeed);
                if (_boneBackLowLegR != null) _boneBackLowLegR.rotation = Quaternion.Slerp(_boneBackLowLegR.rotation, rootRot * _rotInitBackLowR, lerpSpeed);

                // Đuôi vẫy nhẹ khi nghỉ (Idle tail wag)
                float idleTail = Mathf.Sin(Time.time * 1.5f) * 5f;
                if (_boneTail1 != null) _boneTail1.rotation = Quaternion.Slerp(_boneTail1.rotation, rootRot * Quaternion.AngleAxis(idleTail * 0.4f, Vector3.up) * _rotInitTail1, lerpSpeed);
                if (_boneTail2 != null) _boneTail2.rotation = Quaternion.Slerp(_boneTail2.rotation, rootRot * Quaternion.AngleAxis(idleTail * 0.7f, Vector3.up) * _rotInitTail2, lerpSpeed);
                if (_boneTail3 != null) _boneTail3.rotation = Quaternion.Slerp(_boneTail3.rotation, rootRot * Quaternion.AngleAxis(idleTail, Vector3.up) * _rotInitTail3, lerpSpeed);

                if (_boneNeck != null) _boneNeck.rotation = Quaternion.Slerp(_boneNeck.rotation, rootRot * _rotInitNeck, lerpSpeed);
                if (_boneHead != null) _boneHead.rotation = Quaternion.Slerp(_boneHead.rotation, rootRot * _rotInitHead, lerpSpeed);
                if (_boneBody != null) _boneBody.rotation = Quaternion.Slerp(_boneBody.rotation, rootRot * _rotInitBody, lerpSpeed);

                if (VisualModel != null && _HasBaseModelTransform)
                {
                    VisualModel.localPosition = Vector3.Lerp(VisualModel.localPosition, _BaseModelLocalPos, lerpSpeed);
                    VisualModel.localRotation = Quaternion.Slerp(VisualModel.localRotation, _BaseModelLocalRot, lerpSpeed);
                }
            }
        }
    }
}
