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

            // Vô hiệu hóa Animator rỗng để nhường toàn quyền điều khiển xương cho hệ thống bước đi 4 chân chân thực
            if (ModelAnimator != null)
            {
                ModelAnimator.enabled = false;
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
                    _rotInitFrontLegL = b.localRotation;
                }
                // Chân trước phải (Front Leg Right)
                else if (_boneFrontLegR == null && (n.Contains("frontleg.r") || n.Contains("frontupleg.r")))
                {
                    _boneFrontLegR = b;
                    _rotInitFrontLegR = b.localRotation;
                }
                // Cẳng chân trước trái
                else if (_boneFrontLowLegL == null && n.Contains("frontlowleg.l"))
                {
                    _boneFrontLowLegL = b;
                    _rotInitFrontLowL = b.localRotation;
                }
                // Cẳng chân trước phải
                else if (_boneFrontLowLegR == null && n.Contains("frontlowleg.r"))
                {
                    _boneFrontLowLegR = b;
                    _rotInitFrontLowR = b.localRotation;
                }
                // Chân sau trái (Back Leg Left)
                else if (_boneBackLegL == null && (n.Contains("backleg.l") || n.Contains("backupleg.l")))
                {
                    _boneBackLegL = b;
                    _rotInitBackLegL = b.localRotation;
                }
                // Chân sau phải (Back Leg Right)
                else if (_boneBackLegR == null && (n.Contains("backleg.r") || n.Contains("backupleg.r")))
                {
                    _boneBackLegR = b;
                    _rotInitBackLegR = b.localRotation;
                }
                // Cẳng chân sau trái
                else if (_boneBackLowLegL == null && n.Contains("backlowleg.l"))
                {
                    _boneBackLowLegL = b;
                    _rotInitBackLowL = b.localRotation;
                }
                // Cẳng chân sau phải
                else if (_boneBackLowLegR == null && n.Contains("backlowleg.r"))
                {
                    _boneBackLowLegR = b;
                    _rotInitBackLowR = b.localRotation;
                }
                // Đuôi các đoạn (Tail segments)
                else if (_boneTail1 == null && n.Contains("tail1"))
                {
                    _boneTail1 = b;
                    _rotInitTail1 = b.localRotation;
                }
                else if (_boneTail2 == null && (n.Contains("tail2") || n.Contains("tail3")))
                {
                    _boneTail2 = b;
                    _rotInitTail2 = b.localRotation;
                }
                else if (_boneTail3 == null && (n.Contains("tail4") || n.Contains("tail5")))
                {
                    _boneTail3 = b;
                    _rotInitTail3 = b.localRotation;
                }
                // Cổ & Đầu (Neck & Head)
                else if (_boneNeck == null && n.Contains("neck"))
                {
                    _boneNeck = b;
                    _rotInitNeck = b.localRotation;
                }
                else if (_boneHead == null && n.Contains("head"))
                {
                    _boneHead = b;
                    _rotInitHead = b.localRotation;
                }
                // Thân mình (Body/Torso)
                else if (_boneBody == null && (n.Contains("body") || n.Contains("torso") || n.Contains("hips")))
                {
                    _boneBody = b;
                    _rotInitBody = b.localRotation;
                }
            }

            _bonesInitialized = (_boneFrontLegL != null || _boneBackLegL != null);
            Debug.Log($"<color=cyan>[Ankylosaurus]</color> Bones initialized: FL={_boneFrontLegL != null}, FR={_boneFrontLegR != null}, BL={_boneBackLegL != null}, BR={_boneBackLegR != null}, Tail={_boneTail1 != null}");
        }

        protected override IDamageable ScanForBlockers()
        {
            if (_IsNavigatingBreach) return null;
            return base.ScanForBlockers();
        }

        protected override void PerformAttack(IDamageable target)
        {
            base.PerformAttack(target);
            StartCoroutine(AnkyTailSmashRoutine());
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
        }

        protected override void LateUpdate()
        {
            if (!_bonesInitialized)
            {
                InitializeBones();
                if (!_bonesInitialized) return;
            }

            float speed = Agent != null && Agent.isOnNavMesh ? Agent.velocity.magnitude : 0f;
            bool isMoving = speed > 0.15f;

            // 1. XỬ LÝ ĐỘNG TÁC TẤN CÔNG ĐẬP ĐUÔI (Tail Smash Attack)
            if (_attackProgress >= 0f)
            {
                float smashCurve = Mathf.Sin(_attackProgress * Mathf.PI);
                // Vung đuôi mạnh 50 độ sang trái rồi quất ngược sang phải
                float tailSwing = smashCurve * 48f;
                float headRam = -smashCurve * 15f;

                if (_boneTail1 != null) _boneTail1.localRotation = _rotInitTail1 * Quaternion.Euler(0f, tailSwing * 0.4f, 0f);
                if (_boneTail2 != null) _boneTail2.localRotation = _rotInitTail2 * Quaternion.Euler(0f, tailSwing * 0.7f, 0f);
                if (_boneTail3 != null) _boneTail3.localRotation = _rotInitTail3 * Quaternion.Euler(0f, tailSwing, 0f);
                if (_boneHead != null) _boneHead.localRotation = _rotInitHead * Quaternion.Euler(headRam, tailSwing * 0.2f, 0f);
                if (_boneBody != null) _boneBody.localRotation = _rotInitBody * Quaternion.Euler(0f, -tailSwing * 0.25f, smashCurve * 4f);
                return;
            }

            // 2. DÁNG ĐI 4 CHÂN CHUYÊN NGHIỆP (Quadruped Stride Gait)
            if (isMoving)
            {
                float gaitSpeed = Mathf.Clamp(speed * 3.8f, 3.0f, 7.5f);
                _walkPhase += Time.deltaTime * gaitSpeed;
                _tailWagPhase += Time.deltaTime * (gaitSpeed * 0.65f);

                // Góc sải bước chân trước và sau (24 độ swing)
                float strideAngle = Mathf.Sin(_walkPhase) * 24f;

                // Độ nhấc chân (knee lift) khi bước tới để chân không bị lết mặt đất
                float liftFL = Mathf.Max(0f, Mathf.Sin(_walkPhase)) * 16f;
                float liftFR = Mathf.Max(0f, -Mathf.Sin(_walkPhase)) * 16f;
                float liftBL = Mathf.Max(0f, -Mathf.Sin(_walkPhase)) * 14f;
                float liftBR = Mathf.Max(0f, Mathf.Sin(_walkPhase)) * 14f;

                // --- Bước đi đan chéo (Diagonal Pair: FL+BR vs FR+BL) ---
                // Cặp 1: Chân Trước Trái (FL) + Chân Sau Phải (BR) cùng tiến
                if (_boneFrontLegL != null)
                {
                    _boneFrontLegL.localRotation = _rotInitFrontLegL * Quaternion.Euler(strideAngle, 0f, 0f);
                }
                if (_boneFrontLowLegL != null)
                {
                    _boneFrontLowLegL.localRotation = _rotInitFrontLowL * Quaternion.Euler(-liftFL, 0f, 0f);
                }

                if (_boneBackLegR != null)
                {
                    _boneBackLegR.localRotation = _rotInitBackLegR * Quaternion.Euler(strideAngle, 0f, 0f);
                }
                if (_boneBackLowLegR != null)
                {
                    _boneBackLowLegR.localRotation = _rotInitBackLowR * Quaternion.Euler(liftBR, 0f, 0f);
                }

                // Cặp 2: Chân Trước Phải (FR) + Chân Sau Trái (BL) cùng lùi/nghịch pha
                if (_boneFrontLegR != null)
                {
                    _boneFrontLegR.localRotation = _rotInitFrontLegR * Quaternion.Euler(-strideAngle, 0f, 0f);
                }
                if (_boneFrontLowLegR != null)
                {
                    _boneFrontLowLegR.localRotation = _rotInitFrontLowR * Quaternion.Euler(-liftFR, 0f, 0f);
                }

                if (_boneBackLegL != null)
                {
                    _boneBackLegL.localRotation = _rotInitBackLegL * Quaternion.Euler(-strideAngle, 0f, 0f);
                }
                if (_boneBackLowLegL != null)
                {
                    _boneBackLowLegL.localRotation = _rotInitBackLowL * Quaternion.Euler(liftBL, 0f, 0f);
                }

                // --- Đuôi gai lắc lư uyển chuyển (Tail Sway) ---
                float tailYaw = Mathf.Sin(_tailWagPhase) * 18f;
                if (_boneTail1 != null) _boneTail1.localRotation = _rotInitTail1 * Quaternion.Euler(0f, tailYaw * 0.4f, 0f);
                if (_boneTail2 != null) _boneTail2.localRotation = _rotInitTail2 * Quaternion.Euler(0f, tailYaw * 0.7f, 0f);
                if (_boneTail3 != null) _boneTail3.localRotation = _rotInitTail3 * Quaternion.Euler(0f, tailYaw, 0f);

                // --- Đầu & Cổ gật gù theo nhịp bước (Head & Neck Bobbing) ---
                float headPitch = Mathf.Sin(_walkPhase * 2f) * 5.5f;
                float headYaw = Mathf.Cos(_walkPhase) * 4.0f;
                if (_boneNeck != null) _boneNeck.localRotation = _rotInitNeck * Quaternion.Euler(headPitch * 0.5f, headYaw * 0.5f, 0f);
                if (_boneHead != null) _boneHead.localRotation = _rotInitHead * Quaternion.Euler(headPitch, headYaw, 0f);

                // --- Thân mình chuyển dịch trọng tâm (Body Waddle & Rolling) ---
                float bodyRoll = Mathf.Sin(_walkPhase) * 3.8f;
                float bodyYaw = Mathf.Cos(_walkPhase) * 2.2f;
                if (_boneBody != null) _boneBody.localRotation = _rotInitBody * Quaternion.Euler(0f, bodyYaw, bodyRoll);

                if (VisualModel != null && _HasBaseModelTransform)
                {
                    float bobY = Mathf.Abs(Mathf.Sin(_walkPhase * 2f)) * 0.065f;
                    VisualModel.localPosition = _BaseModelLocalPos + new Vector3(0f, bobY, 0f);
                }
            }
            else
            {
                // Khi đứng yên (Idle): hồi phục mượt mà về tư thế tự nhiên chuẩn
                float lerpSpeed = Time.deltaTime * 6f;
                if (_boneFrontLegL != null) _boneFrontLegL.localRotation = Quaternion.Slerp(_boneFrontLegL.localRotation, _rotInitFrontLegL, lerpSpeed);
                if (_boneFrontLowLegL != null) _boneFrontLowLegL.localRotation = Quaternion.Slerp(_boneFrontLowLegL.localRotation, _rotInitFrontLowL, lerpSpeed);
                if (_boneFrontLegR != null) _boneFrontLegR.localRotation = Quaternion.Slerp(_boneFrontLegR.localRotation, _rotInitFrontLegR, lerpSpeed);
                if (_boneFrontLowLegR != null) _boneFrontLowLegR.localRotation = Quaternion.Slerp(_boneFrontLowLegR.localRotation, _rotInitFrontLowR, lerpSpeed);

                if (_boneBackLegL != null) _boneBackLegL.localRotation = Quaternion.Slerp(_boneBackLegL.localRotation, _rotInitBackLegL, lerpSpeed);
                if (_boneBackLowLegL != null) _boneBackLowLegL.localRotation = Quaternion.Slerp(_boneBackLowLegL.localRotation, _rotInitBackLowL, lerpSpeed);
                if (_boneBackLegR != null) _boneBackLegR.localRotation = Quaternion.Slerp(_boneBackLegR.localRotation, _rotInitBackLegR, lerpSpeed);
                if (_boneBackLowLegR != null) _boneBackLowLegR.localRotation = Quaternion.Slerp(_boneBackLowLegR.localRotation, _rotInitBackLowR, lerpSpeed);

                // Đuôi vẫy nhẹ khi nghỉ (Idle tail wag)
                float idleTail = Mathf.Sin(Time.time * 1.5f) * 6f;
                if (_boneTail1 != null) _boneTail1.localRotation = Quaternion.Slerp(_boneTail1.localRotation, _rotInitTail1 * Quaternion.Euler(0f, idleTail * 0.4f, 0f), lerpSpeed);
                if (_boneTail2 != null) _boneTail2.localRotation = Quaternion.Slerp(_boneTail2.localRotation, _rotInitTail2 * Quaternion.Euler(0f, idleTail * 0.7f, 0f), lerpSpeed);
                if (_boneTail3 != null) _boneTail3.localRotation = Quaternion.Slerp(_boneTail3.localRotation, _rotInitTail3 * Quaternion.Euler(0f, idleTail, 0f), lerpSpeed);

                if (_boneNeck != null) _boneNeck.localRotation = Quaternion.Slerp(_boneNeck.localRotation, _rotInitNeck, lerpSpeed);
                if (_boneHead != null) _boneHead.localRotation = Quaternion.Slerp(_boneHead.localRotation, _rotInitHead, lerpSpeed);
                if (_boneBody != null) _boneBody.localRotation = Quaternion.Slerp(_boneBody.localRotation, _rotInitBody, lerpSpeed);

                if (VisualModel != null && _HasBaseModelTransform)
                {
                    VisualModel.localPosition = Vector3.Lerp(VisualModel.localPosition, _BaseModelLocalPos, lerpSpeed);
                }
            }
        }
    }
}
