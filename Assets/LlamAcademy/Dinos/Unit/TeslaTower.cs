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
        [SerializeField] private float AttackRange = 9.0f;
        [SerializeField] private float AttackInterval = 1.2f;
        [SerializeField] private int DamagePerHit = 25;
        [SerializeField] private int MaxChains = 4;
        [SerializeField] private float ChainRadius = 6.0f;
        [SerializeField] private LayerMask EnemyLayers;
        [SerializeField] private LineRenderer LightningLinePrefab;
        [SerializeField] private Transform FirePoint;

        private float LastAttackTime;
        private Collider[] OverlapBuffer = new Collider[20];

        protected override void Start()
        {
            base.Start();
            if (EnemyLayers.value == 0) EnemyLayers = ~LayerMask.GetMask("Ignore Raycast");
            if (FirePoint == null) FirePoint = transform;
        }

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;

            if (Time.time >= LastAttackTime + AttackInterval)
            {
                TryDischargeLightning();
            }
        }

        private void TryDischargeLightning()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
            if (count == 0) return;

            // Find closest enemy
            Collider primaryTarget = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (OverlapBuffer[i] == null) continue;
                float d = (OverlapBuffer[i].transform.position - transform.position).sqrMagnitude;
                if (d < closestDist)
                {
                    closestDist = d;
                    primaryTarget = OverlapBuffer[i];
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
                line = lineObj.AddComponent<LineRenderer>();
                line.startWidth = 0.15f;
                line.endWidth = 0.05f;
                line.material = new Material(Shader.Find("Sprites/Default"));
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
