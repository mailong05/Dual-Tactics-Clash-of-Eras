using System;
using System.Collections.Generic;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    public class DeadlinessHeatmap : MonoBehaviour
    {
        public static DeadlinessHeatmap Instance { get; private set; }

        [System.Serializable]
        public class DeathRecord
        {
            public Vector3 Position;
            public float TimeStamp;
            public int Round;
            public string KillerType;

            public DeathRecord(Vector3 pos, float time, int round, string killer)
            {
                Position = pos;
                TimeStamp = time;
                Round = round;
                KillerType = killer;
            }
        }

        [Header("Settings")]
        [SerializeField] private float HeatRadius = 6.0f;
        [SerializeField] private float DangerThreshold = 3.0f;
        [SerializeField] private bool ShowGizmos = true;

        private List<DeathRecord> DeathRecords = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RecordDeath(Vector3 position, int currentRound, string killerType = "Unknown")
        {
            DeathRecords.Add(new DeathRecord(position, Time.time, currentRound, killerType));
        }

        /// <summary>
        /// Calculates the danger score at a specific world location based on how many units died there.
        /// </summary>
        public float GetDangerScore(Vector3 position, float searchRadius = -1f)
        {
            if (searchRadius <= 0) searchRadius = HeatRadius;

            float score = 0f;
            float sqrRadius = searchRadius * searchRadius;

            for (int i = 0; i < DeathRecords.Count; i++)
            {
                float sqrDist = (DeathRecords[i].Position - position).sqrMagnitude;
                if (sqrDist <= sqrRadius)
                {
                    // Closer deaths contribute higher danger weight
                    float weight = 1f - (Mathf.Sqrt(sqrDist) / searchRadius);
                    score += weight;
                }
            }

            return score;
        }

        /// <summary>
        /// Checks whether a given position is considered a lethal 'Kill Zone' for monsters.
        /// </summary>
        public bool IsKillZone(Vector3 position)
        {
            return GetDangerScore(position) >= DangerThreshold;
        }

        /// <summary>
        /// Returns all identified cluster centers where deaths concentrate.
        /// </summary>
        public List<Vector3> GetDeadliestClusters(int maxCount = 5)
        {
            List<Vector3> clusters = new();
            if (DeathRecords.Count == 0) return clusters;

            // Simple clustering around high-density points
            List<DeathRecord> candidates = new(DeathRecords);
            candidates.Sort((a, b) => GetDangerScore(b.Position).CompareTo(GetDangerScore(a.Position)));

            foreach (var rec in candidates)
            {
                bool tooClose = false;
                foreach (var cluster in clusters)
                {
                    if (Vector3.Distance(rec.Position, cluster) < HeatRadius)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose && GetDangerScore(rec.Position) >= DangerThreshold)
                {
                    clusters.Add(rec.Position);
                    if (clusters.Count >= maxCount) break;
                }
            }

            return clusters;
        }

        public void DecayOldRecords(int currentRound, int roundsToKeep = 3)
        {
            DeathRecords.RemoveAll(r => (currentRound - r.Round) > roundsToKeep);
        }

        private void OnDrawGizmosSelected()
        {
            if (!ShowGizmos) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            foreach (var rec in DeathRecords)
            {
                Gizmos.DrawSphere(rec.Position + Vector3.up * 0.2f, 0.5f);
            }

            List<Vector3> clusters = GetDeadliestClusters();
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            foreach (var cluster in clusters)
            {
                Gizmos.DrawWireSphere(cluster, HeatRadius);
            }
        }
    }
}
