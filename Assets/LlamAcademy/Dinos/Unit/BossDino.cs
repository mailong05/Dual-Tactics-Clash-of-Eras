using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class BossDino : PrehistoricDinoBase
    {
        [Header("Boss Apex Settings")]
        [SerializeField] private float RoarRadius = 15.0f;
        [SerializeField] private float RoarStunDuration = 1.5f;
        [SerializeField] private LayerMask TowerLayers;
        [SerializeField] private ParticleSystem RoarVFX;

        private AudioSource _bossAudioSource;
        private static AudioClip s_cachedTrexRoarClip;
        private bool _isRoaring = false;
        public bool IsRoaring => _isRoaring;

        protected override void Awake()
        {
            AISpeciesType = LlamAcademy.Dinos.AI.DinoAISpeciesType.TRexApexBoss;
            MoveSpeed = 3.2f;
            base.Awake();
            ConfigureNavMeshAgent();

            // Cự ly tấn công 3.4m chuẩn xác cho T-Rex (đầu dài ~3m):
            // Triệt tiêu 100% hiện tượng mõm và đầu khủng long đâm xuyên qua tường rào / công trình khi cắn!
            AttackRange = 3.4f;
            if (Agent != null)
            {
                Agent.stoppingDistance = 3.0f;
            }

            MaxHealth = (UnitType != null && UnitType.Health > 0) ? UnitType.Health : 8500;
            Health = MaxHealth;
            AttackDamage = 140;
            BuildingDamageMultiplier = 3.0f;
            GoldReward = 150;

            // Khởi tạo AudioSource cục bộ gắn trực tiếp trên thân T-Rex
            _bossAudioSource = GetComponent<AudioSource>();
            if (_bossAudioSource == null)
            {
                _bossAudioSource = gameObject.AddComponent<AudioSource>();
            }
            _bossAudioSource.playOnAwake = false;
            _bossAudioSource.loop = false;
            _bossAudioSource.spatialBlend = 0.2f; // Âm thanh nổi 3D uy lực nhưng vẫn rõ ở mọi góc camera

            if (s_cachedTrexRoarClip == null)
            {
                s_cachedTrexRoarClip = Resources.Load<AudioClip>("Audio/dffdv-dinosaur-roar-with-screams-and-growls-193210");
            }
        }

        public override void TakeDamage(int damage)
        {
            base.TakeDamage(damage);
            // THEO YÊU CẦU NGƯỜI DÙNG: Khủng long KHÔNG gầm khi bị dính đòn / di chuyển trong trận để tránh ồn ào và giật lag!
            // Chỉ gầm DUY NHẤT một lần khi phá hủy hoàn toàn một công trình phòng thủ.
        }

        protected override void PerformAttack(IDamageable target)
        {
            if (target == null) return;
            int hpBefore = target.Health;
            base.PerformAttack(target);

            // Khi T-Rex phá hủy hoàn toàn mục tiêu:
            // THEO YÊU CẦU NGƯỜI DÙNG: Chỉ gầm DUY NHẤT khi phá hủy Nhà chính HOẶC Chòi/Tháp lớn!
            // Tuyệt đối KHÔNG gầm khi phá hủy hàng rào gỗ cọc cản đường thông thường.
            if (hpBefore > 0 && target.Health <= 0 && !_isRoaring)
            {
                bool isVillageBase = (target is PrehistoricVillageBase) ||
                                     (target.Transform != null && target.Transform.name.ToLower().Contains("village"));

                bool isBigTower = (target is ArcherTower) ||
                                  (target is BallistaTower) ||
                                  (target is CatapultTower) ||
                                  (target is TeslaTower) ||
                                  (target is FrostTower);

                if (!isBigTower && target.Transform != null)
                {
                    string targetName = target.Transform.name.ToLower();
                    if (targetName.Contains("tower") || targetName.Contains("watchtower") || targetName.Contains("chòi") || targetName.Contains("bastion"))
                    {
                        isBigTower = true;
                    }
                }

                if (isVillageBase || isBigTower)
                {
                    TriggerBossRoar();
                }
            }
        }

        private void TriggerBossRoar()
        {
            if (_isRoaring) return;
            _isRoaring = true;

            Debug.Log($"<color=red>[BOSS T-REX]</color> Roars fiercely upon destroying a defense! Nearby defenses stunned for {RoarStunDuration}s!");

            if (RoarVFX != null) RoarVFX.Play();

            // 1. Phát âm thanh tiếng gầm T-Rex chân thực 100% không bao giờ bị im lặng
            PlayRoarAudio();

            // 2. Kích hoạt Mecanim Animation gầm
            if (ModelAnimator != null && ModelAnimator.enabled)
            {
                ModelAnimator.ResetTrigger("Roar");
                ModelAnimator.SetTrigger("Roar");
            }

            // 3. Cho T-Rex đứng gầm trong 1.5s rồi lập tức tiếp tục hành quân mượt mà
            StartCoroutine(BossRoarPauseRoutine(1.5f));

            // Stun các tháp xung quanh
            Collider[] nearbyTowers = Physics.OverlapSphere(transform.position, RoarRadius, TowerLayers);
            foreach (Collider col in nearbyTowers)
            {
                if (col.TryGetComponent(out Unit towerUnit) && !(towerUnit is PrehistoricDinoBase))
                {
                    StartCoroutine(StunTowerRoutine(towerUnit));
                }
            }

            // PRIMAL FRENZY AURA: Tiếp sức mạnh cho toàn bộ bầy khủng long đồng minh xung quanh!
            Collider[] nearbyAllies = Physics.OverlapSphere(transform.position, RoarRadius);
            foreach (Collider col in nearbyAllies)
            {
                if (col.TryGetComponent(out PrehistoricDinoBase allyDino) && allyDino != this)
                {
                    allyDino.ApplyPrimalFrenzy(5.0f, 1.30f);
                }
            }
        }

        private void PlayRoarAudio()
        {
            try
            {
                if (s_cachedTrexRoarClip == null)
                {
                    s_cachedTrexRoarClip = Resources.Load<AudioClip>("Audio/dffdv-dinosaur-roar-with-screams-and-growls-193210");
                }

                if (_bossAudioSource == null)
                {
                    _bossAudioSource = GetComponent<AudioSource>();
                    if (_bossAudioSource == null) _bossAudioSource = gameObject.AddComponent<AudioSource>();
                }

                if (_bossAudioSource != null && s_cachedTrexRoarClip != null)
                {
                    _bossAudioSource.Stop();
                    _bossAudioSource.clip = s_cachedTrexRoarClip;
                    _bossAudioSource.volume = 1.0f;
                    _bossAudioSource.Play();
                }

                // Phát dự phòng qua AudioManager toàn cục
                Audio.PrehistoricAudioManager.Instance?.PlayDinoSound("trex");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BossDino] PlayRoarAudio: {ex.Message}");
            }
        }

        private IEnumerator StunTowerRoutine(Unit tower)
        {
            if (tower == null) yield break;

            tower.enabled = false;
            yield return new WaitForSeconds(RoarStunDuration);
            if (tower != null)
            {
                tower.enabled = true;
            }
        }

        private IEnumerator BossRoarPauseRoutine(float pauseTime)
        {
            if (Agent != null && Agent.isOnNavMesh)
            {
                Agent.isStopped = true;
                Agent.velocity = Vector3.zero;
            }

            yield return new WaitForSeconds(pauseTime);

            if (ModelAnimator != null && ModelAnimator.enabled)
            {
                ModelAnimator.ResetTrigger("Roar");
            }

            _isRoaring = false;

            // Sau khi gầm xong, hồi phục di chuyển và tiếp tục bám theo lộ trình hướng về Nhà chính
            if (Agent != null && Agent.isOnNavMesh && Health > 0)
            {
                Agent.isStopped = false;
                Agent.updateRotation = true;

                if (TargetBase != null)
                {
                    Agent.SetDestination(TargetBase.position);
                }
            }
        }
    }
}
