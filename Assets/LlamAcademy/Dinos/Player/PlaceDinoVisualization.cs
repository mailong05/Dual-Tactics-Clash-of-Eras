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
                if (SafeZone != null && SafeZone.activeSelf && SafeZone.TryGetComponent(out Renderer r))
                {
                    StartCoroutine(FadeOut(r.material));
                }
                return;
            }

            if (SafeZone != null && !SafeZone.activeSelf && SafeZone.TryGetComponent(out Renderer safeR))
            {
                StartCoroutine(FadeIn(safeR.material));
            }

            for (int i = 0; i < Count; i++)
            {
                Quaternion rot = Quaternion.identity;
                if (RoundManager.Instance != null && RoundManager.Instance.DinoTarget != null)
                {
                    rot = Quaternion.LookRotation((RoundManager.Instance.DinoTarget.position - transform.position).normalized);
                }
                Unit.Unit dino = Instantiate(dinoSO.Prefab, transform.position, rot, transform);
                dino.enabled = false; // Vô hiệu hóa để không bị coi là unit chiến đấu thật

                foreach (Collider collider in dino.GetComponentsInChildren<Collider>())
                {
                    collider.enabled = false;
                }
                if (dino.TryGetComponent(out NavMeshAgent agent))
                {
                    agent.enabled = false;
                }
                dino.transform.localPosition = Vector3.zero;
                Visualizations.Add(dino.gameObject);
                Renderer renderer = dino.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Renderers.Add(renderer);
                }
            }
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
                    if (i < Renderers.Count) Renderers.RemoveAt(i);
                    continue;
                }

                bool canAfford = DinoSpawner.Instance != null && DinoSpawner.Instance.ResourcesToSpend >= Dino.Cost;
                bool isBlocked = Physics.OverlapSphereNonAlloc(
                    Visualizations[i].transform.position,
                    0.25f,
                    Hits,
                    UnsafeLayers) > 0;

                bool isValid = canAfford && !isBlocked;
                IsValidPlacementLocation = isValid;

                if (i < Renderers.Count && Renderers[i] != null)
                {
                    Color targetColor = isValid ? Color.cyan : Color.red;
                    Material mat = Renderers[i].material;
                    if (mat != null)
                    {
                        if (mat.HasProperty(TINT)) mat.SetColor(TINT, targetColor);
                        if (mat.HasProperty(FRESNEL_COLOR)) mat.SetColor(FRESNEL_COLOR, targetColor);
                    }
                }
            }
        }
    }
}
