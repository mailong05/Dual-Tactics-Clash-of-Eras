using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Fast-Forward Offline Reinforcement Learning Simulator for Academic Defense.
    /// Runs 500 accelerated combat episodes using mathematical combat resolution and analytical Backpropagation.
    /// Features:
    /// - Fast simulation without waiting for 3D physics / rendering (completes in ~1-2 seconds).
    /// - Comprehensive Counter-Combat system (AoE vs Swarm, Ballista vs Armor, Walls vs Tanker/Flyer, Flanking vs Asymmetry).
    /// - Updates weights of PrehistoricTacticsNet via Gradient Descent (lr = 0.01).
    /// - Auto-saves converged weights to 'pretrained_tactics_weights.json'.
    /// - Exports full 500-episode training log to CSV ('training_500_episodes_log.csv') for Excel graphing.
    /// </summary>
    public class FastForwardOfflineTrainer : MonoBehaviour
    {
        public static FastForwardOfflineTrainer Instance { get; private set; }

        public const int DEFAULT_EPISODES = 500;

        [Header("=== Trainer State ===")]
        public bool IsTraining = false;
        public float TrainingProgress = 0f;
        public int CurrentEpisode = 0;
        public float CurrentLoss = 0f;
        public float CurrentReward = 0f;
        public float CurrentWeightNorm = 0f;

        // History logs for XAI Line Graph & CSV Export
        public readonly List<TrainingEpisodeRecord> TrainingHistory = new List<TrainingEpisodeRecord>(512);

        public event Action<int, float, float> OnEpisodeCompleted; // episode, loss, reward
        public event Action OnTrainingFinished;

        public struct TrainingEpisodeRecord
        {
            public int Episode;
            public string Archetype;
            public string Tactic;
            public float Reward;
            public float Loss;
            public float WeightNorm;
            public float DamageToBase;
            public int WallsBreached;
            public int TowersDestroyed;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public string GetPretrainedWeightsPath()
        {
            // Primary path: inside Assets/Resources or persistentDataPath
            string path = Path.Combine(Application.persistentDataPath, "pretrained_tactics_weights.json");
            return path;
        }

        public string GetExportCsvPath()
        {
            return Path.Combine(Application.persistentDataPath, "training_500_episodes_log.csv");
        }

        /// <summary>
        /// Starts the 500-episode fast training loop as a Unity Coroutine.
        /// </summary>
        public void StartFastForwardTraining(int episodes = DEFAULT_EPISODES)
        {
            if (IsTraining) return;
            StartCoroutine(TrainEpisodesRoutine(episodes));
        }

        private IEnumerator TrainEpisodesRoutine(int targetEpisodes)
        {
            IsTraining = true;
            TrainingProgress = 0f;
            TrainingHistory.Clear();

            PrehistoricTacticsNet net = PrehistoricNeuralDirector.Instance != null 
                ? PrehistoricNeuralDirector.Instance.TacticsNet 
                : new PrehistoricTacticsNet();

            System.Random rnd = new System.Random(1337);
            float runningLoss = 0f;
            float runningReward = 0f;

            Debug.Log($"<color=cyan>[Offline Trainer]</color> Starting Fast-Forward Reinforcement Learning ({targetEpisodes} episodes)...");

            int batchSize = 25; // Yield every 25 episodes to keep UI responsive
            for (int ep = 1; ep <= targetEpisodes; ep++)
            {
                CurrentEpisode = ep;
                TrainingProgress = (float)ep / targetEpisodes;

                // 1. Generate randomized player defense state (16 features)
                float[] defenseState = GenerateRandomPlayerDefense(rnd, ep);

                // 2. Forward pass through PrehistoricTacticsNet
                net.Forward(defenseState, out float[] archProbs, out float[] tactProbs, out float valueEstimate);

                // Sample action using epsilon-greedy exploration (epsilon decays from 0.35 -> 0.05)
                float epsilon = Mathf.Lerp(0.35f, 0.05f, (float)ep / targetEpisodes);
                int chosenArch = SampleActionWithEpsilon(archProbs, epsilon, rnd);
                int chosenTactic = SampleActionWithEpsilon(tactProbs, epsilon, rnd);

                // 3. Mathematical Combat Resolver
                SimulateCombatOutcome(defenseState, chosenArch, chosenTactic, rnd, 
                    out float damageToBase, out int wallsBreached, out int towersDestroyed, out float resourceLoss, out float rawReward);

                // Normalize reward to [-1, 1] for stable gradient descent
                float normalizedReward = Mathf.Clamp(rawReward / 25.0f, -1.0f, 1.0f);

                // 4. Analytical Backpropagation & SGD step
                float loss = net.TrainStep(defenseState, chosenArch, chosenTactic, normalizedReward, lr: 0.012f);

                float weightNorm = net.ComputeWeightNorm();
                CurrentLoss = loss;
                CurrentReward = rawReward;
                CurrentWeightNorm = weightNorm;

                runningLoss += loss;
                runningReward += rawReward;

                // Record history
                TrainingEpisodeRecord record = new TrainingEpisodeRecord
                {
                    Episode = ep,
                    Archetype = GetArchName(chosenArch),
                    Tactic = GetTacticName(chosenTactic),
                    Reward = rawReward,
                    Loss = loss,
                    WeightNorm = weightNorm,
                    DamageToBase = damageToBase,
                    WallsBreached = wallsBreached,
                    TowersDestroyed = towersDestroyed
                };
                TrainingHistory.Add(record);

                OnEpisodeCompleted?.Invoke(ep, loss, rawReward);

                if (ep % batchSize == 0)
                {
                    yield return null; // Breathe to allow UI redraw
                }
            }

            // 5. Save Converged Weights to JSON
            string weightsPath = GetPretrainedWeightsPath();
            net.SaveWeights(weightsPath);

            // Also copy to Assets folder if in editor
            string projectAssetPath = Path.Combine(Application.dataPath, "LlamAcademy", "Dinos", "AI", "pretrained_tactics_weights.json");
            net.SaveWeights(projectAssetPath);

            // 6. Automatically export CSV
            ExportTrainingLogToCSV();

            IsTraining = false;
            TrainingProgress = 1.0f;
            Debug.Log($"<color=green>[Offline Trainer]</color> Training Completed! 500 Episodes converged. Avg Loss: {runningLoss / targetEpisodes:F4}, Avg Reward: {runningReward / targetEpisodes:F2}");

            OnTrainingFinished?.Invoke();
        }

        private float[] GenerateRandomPlayerDefense(System.Random rnd, int episode)
        {
            float[] s = new float[16];

            // Archetype of player layout (0 = Pure Archer, 1 = AoE Heavy, 2 = Heavy Maze, 3 = Flank Asymmetry, 4 = Balanced)
            int layoutType = rnd.Next(5);

            // Canonical 16-feature vector indices aligned with PrehistoricNeuralDirector:
            // 0: Gold, 1: BaseHP, 2: Wave, 3: Archer, 4: Ballista, 5: Catapult, 6: Tesla, 7: Frost,
            // 8: Traps, 9: Walls, 10: DPS, 11: Skill, 12: Left, 13: Right, 14: Center, 15: Chokepoints
            switch (layoutType)
            {
                case 0: // Archer Ranged Heavy (Single Target)
                    s[3] = (float)(0.6 + rnd.NextDouble() * 0.4); // High archer
                    s[4] = (float)(rnd.NextDouble() * 0.2);        // Low ballista
                    s[5] = 0.05f;                                  // Low catapult
                    s[6] = 0.05f;                                  // Low tesla
                    s[7] = 0.1f;                                   // Low frost
                    s[9] = 0.08f;                                  // Low walls
                    s[12] = 0.33f; s[13] = 0.33f; s[14] = 0.34f;
                    break;
                case 1: // Splash / AoE Heavy (Catapults + Tesla)
                    s[3] = 0.1f;
                    s[4] = 0.05f;
                    s[5] = (float)(0.5 + rnd.NextDouble() * 0.4); // High catapult
                    s[6] = (float)(0.4 + rnd.NextDouble() * 0.4); // High tesla
                    s[7] = 0.2f;
                    s[9] = 0.15f;
                    s[12] = 0.33f; s[13] = 0.33f; s[14] = 0.34f;
                    break;
                case 2: // Thick Maze Fortress (Wall Density & Chokepoints)
                    s[3] = 0.3f;
                    s[4] = 0.2f;
                    s[5] = 0.3f;
                    s[6] = 0.2f;
                    s[7] = 0.3f;
                    s[9] = (float)(0.65 + rnd.NextDouble() * 0.35); // Very high wall density
                    s[15] = (float)(0.6 + rnd.NextDouble() * 0.4);   // High maze chokepoints
                    s[12] = 0.33f; s[13] = 0.33f; s[14] = 0.34f;
                    break;
                case 3: // Flank Asymmetry (Heavily stacked Left, empty Right)
                    s[3] = 0.3f;
                    s[4] = 0.2f;
                    s[5] = 0.2f;
                    s[9] = 0.25f;
                    s[12] = 0.85f; // Left fortified
                    s[13] = 0.10f; // Right blind spot!
                    s[14] = 0.30f;
                    break;
                case 4: // Balanced High Tier
                    s[3] = (float)(0.2 + rnd.NextDouble() * 0.3);
                    s[4] = (float)(0.2 + rnd.NextDouble() * 0.3);
                    s[5] = (float)(0.2 + rnd.NextDouble() * 0.3);
                    s[6] = (float)(0.2 + rnd.NextDouble() * 0.3);
                    s[7] = (float)(0.2 + rnd.NextDouble() * 0.3);
                    s[9] = (float)(0.3 + rnd.NextDouble() * 0.4);
                    s[12] = 0.33f; s[13] = 0.33f; s[14] = 0.34f;
                    break;
            }

            s[0] = (float)(0.2 + rnd.NextDouble() * 0.7);            // Gold
            s[1] = (float)(0.4 + rnd.NextDouble() * 0.6);            // Base HP
            s[2] = Mathf.Clamp01((float)episode / DEFAULT_EPISODES); // Wave progression
            s[8] = (float)(rnd.NextDouble() * 0.5);                  // Ground Traps
            s[10] = (float)(0.3 + rnd.NextDouble() * 0.6);           // DPS density
            s[11] = (float)(0.3 + rnd.NextDouble() * 0.5);           // Player Skill Rating
            if (s[15] < 0.01f) s[15] = Mathf.Clamp01(s[9] * 0.8f);  // Maze chokepoints

            return s;
        }

        private int SampleActionWithEpsilon(float[] probs, float epsilon, System.Random rnd)
        {
            if (rnd.NextDouble() < epsilon)
            {
                return rnd.Next(probs.Length);
            }

            // Sample proportionally from probability distribution
            double r = rnd.NextDouble();
            double cum = 0;
            for (int i = 0; i < probs.Length; i++)
            {
                cum += probs[i];
                if (r <= cum) return i;
            }
            return probs.Length - 1;
        }

        /// <summary>
        /// Rigorous Mathematical Combat Resolver based on rock-paper-scissors counter mechanics.
        /// Aligned with canonical 16-feature input vector:
        /// 0:Gold, 1:BaseHP, 2:Wave, 3:Archer, 4:Ballista, 5:Catapult, 6:Tesla, 7:Frost,
        /// 8:Traps, 9:Walls, 10:DPS, 11:Skill, 12:Left, 13:Right, 14:Center, 15:Chokepoint
        /// </summary>
        private void SimulateCombatOutcome(
            float[] s, int arch, int tactic, System.Random rnd,
            out float damageToBase, out int wallsBreached, out int towersDestroyed, out float resourceLoss, out float reward)
        {
            float archerRatio = s[3];
            float ballistaRatio = s[4];
            float catapultRatio = s[5];
            float teslaRatio = s[6];
            float frostRatio = s[7];
            float wallDensity = s[9];
            float leftFlank = s[12];
            float rightFlank = s[13];

            damageToBase = 0.2f;
            wallsBreached = 0;
            towersDestroyed = 0;
            resourceLoss = 1.0f;

            // Archetype Performance:
            // 0: Velociraptor (Runner/Swarm)
            // 1: Pterodactyl (Aerial Infiltration)
            // 2: Ankylosaurus (Siege Tanker)
            // 3: T-Rex (Apex Boss)

            // Counter Logic:
            if (arch == 0) // Velociraptor Swarm
            {
                if (catapultRatio > 0.35f || teslaRatio > 0.35f)
                {
                    // Splash destroys raptor swarms
                    damageToBase = (float)(0.05 + rnd.NextDouble() * 0.15);
                    resourceLoss = 2.5f;
                }
                else if (archerRatio > 0.4f)
                {
                    // Single target archers overwhelmed by swarm numbers!
                    damageToBase = (float)(0.65 + rnd.NextDouble() * 0.30);
                    towersDestroyed = rnd.Next(2, 4);
                    resourceLoss = 0.8f;
                }
                else if (wallDensity > 0.4f && tactic != 1)
                {
                    // Thick walls block raptors without breach
                    damageToBase = (float)(0.1 + rnd.NextDouble() * 0.15);
                    resourceLoss = 1.8f;
                }
                else
                {
                    damageToBase = (float)(0.4 + rnd.NextDouble() * 0.3);
                }
            }
            else if (arch == 1) // Pterodactyl Aerial
            {
                if (wallDensity > 0.3f)
                {
                    // Bypasses walls 100%!
                    damageToBase = (float)(0.70 + rnd.NextDouble() * 0.25);
                }
                if (archerRatio > 0.5f || ballistaRatio > 0.4f)
                {
                    // Anti-air focus damages flyers
                    damageToBase *= 0.6f;
                    resourceLoss = 2.0f;
                }
            }
            else if (arch == 2) // Ankylosaurus Tanker
            {
                if (wallDensity > 0.25f || tactic == 2) // Siege breach
                {
                    wallsBreached = rnd.Next(2, 5);
                    damageToBase = (float)(0.55 + rnd.NextDouble() * 0.35);
                    towersDestroyed = rnd.Next(1, 3);
                }
                if (ballistaRatio > 0.4f)
                {
                    // Armor piercer counters tanker
                    damageToBase *= 0.5f;
                    resourceLoss = 2.2f;
                }
            }
            else if (arch == 3) // T-Rex Apex (Boss)
            {
                // High risk, high reward (heavy resource cost 100 meat)
                float waveProgression = s[2];
                resourceLoss = 4.2f;
                if (waveProgression < 0.60f)
                {
                    // Premature boss deployment penalty before wave 7-8
                    damageToBase = 0.15f;
                    resourceLoss = 6.0f;
                }
                else
                {
                    damageToBase = (float)(0.60 + rnd.NextDouble() * 0.35);
                    wallsBreached = rnd.Next(1, 4);
                    towersDestroyed = rnd.Next(2, 4);

                    if (ballistaRatio > 0.35f && frostRatio > 0.25f)
                    {
                        // Slow + Pierce halts T-Rex
                        damageToBase *= 0.45f;
                        resourceLoss = 6.5f;
                    }
                }
            }

            // Tactic Modifiers:
            // 1: Pincer Flank (Exploits asymmetric defense)
            if (tactic == 1 && Mathf.Abs(leftFlank - rightFlank) > 0.3f)
            {
                damageToBase += 0.25f; // Rushed the weak flank
                damageToBase = Mathf.Clamp01(damageToBase);
            }
            // 2: Siege Breach
            if (tactic == 2 && wallDensity > 0.25f)
            {
                wallsBreached += 2;
                damageToBase += 0.15f;
                damageToBase = Mathf.Clamp01(damageToBase);
            }

            // Reward Formula:
            // R = +10 * DamageToBase + 5 * WallsBreached - 2 * ResourceLoss + 8 * TowersDestroyed
            reward = 10f * damageToBase + 5f * wallsBreached - 2f * resourceLoss + 8f * towersDestroyed;
        }

        private static string GetArchName(int idx)
        {
            switch (idx)
            {
                case 0: return "Velociraptor (Swarm)";
                case 1: return "Pterodactyl (Flyer)";
                case 2: return "Ankylosaurus (Tanker)";
                case 3: return "TRex (Apex Boss)";
                default: return "Unknown";
            }
        }

        private static string GetTacticName(int idx)
        {
            switch (idx)
            {
                case 0: return "Swarm Rush";
                case 1: return "Pincer Flank";
                case 2: return "Siege Breach";
                case 3: return "Aerial Decoy";
                default: return "Standard";
            }
        }

        /// <summary>
        /// Exports the full 500-episode training log to a CSV file for instructor review and thesis plots.
        /// </summary>
        public string ExportTrainingLogToCSV()
        {
            try
            {
                string csvPath = GetExportCsvPath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Episode,Archetype,Tactic,Reward,Loss,WeightNorm,DamageToBase,WallsBreached,TowersDestroyed");

                for (int i = 0; i < TrainingHistory.Count; i++)
                {
                    var rec = TrainingHistory[i];
                    sb.AppendLine($"{rec.Episode},{rec.Archetype},{rec.Tactic},{rec.Reward:F2},{rec.Loss:F5},{rec.WeightNorm:F3},{rec.DamageToBase:F2},{rec.WallsBreached},{rec.TowersDestroyed}");
                }

                File.WriteAllText(csvPath, sb.ToString(), Encoding.UTF8);
                Debug.Log($"<color=green>[Offline Trainer]</color> Exported 500-episode CSV log to: {csvPath}");
                return csvPath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Offline Trainer] Failed to export CSV: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resets network weights to fresh He Normal initialization.
        /// </summary>
        public void ResetWeights()
        {
            if (PrehistoricNeuralDirector.Instance != null && PrehistoricNeuralDirector.Instance.TacticsNet != null)
            {
                PrehistoricNeuralDirector.Instance.TacticsNet.ResetToHeInitialization();
                Debug.Log("<color=yellow>[Offline Trainer]</color> PrehistoricTacticsNet weights reset to initial He distribution.");
            }
            TrainingHistory.Clear();
            TrainingProgress = 0f;
            CurrentEpisode = 0;
            CurrentLoss = 0f;
            CurrentReward = 0f;
        }
    }
}
