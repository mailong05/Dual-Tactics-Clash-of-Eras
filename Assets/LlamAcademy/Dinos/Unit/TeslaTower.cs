using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Unit
{
    public class TeslaTower : Unit
    {
        [Header("Tesla Settings")]
        [SerializeField] private float AttackRange = 18.0f;
        [SerializeField] private float AttackInterval = 1.2f;
        [SerializeField] private int DamagePerHit = 25;
        public void SetDamage(int dmg) => DamagePerHit = dmg;
        [SerializeField] private int MaxChains = 4;
        [SerializeField] private float ChainRadius = 6.0f;
        [SerializeField] private LayerMask EnemyLayers;
        [SerializeField] private LineRenderer LightningLinePrefab;
        [SerializeField] private Transform FirePoint;

        private float LastAttackTime;
        private Collider[] OverlapBuffer = new Collider[64];

        protected override void Start()
        {
            base.Start();
            AttackRange = Mathf.Max(AttackRange, 22.0f);
            if (EnemyLayers.value == 0)
            {
                EnemyLayers = (1 << 8) | (1 << 7) | LayerMask.GetMask("Dinos", "Default");
            }
            if (FirePoint == null) FirePoint = transform;
        }

        protected override void Update()
        {
            base.Update();

            if (!IsCombatActive()) return;

            if (Time.time >= LastAttackTime + AttackInterval)
            {
                TryDischargeLightning();
            }
        }

        private void TryDischargeLightning()
        {
            Vector3 basePos = PrehistoricVillageBase.Instance != null 
                ? PrehistoricVillageBase.Instance.transform.position 
                : new Vector3(-2.23f, 0f, -38.26f);

            Collider primaryTarget = null;
            float bestScore = float.MinValue;

            // 1. Quét ActiveDinos (ưu tiên giật sét quái gần nhà chính)
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
                    float score = 1500f - (distToBase * 10f) - distToTower;
                    if (dino.HasReachedBase) score += 500f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        primaryTarget = col;
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
                    float score = 1600f - (distToBase * 10f) - distToTower;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        primaryTarget = col;
                    }
                }
            }

            if (primaryTarget == null)
            {
                int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
                for (int i = 0; i < count; i++)
                {
                    if (OverlapBuffer[i] == null) continue;
                    if (!IsHostileMonster(OverlapBuffer[i])) continue;
                    float distToTower = Vector3.Distance(transform.position, OverlapBuffer[i].transform.position);
                    float distToBase = Vector3.Distance(OverlapBuffer[i].transform.position, basePos);
                    float score = 1000f - (distToBase * 10f) - distToTower;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        primaryTarget = OverlapBuffer[i];
                    }
                }
            }

            if (primaryTarget == null) return;

            LastAttackTime = Time.time;

            // Chain to other enemies
            List<Vector3> chainPoints = new() { FirePoint.position, primaryTarget.bounds.center };
            HashSet<Collider> hitEnemies = new() { primaryTarget };

            ApplyTeslaDamage(primaryTarget);

            Collider currentSource = primaryTarget;
            for (int step = 1; step < MaxChains; step++)
            {
                int secondaryCount = Physics.OverlapSphereNonAlloc(currentSource.transform.position, ChainRadius, OverlapBuffer, EnemyLayers);
                Collider nextTarget = null;
                float nextDist = float.MaxValue;

                for (int j = 0; j < secondaryCount; j++)
                {
                    if (OverlapBuffer[j] == null || hitEnemies.Contains(OverlapBuffer[j])) continue;
                    if (!IsHostileMonster(OverlapBuffer[j])) continue; // BẢO VỆ ĐỒNG MINH
                    float d = (OverlapBuffer[j].transform.position - currentSource.transform.position).sqrMagnitude;
                    if (d < nextDist)
                    {
                        nextDist = d;
                        nextTarget = OverlapBuffer[j];
                    }
                }

                if (nextTarget != null)
                {
                    hitEnemies.Add(nextTarget);
                    chainPoints.Add(nextTarget.bounds.center);
                    ApplyTeslaDamage(nextTarget);
                    currentSource = nextTarget;
                }
                else
                {
                    break;
                }
            }

            StartCoroutine(DrawLightningEffect(chainPoints));
        }

        private void ApplyTeslaDamage(Collider targetCol)
        {
            if (targetCol.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(DamagePerHit);
            }
        }

        private IEnumerator DrawLightningEffect(List<Vector3> points)
        {
            LineRenderer line = null;
            if (LightningLinePrefab != null)
            {
                line = Instantiate(LightningLinePrefab, transform);
            }
            else
            {
                GameObject lineObj = new GameObject("Tesla_Bolt");
                Destroy(lineObj, 0.5f);
                line = lineObj.AddComponent<LineRenderer>();
                line.startWidth = 0.15f;
                line.endWidth = 0.05f;
                line.material = LlamAcademy.Dinos.Rendering.PrehistoricShaderUtility.CreateLineMaterial(Color.cyan);
                line.startColor = Color.cyan;
                line.endColor = Color.white;
            }

            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());

            yield return new WaitForSeconds(0.15f);

            if (line != null)
            {
                Destroy(line.gameObject);
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
