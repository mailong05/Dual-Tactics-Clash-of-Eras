using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.RoundManagement;
using UnityEngine;
using UnityEngine.AI;

namespace LlamAcademy.Dinos.Player
{
    public class PlaceDinoVisualization : MonoBehaviour
    {
        public bool IsValidPlacementLocation { get; private set; }

        // [SerializeField] private float SafeSpawnDistance = 15f;
        [SerializeField] private List<GameObject> Visualizations = new();
        [SerializeField] private LayerMask UnsafeLayers;

        [SerializeField] private SafeZoneVisualizer SafeZonePrefab;
        [SerializeField] private GameObject WorldRoot;

        private List<Renderer> Renderers = new();
        private Collider[] Hits = new Collider[1];
        private int Count = 1;
        private DinoSO Dino;
        [SerializeField] private GameObject SafeZone;

        private static readonly int TINT = Shader.PropertyToID("_Tint");
        private static readonly int FRESNEL_COLOR = Shader.PropertyToID("_FresnelColor");

        private void Start()
        {
            if (SafeZone != null) SafeZone.SetActive(false);
            // If you're not using a shader to show this, this code works nicely :)
            // List<Collider> colliders = WorldRoot.GetComponentsInChildren<Collider>().Where(collider => collider.enabled && !collider.isTrigger && (UnsafeLayers | 1 << collider.gameObject.layer) == UnsafeLayers).ToList();
            // SafeZone = new  GameObject("Safe Zone");
            // foreach (Collider collider in colliders)
            // {
            //     float radius = (collider.bounds.extents.x + collider.bounds.extents.z) / 2f + SafeSpawnDistance;
            //     SafeZoneVisualizer safeZone = Instantiate(SafeZonePrefab, collider.transform.position, Quaternion.identity, SafeZone.transform);
            //     safeZone.name = $"Safe Zone for {collider.name}";
            //     safeZone.transform.localScale = new Vector3(radius, radius, radius);
            //     safeZone.transform.position = collider.transform.position;
            //     // Assume default layer is unsafe, so good enough.
            // }

            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange += OnGameStateChange;
            }
        }

        private void OnDestroy()
        {
            if (RoundManager.Instance != null)
            {
                RoundManager.Instance.OnGameStateChange -= OnGameStateChange;
            }
        }

        private void OnGameStateChange(GameState oldState, GameState newState)
        {
            if (newState == GameState.Running)
            {
                if (SafeZone != null && SafeZone.activeSelf && SafeZone.TryGetComponent(out Renderer r))
                {
                    StartCoroutine(FadeOut(r.material));
                }
            }
        }

        private IEnumerator FadeIn(Material material)
        {
            if (SafeZone != null) SafeZone.SetActive(true);
            float time = 0;
            int STRENGTH_PROPERTY = Shader.PropertyToID("_Strength");
            while (time < 1)
            {
                if (material != null) material.SetFloat(STRENGTH_PROPERTY, time);
                time += Time.deltaTime * 4;
                yield return null;
            }

            if (material != null) material.SetFloat(STRENGTH_PROPERTY, 1);
        }

        private IEnumerator FadeOut(Material material)
        {
            float time = 1;
            int STRENGTH_PROPERTY = Shader.PropertyToID("_Strength");
            while (time > 0)
            {
                if (material != null) material.SetFloat(STRENGTH_PROPERTY, time);
                time -= Time.deltaTime * 4;
                yield return null;
            }

            if (material != null) material.SetFloat(STRENGTH_PROPERTY, 0);
            if (SafeZone != null) SafeZone.SetActive(false);
        }

        public void ChangeDino(DinoSO dinoSO)
        {
            foreach (GameObject go in Visualizations)
            {
                if (go != null) Destroy(go.gameObject);
            }

            Visualizations.Clear();
            Renderers.Clear();
            Dino = dinoSO;

            if (dinoSO == null)
            {
                if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);
                return;
            }

            Unit.Unit prefab = dinoSO.Prefab;
            if (prefab == null)
            {
                string soName = dinoSO.name.ToLower();
                GameObject resGo = null;
                if (soName.Contains("velo") || soName.Contains("raptor")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Velociraptor");
                else if (soName.Contains("ankyl")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Ankylosaurus");
                else if (soName.Contains("ptero")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_Pterodactyl");
                else if (soName.Contains("trex") || soName.Contains("t-rex")) resGo = Resources.Load<GameObject>("Prefabs/Prehistoric/Prefab_TRexBoss");
                if (resGo != null) prefab = resGo.GetComponent<Unit.Unit>();
            }

            if (prefab == null)
            {
                if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);
                return;
            }

            if (SafeZone != null && SafeZone.activeSelf) SafeZone.SetActive(false);

            for (int i = 0; i < Count; i++)
            {
                Quaternion rot = Quaternion.identity;
                if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
                {
                    rot = Quaternion.LookRotation((RoundManager.Instance.DinoTarget.position - transform.position).normalized);
                }

                Transform modelSource = prefab.transform.Find("Model");
                GameObject ghostGo = null;
                if (modelSource != null)
                {
                    ghostGo = Instantiate(modelSource.gameObject, transform.position, rot, transform);
                    ghostGo.transform.localScale = modelSource.localScale;
                }
                else
                {
                    ghostGo = Instantiate(prefab.gameObject, transform.position, rot, transform);
                    ghostGo.transform.localScale = prefab.transform.localScale;
                }

                ghostGo.name = "GhostVisual_" + prefab.name;
                ghostGo.transform.localPosition = Vector3.zero;

                // Triệt tiêu 100% toàn bộ component logic, AI, NavMeshAgent, Collider để không bao giờ can thiệp vào game
                foreach (var nav in ghostGo.GetComponentsInChildren<NavMeshAgent>(true)) DestroyImmediate(nav);
                foreach (var u in ghostGo.GetComponentsInChildren<Unit.Unit>(true)) DestroyImmediate(u);
                foreach (var collider in ghostGo.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
                foreach (var rb in ghostGo.GetComponentsInChildren<Rigidbody>(true)) DestroyImmediate(rb);
                foreach (var a in ghostGo.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(a);
                foreach (var anim in ghostGo.GetComponentsInChildren<Animator>(true)) anim.enabled = false;

                Visualizations.Add(ghostGo);

                Renderer[] renderers = ghostGo.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r != null)
                    {
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        Renderers.Add(r);
                    }
                }
            }
        }

        private bool _IsOnNavMesh = true;

        public void SetPlacementValidity(bool canAfford, bool onNavMesh)
        {
            _IsOnNavMesh = onNavMesh;
            IsValidPlacementLocation = canAfford && onNavMesh;
        }

        private void Update()
        {
            if (Dino == null || Visualizations.Count == 0)
            {
                IsValidPlacementLocation = false;
                return;
            }

            for (int i = Visualizations.Count - 1; i >= 0; i--)
            {
                if (Visualizations[i] == null)
                {
                    Visualizations.RemoveAt(i);
                    continue;
                }
            }

            bool canAfford = DinoSpawner.Instance != null && DinoSpawner.Instance.ResourcesToSpend >= Dino.Cost;
            bool isValid = canAfford && _IsOnNavMesh;
            IsValidPlacementLocation = isValid;

            Color targetColor = isValid ? new Color(0.2f, 1f, 0.35f, 0.75f) : new Color(1f, 0.25f, 0.25f, 0.75f);
            for (int r = 0; r < Renderers.Count; r++)
            {
                Renderer rend = Renderers[r];
                if (rend == null) continue;
                foreach (var mat in rend.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty(TINT)) mat.SetColor(TINT, targetColor);
                    if (mat.HasProperty(FRESNEL_COLOR)) mat.SetColor(FRESNEL_COLOR, targetColor);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", targetColor);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", targetColor);
                }
            }
        }
    }
}
