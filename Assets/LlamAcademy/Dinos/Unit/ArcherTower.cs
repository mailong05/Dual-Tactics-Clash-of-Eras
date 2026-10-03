using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;

namespace LlamAcademy.Dinos.Unit
{
    public class ArcherTower : Unit
    {
        [Header("Archer Mechanics")]
        [SerializeField] private float AttackRange = 16.0f;
        [SerializeField] private float FireInterval = 1.0f;
        [SerializeField] private int ArrowDamage = 28;
        [SerializeField] private float ArrowSpeed = 24.0f;
        [SerializeField] private LayerMask EnemyLayers;
        [SerializeField] private Transform BowPivot;
        [SerializeField] private Transform FirePoint;
        [SerializeField] private GameObject ArrowPrefab;

        private float LastFireTime;
        private Collider[] OverlapBuffer = new Collider[20];

        protected override void Start()
        {
            base.Start();
            if (EnemyLayers.value == 0) EnemyLayers = ~LayerMask.GetMask("Ignore Raycast");
            if (FirePoint == null)
            {
                GameObject fp = new GameObject("FirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0, 4.5f, 0);
                FirePoint = fp.transform;
            }
            if (BowPivot == null) BowPivot = FirePoint;
        }

        protected override void Update()
        {
            base.Update();

            if (RoundManager.Instance != null && RoundManager.Instance.State != GameState.Running) return;

            Collider target = FindTarget();
            if (target != null)
            {
                // Smoothly aim BowPivot toward target
                Vector3 aimDir = (target.bounds.center - FirePoint.position).normalized;
                if (aimDir != Vector3.zero && BowPivot != null)
                {
                    BowPivot.rotation = Quaternion.Slerp(BowPivot.rotation, Quaternion.LookRotation(aimDir), Time.deltaTime * 8f);
                }

                if (Time.time >= LastFireTime + FireInterval)
                {
                    LastFireTime = Time.time;
                    ShootArrow(target);
                }
            }
        }

        private Collider FindTarget()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, AttackRange, OverlapBuffer, EnemyLayers);
            Collider bestTarget = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null || !col.gameObject.activeInHierarchy) continue;
                if (col.TryGetComponent(out IDamageable d) && d.Health > 0)
                {
                    float dist = (col.transform.position - transform.position).sqrMagnitude;
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        bestTarget = col;
                    }
                }
            }
            return bestTarget;
        }

        private void ShootArrow(Collider target)
        {
            Vector3 startPos = FirePoint.position;
            Vector3 targetPos = target.bounds.center;
            Vector3 dir = (targetPos - startPos).normalized;

            GameObject arrow;
            if (ArrowPrefab != null)
            {
                arrow = Instantiate(ArrowPrefab, startPos, Quaternion.LookRotation(dir));
            }
            else
            {
                arrow = CreateProceduralArrow(startPos, dir);
            }

            StartCoroutine(AnimateArrow(arrow, target, targetPos));
        }

        private IEnumerator AnimateArrow(GameObject arrow, Collider target, Vector3 lastKnownPos)
        {
            float elapsed = 0f;
            Vector3 start = arrow.transform.position;

            while (arrow != null)
            {
                Vector3 currentTargetPos = (target != null && target.gameObject.activeInHierarchy)
                    ? target.bounds.center
                    : lastKnownPos;

                float distance = Vector3.Distance(start, currentTargetPos);
                float duration = Mathf.Max(0.05f, distance / ArrowSpeed);

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Small arc trajectory
                Vector3 linear = Vector3.Lerp(start, currentTargetPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * (distance * 0.12f);
                arrow.transform.position = new Vector3(linear.x, linear.y + arc, linear.z);

                Vector3 moveDir = (currentTargetPos - arrow.transform.position).normalized;
                if (moveDir != Vector3.zero) arrow.transform.rotation = Quaternion.LookRotation(moveDir);

                if (t >= 1.0f || (target != null && Vector3.Distance(arrow.transform.position, currentTargetPos) < 0.6f))
                {
                    if (target != null && target.TryGetComponent(out IDamageable damageable))
                    {
                        damageable.TakeDamage(ArrowDamage);
                    }
                    Destroy(arrow);
                    yield break;
                }

                yield return null;
            }
        }

        private GameObject CreateProceduralArrow(Vector3 position, Vector3 direction)
        {
            GameObject arrow = new GameObject("Procedural_Arrow");
            arrow.transform.position = position;
            arrow.transform.rotation = Quaternion.LookRotation(direction);

            // Shaft
            GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.transform.SetParent(arrow.transform);
            shaft.transform.localPosition = Vector3.zero;
            shaft.transform.localRotation = Quaternion.Euler(90, 0, 0);
            shaft.transform.localScale = new Vector3(0.06f, 0.45f, 0.06f);
            if (shaft.TryGetComponent(out Collider c1)) Destroy(c1);

            // Brown wooden tint
            Renderer rend = shaft.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = new Color(0.45f, 0.28f, 0.14f);
            }

            // Arrow tip
            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.transform.SetParent(arrow.transform);
            tip.transform.localPosition = new Vector3(0, 0, 0.45f);
            tip.transform.localScale = new Vector3(0.12f, 0.12f, 0.22f);
            if (tip.TryGetComponent(out Collider c2)) Destroy(c2);

            Renderer tipRend = tip.GetComponent<Renderer>();
            if (tipRend != null)
            {
                tipRend.material.color = new Color(0.3f, 0.3f, 0.35f); // Flint grey
            }

            Destroy(arrow, 4f);
            return arrow;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, AttackRange);
        }

        protected override void OnTargetEnter(IDamageable target) {}
        protected override void OnTargetExit(IDamageable target) {}
        public override void Die()
        {
            RaiseDeathEvent();
            Destroy(gameObject);
        }
    }
}
