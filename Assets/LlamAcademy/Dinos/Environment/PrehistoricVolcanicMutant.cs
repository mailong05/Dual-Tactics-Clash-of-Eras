using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Audio;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.RoundManagement;
using LlamAcademy.Dinos.Unit;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Environment
{
    public enum VolcanicMutationType
    {
        MagmaRaptor,
        InfernoPterodactyl,
        ObsidianAnkylosaurus,
        VolcanicTRexBoss,
        GenericMagma
    }

    /// <summary>
    /// Hệ thống khủng long biến dị núi lửa dành riêng cho Bản đồ 2: "🌋 Hẻm Núi Dung Nham" (Volcanic Gorge).
    /// Khủng long hấp thụ năng lượng địa nhiệt sâu trong lòng đất, biến đổi hình dạng (Lava/Obsidian skin, emission nhấp nháy, tro bụi lửa)
    /// và sở hữu các đặc tính chiến đấu độc nhất vô nhị:
    /// - Magma Raptor: Tốc độ cực nhanh, khi chết phát nổ dung nham (AOE Death Explosion) gây sát thương cho tháp và quân làng.
    /// - Inferno Pterodactyl: Bay lượn ném bom dung nham thiêu đốt tháp phòng thủ từ trên không.
    /// - Obsidian Ankylosaurus: Lớp giáp hắc diệu thạch giảm 35% sát thương và phản 25% sát thương lại kẻ tấn công.
    /// - Volcanic T-Rex Boss: Thể tích khổng lồ rực lửa, gầm thét tạo sóng xung kích dung nham và kích nộ toàn bộ Raptor xung quanh!
    /// </summary>
    [DisallowMultipleComponent]
    public class PrehistoricVolcanicMutant : MonoBehaviour
    {
        [Header("Mutation Identity")]
        public VolcanicMutationType MutationType = VolcanicMutationType.GenericMagma;
        public string MutantDisplayName = "Khủng Long Nham Thạch";

        private Unit.Unit _unit;
        private NavMeshAgent _agent;
        private readonly List<Material> _clonedMaterials = new();
        private ParticleSystem _emberAura;

        // Timers & Combat Variables
        private float _abilityTimer = 0f;
        private float _pulseTimer = 0f;
        private bool _isDead = false;

        public static void ApplyMutation(GameObject dinoObj)
        {
            if (dinoObj == null) return;
            if (dinoObj.GetComponent<PrehistoricVolcanicMutant>() != null) return;

            dinoObj.AddComponent<PrehistoricVolcanicMutant>();
        }

        private void Awake()
        {
            _unit = GetComponent<Unit.Unit>();
            _agent = GetComponent<NavMeshAgent>();

            IdentifyMutationType();
            ApplyVisualTransformations();
            ApplyStatMutations();

            if (_unit != null)
            {
                _unit.OnTakeDamage += HandleTakeDamage;
                _unit.OnDeath += HandleDeath;
            }
        }

        private void OnDestroy()
        {
            if (_unit != null)
            {
                _unit.OnTakeDamage -= HandleTakeDamage;
                _unit.OnDeath -= HandleDeath;
            }

            foreach (var mat in _clonedMaterials)
            {
                if (mat != null) Destroy(mat);
            }
            _clonedMaterials.Clear();
        }

        private void IdentifyMutationType()
        {
            string n = gameObject.name.ToLower();

            if (n.Contains("velociraptor") || n.Contains("raptor"))
            {
                MutationType = VolcanicMutationType.MagmaRaptor;
                MutantDisplayName = "🌋 Magma Velociraptor";
            }
            else if (n.Contains("pterodactyl") || GetComponent<FlyingUnit>() != null)
            {
                MutationType = VolcanicMutationType.InfernoPterodactyl;
                MutantDisplayName = "🔥 Inferno Pterodactyl";
            }
            else if (n.Contains("ankylosaurus") || n.Contains("ankylo"))
            {
                MutationType = VolcanicMutationType.ObsidianAnkylosaurus;
                MutantDisplayName = "🛡️ Obsidian Ankylosaurus";
            }
            else if (n.Contains("trex") || n.Contains("boss"))
            {
                MutationType = VolcanicMutationType.VolcanicTRexBoss;
                MutantDisplayName = "👑🌋 Bạo Chúa T-Rex Nham Thạch";
            }
            else
            {
                MutationType = VolcanicMutationType.GenericMagma;
                MutantDisplayName = "🌋 Khủng Long Dung Nham";
            }
        }

        private void ApplyVisualTransformations()
        {
            // 1. Áp dụng vật liệu dung nham / hắc diệu thạch rực lửa
            var renderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer) continue;

                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;

                    Material cloned = new Material(mats[i]);
                    if (MutationType == VolcanicMutationType.ObsidianAnkylosaurus)
                    {
                        // Vỏ giáp đá bazan đen kịt với đường vân lửa
                        cloned.color = new Color(0.20f, 0.18f, 0.18f);
                        if (cloned.HasProperty("_EmissionColor"))
                        {
                            cloned.EnableKeyword("_EMISSION");
                            cloned.SetColor("_EmissionColor", new Color(1.0f, 0.32f, 0.05f) * 2.2f);
                        }
                    }
                    else
                    {
                        // Toàn thân phủ sắc cam đỏ nham thạch rực sáng
                        cloned.color = new Color(1.0f, 0.45f, 0.18f);
                        if (cloned.HasProperty("_EmissionColor"))
                        {
                            cloned.EnableKeyword("_EMISSION");
                            cloned.SetColor("_EmissionColor", new Color(1.0f, 0.35f, 0.05f) * 2.6f);
                        }
                    }
                    mats[i] = cloned;
                    _clonedMaterials.Add(cloned);
                }
                r.materials = mats;
            }

            // 2. Kích thước khổng lồ cho Boss T-Rex
            if (MutationType == VolcanicMutationType.VolcanicTRexBoss)
            {
                transform.localScale *= 1.25f;
            }

            // 3. Khởi tạo vầng tro tàn lửa (Ember Aura) gắn liền trên thân
            BuildEmberAura();
        }

        private void BuildEmberAura()
        {
            GameObject auraObj = new GameObject("[Volcanic_Ember_Aura]");
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = Vector3.up * 0.8f;

            _emberAura = auraObj.AddComponent<ParticleSystem>();
            var main = _emberAura.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 1.2f;
            main.startSize = 0.25f;
            main.maxParticles = 35;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Gradient màu lửa cam -> đỏ rực
            var col = main.startColor;
            col.mode = ParticleSystemGradientMode.Color;
            col.color = new Color(1.0f, 0.45f, 0.10f, 0.85f);

            var emission = _emberAura.emission;
            emission.rateOverTime = MutationType == VolcanicMutationType.VolcanicTRexBoss ? 30f : 15f;

            var shape = _emberAura.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = MutationType == VolcanicMutationType.VolcanicTRexBoss ? 2.5f : 1.0f;

            var velOverLifetime = _emberAura.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);

            var psRenderer = auraObj.GetComponent<ParticleSystemRenderer>();
            Material emberMat = Resources.Load<Material>("Materials/Particle_LavaEmber");
            if (emberMat != null)
            {
                psRenderer.material = emberMat;
            }
        }

        private void ApplyStatMutations()
        {
            if (_unit != null)
            {
                float hpMultiplier = MutationType switch
                {
                    VolcanicMutationType.VolcanicTRexBoss => 1.45f,
                    VolcanicMutationType.ObsidianAnkylosaurus => 1.30f,
                    VolcanicMutationType.MagmaRaptor => 1.15f,
                    _ => 1.20f
                };

                int currentMax = _unit.MaxHealth > 0 ? _unit.MaxHealth : 250;
                int mutatedHp = Mathf.RoundToInt(currentMax * hpMultiplier);
                _unit.MaxHealth = mutatedHp;
                _unit.Health = mutatedHp;
                _unit.EnsureHealthBarAttached();
            }

            if (_agent != null)
            {
                if (MutationType == VolcanicMutationType.MagmaRaptor)
                {
                    // Raptor đột biến cực nhanh
                    _agent.speed *= 1.25f;
                    _agent.acceleration *= 1.35f;
                }
            }
        }

        private void Update()
        {
            if (_isDead) return;

            // 1. Hiệu ứng nhịp đập dung nham (Lava Pulse)
            _pulseTimer += Time.deltaTime * 3.0f;
            float emissionIntensity = 2.0f + Mathf.PingPong(_pulseTimer, 1.5f);
            Color pulseColor = new Color(1.0f, 0.35f, 0.05f) * emissionIntensity;
            foreach (var mat in _clonedMaterials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", pulseColor);
                }
            }

            // 2. Kỹ năng đặc thù theo chủng loài
            _abilityTimer += Time.deltaTime;

            if (MutationType == VolcanicMutationType.InfernoPterodactyl && _abilityTimer >= 4.0f)
            {
                _abilityTimer = 0f;
                DropMagmaBomb();
            }
            else if (MutationType == VolcanicMutationType.VolcanicTRexBoss && _abilityTimer >= 8.5f)
            {
                _abilityTimer = 0f;
                UnleashVolcanicShockwave();
            }
        }

        /// <summary>
        /// Kỹ năng Pterodactyl Hỏa Ngục: Thả bom dung nham thiêu đốt tháp và NPC bên dưới khi bay qua.
        /// </summary>
        private void DropMagmaBomb()
        {
            Vector3 dropPos = transform.position;
            GameObject bomb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bomb.name = "Inferno_Magma_Bomb";
            bomb.transform.position = dropPos;
            bomb.transform.localScale = Vector3.one * 0.7f;

            var r = bomb.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material bMat = new Material(s);
                bMat.color = new Color(1f, 0.25f, 0.05f);
                if (bMat.HasProperty("_EmissionColor"))
                {
                    bMat.EnableKeyword("_EMISSION");
                    bMat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.05f) * 3f);
                }
                r.material = bMat;
            }

            var rb = bomb.AddComponent<Rigidbody>();
            rb.mass = 2.0f;
            rb.linearVelocity = Vector3.down * 4.0f;

            var bombScript = bomb.AddComponent<MagmaBombExplosion>();
            bombScript.Owner = gameObject;
        }

        /// <summary>
        /// Kỹ năng Bạo Chúa T-Rex Nham Thạch: Gầm thét tạo sóng xung kích lửa địa chấn và kích nộ Raptor.
        /// </summary>
        private void UnleashVolcanicShockwave()
        {
            PrehistoricAudioManager.Instance?.PlayDinoSound("trex");

            // Hiệu ứng ánh sáng & vòng lửa
            GameObject waveObj = new GameObject("[Lava_Shockwave_VFX]");
            waveObj.transform.position = transform.position + Vector3.up * 0.3f;
            var light = waveObj.AddComponent<Light>();
            light.color = new Color(1f, 0.4f, 0.05f);
            light.range = 14f;
            light.intensity = 4.5f;
            Destroy(waveObj, 1.2f);

            // Sát thương diện rộng lên tháp và quân phòng thủ
            Collider[] hits = Physics.OverlapSphere(transform.position, 10.0f);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Unit.Unit targetUnit))
                {
                    if (!(targetUnit is PrehistoricDinoBase) && !(targetUnit is Dino) && !(targetUnit is FlyingUnit))
                    {
                        targetUnit.TakeDamage(45);
                    }
                }
            }

            // Kích nộ (Frenzy) toàn bộ Raptor đồng minh xung quanh
            var nearbyMutants = FindObjectsByType<PrehistoricVolcanicMutant>(FindObjectsSortMode.None);
            foreach (var m in nearbyMutants)
            {
                if (m != null && m.MutationType == VolcanicMutationType.MagmaRaptor && Vector3.Distance(transform.position, m.transform.position) <= 12f)
                {
                    m.ApplyFrenzyBuff(4.5f);
                }
            }

            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.ShowAnnouncement("🌋 BẠO CHÚA T-REX PHÁT ĐỘNG SÓNG XUNG KÍCH DUNG NHAM!", 2.5f);
            }
        }

        public void ApplyFrenzyBuff(float duration)
        {
            if (_agent != null)
            {
                StartCoroutine(FrenzyRoutine(duration));
            }
        }

        private IEnumerator FrenzyRoutine(float duration)
        {
            float origSpeed = _agent.speed;
            _agent.speed *= 1.35f;
            yield return new WaitForSeconds(duration);
            if (_agent != null)
            {
                _agent.speed = origSpeed;
            }
        }

        private void HandleTakeDamage(IDamageable damageable, int damage)
        {
            if (_isDead) return;

            // Khủng long Obsidian Ankylosaurus: Phản đòn gai lửa (25% sát thương nhận vào)
            if (MutationType == VolcanicMutationType.ObsidianAnkylosaurus && damage > 5)
            {
                int reflectDmg = Mathf.RoundToInt(damage * 0.25f);
                Collider[] hits = Physics.OverlapSphere(transform.position, 4.0f);
                foreach (var hit in hits)
                {
                    if (hit.TryGetComponent(out Unit.Unit target) && !(target is PrehistoricDinoBase) && !(target is Dino) && !(target is FlyingUnit))
                    {
                        target.TakeDamage(reflectDmg);
                        break;
                    }
                }
            }
        }

        private void HandleDeath(IDamageable diedObject)
        {
            if (_isDead) return;
            _isDead = true;

            // Magma Raptor: Vụ nổ dung nham khi chết (Magma Death Explosion)
            if (MutationType == VolcanicMutationType.MagmaRaptor)
            {
                TriggerMagmaDeathExplosion();
            }
            else if (MutationType == VolcanicMutationType.VolcanicTRexBoss)
            {
                TriggerBossDeathVolcano();
            }
        }

        private void TriggerMagmaDeathExplosion()
        {
            Vector3 pos = transform.position;

            // Tạo luồng sáng bùng nổ nham thạch
            GameObject explosionGo = new GameObject("[Magma_Death_Explosion]");
            explosionGo.transform.position = pos + Vector3.up * 0.5f;

            var light = explosionGo.AddComponent<Light>();
            light.color = new Color(1f, 0.35f, 0.05f);
            light.range = 7f;
            light.intensity = 5.0f;
            Destroy(explosionGo, 1.0f);

            // Sát thương nổ lan 3.5m lên tháp và quân phòng thủ
            Collider[] hits = Physics.OverlapSphere(pos, 3.5f);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Unit.Unit target) && !(target is PrehistoricDinoBase) && !(target is Dino) && !(target is FlyingUnit))
                {
                    target.TakeDamage(50);
                }
            }

            // Để lại vũng lửa cháy trên mặt đất trong 3 giây
            CreateScorchedGround(pos, 3.0f);
        }

        private void TriggerBossDeathVolcano()
        {
            if (TowerPlacer.Instance != null)
            {
                TowerPlacer.Instance.AddGold(150);
            }
            if (PrehistoricGameplayManager.Instance != null)
            {
                PrehistoricGameplayManager.Instance.ShowAnnouncement("🏆 BẠO CHÚA DUNG NHAM ĐÃ BỊ HẠ GỤC! THƯỞNG ĐẶC BIỆT +150 VÀNG! 🏆", 4.0f);
            }
        }

        private void CreateScorchedGround(Vector3 position, float duration)
        {
            GameObject scorch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            scorch.name = "Scorched_Lava_Puddle";
            scorch.transform.position = new Vector3(position.x, 0.02f, position.z);
            scorch.transform.localScale = new Vector3(3.2f, 0.01f, 3.2f);

            var c = scorch.GetComponent<Collider>();
            if (c != null) Destroy(c);

            var r = scorch.GetComponent<Renderer>();
            if (r != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Material mat = new Material(s);
                mat.color = new Color(0.95f, 0.30f, 0.05f, 0.75f);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1f, 0.30f, 0.05f) * 2.5f);
                }
                r.material = mat;
            }

            Destroy(scorch, duration);
        }
    }

    /// <summary>
    /// Viên bom nham thạch rơi từ Pterodactyl hỏa ngục.
    /// </summary>
    public class MagmaBombExplosion : MonoBehaviour
    {
        public GameObject Owner;

        private void OnCollisionEnter(Collision collision)
        {
            Detonate();
        }

        private void Update()
        {
            if (transform.position.y <= 0.2f)
            {
                Detonate();
            }
        }

        private void Detonate()
        {
            Vector3 pos = transform.position;

            // Sát thương nổ lan
            Collider[] hits = Physics.OverlapSphere(pos, 3.0f);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Unit.Unit target) && !(target is PrehistoricDinoBase) && !(target is Dino) && !(target is FlyingUnit))
                {
                    target.TakeDamage(40);
                }
            }

            // Đèn flash nổ
            GameObject fx = new GameObject("[Magma_Bomb_Detonation]");
            fx.transform.position = pos;
            var l = fx.AddComponent<Light>();
            l.color = new Color(1f, 0.35f, 0.05f);
            l.range = 5f;
            l.intensity = 4f;
            Destroy(fx, 0.8f);

            Destroy(gameObject);
        }
    }
}
