using System;
using System.IO;
using UnityEngine;

namespace LlamAcademy.Dinos.AI
{
    /// <summary>
    /// Deep Reinforcement Learning Neural Network for Prehistoric Tactical Wave Composition.
    /// Architecture: 16 Input Features -> 32 Dense (LeakyReLU) -> 16 Dense (LeakyReLU) -> Multi-Head Outputs:
    /// - Archetype Head (4 logits -> Softmax): Velociraptor, Pterodactyl, Ankylosaurus, TRex
    /// - Tactic Head (4 logits -> Softmax): Swarm Rush, Pincer Flank, Siege Breach, Aerial Decoy
    /// - Value Head (1 logit -> Sigmoid): Wave Success Probability V(s)
    /// Features:
    /// - Analytical Backpropagation (Policy Gradient + MSE Value Loss) via Stochastic Gradient Descent (SGD)
    /// - Weight Serialization/Deserialization to JSON (pretrained_tactics_weights.json)
    /// - Real-time Activation extraction for Explainable AI (XAI) Dashboard visualization
    /// </summary>
    [System.Serializable]
    public class PrehistoricTacticsNet
    {
        public const int INPUT_DIM = 16;
        public const int HIDDEN1_DIM = 32;
        public const int HIDDEN2_DIM = 16;
        public const int ARCH_DIM = 4;
        public const int TACTIC_DIM = 4;

        // Weights & Biases (Custom serialized via SaveWeights / LoadWeights JSON)
        [NonSerialized] public float[][] W1;      // [INPUT_DIM, HIDDEN1_DIM]
        [NonSerialized] public float[] b1;        // [HIDDEN1_DIM]

        [NonSerialized] public float[][] W2;      // [HIDDEN1_DIM, HIDDEN2_DIM]
        [NonSerialized] public float[] b2;        // [HIDDEN2_DIM]

        [NonSerialized] public float[][] W3_arch; // [HIDDEN2_DIM, ARCH_DIM]
        [NonSerialized] public float[] b3_arch;   // [ARCH_DIM]

        [NonSerialized] public float[][] W3_tact; // [HIDDEN2_DIM, TACTIC_DIM]
        [NonSerialized] public float[] b3_tact;   // [TACTIC_DIM]

        [NonSerialized] public float[] W3_val;    // [HIDDEN2_DIM]
        [NonSerialized] public float b3_val;      // scalar

        // Caches for Backprop and XAI visualization
        [NonSerialized] public float[] LastInput = new float[INPUT_DIM];
        [NonSerialized] public float[] LastZ1 = new float[HIDDEN1_DIM];
        [NonSerialized] public float[] LastA1 = new float[HIDDEN1_DIM];
        [NonSerialized] public float[] LastZ2 = new float[HIDDEN2_DIM];
        [NonSerialized] public float[] LastA2 = new float[HIDDEN2_DIM];
        [NonSerialized] public float[] LastArchProbs = new float[ARCH_DIM];
        [NonSerialized] public float[] LastTactProbs = new float[TACTIC_DIM];
        [NonSerialized] public float LastValue;

        [NonSerialized] public float LastLoss = 0f;
        [NonSerialized] public float LastReward = 0f;
        [NonSerialized] public int TotalTrainSteps = 0;

        public PrehistoricTacticsNet()
        {
            InitializeMatrices();
            ResetToHeInitialization();
        }

        private void InitializeMatrices()
        {
            W1 = new float[INPUT_DIM][];
            for (int i = 0; i < INPUT_DIM; i++) W1[i] = new float[HIDDEN1_DIM];
            b1 = new float[HIDDEN1_DIM];

            W2 = new float[HIDDEN1_DIM][];
            for (int i = 0; i < HIDDEN1_DIM; i++) W2[i] = new float[HIDDEN2_DIM];
            b2 = new float[HIDDEN2_DIM];

            W3_arch = new float[HIDDEN2_DIM][];
            for (int i = 0; i < HIDDEN2_DIM; i++) W3_arch[i] = new float[ARCH_DIM];
            b3_arch = new float[ARCH_DIM];

            W3_tact = new float[HIDDEN2_DIM][];
            for (int i = 0; i < HIDDEN2_DIM; i++) W3_tact[i] = new float[TACTIC_DIM];
            b3_tact = new float[TACTIC_DIM];

            W3_val = new float[HIDDEN2_DIM];
            b3_val = 0f;
        }

        public void ResetToHeInitialization()
        {
            System.Random rnd = new System.Random(42);

            // He (Kaiming) normal initialization: std = sqrt(2 / fan_in)
            InitWeightsHe(W1, INPUT_DIM, rnd);
            InitArray(b1, 0.01f);

            InitWeightsHe(W2, HIDDEN1_DIM, rnd);
            InitArray(b2, 0.01f);

            // Xavier for output heads: std = sqrt(2 / (fan_in + fan_out))
            InitWeightsXavier(W3_arch, HIDDEN2_DIM, ARCH_DIM, rnd);
            InitArray(b3_arch, 0.0f);

            InitWeightsXavier(W3_tact, HIDDEN2_DIM, TACTIC_DIM, rnd);
            InitArray(b3_tact, 0.0f);

            float stdVal = Mathf.Sqrt(2f / HIDDEN2_DIM);
            for (int i = 0; i < HIDDEN2_DIM; i++) W3_val[i] = (float)SampleGaussian(rnd, 0, stdVal);
            b3_val = 0f;

            TotalTrainSteps = 0;
            LastLoss = 0f;
        }

        private void InitWeightsHe(float[][] matrix, int fanIn, System.Random rnd)
        {
            float std = Mathf.Sqrt(2.0f / fanIn);
            for (int i = 0; i < matrix.Length; i++)
            {
                for (int j = 0; j < matrix[i].Length; j++)
                {
                    matrix[i][j] = (float)SampleGaussian(rnd, 0, std);
                }
            }
        }

        private void InitWeightsXavier(float[][] matrix, int fanIn, int fanOut, System.Random rnd)
        {
            float std = Mathf.Sqrt(2.0f / (fanIn + fanOut));
            for (int i = 0; i < matrix.Length; i++)
            {
                for (int j = 0; j < matrix[i].Length; j++)
                {
                    matrix[i][j] = (float)SampleGaussian(rnd, 0, std);
                }
            }
        }

        private void InitArray(float[] arr, float val)
        {
            for (int i = 0; i < arr.Length; i++) arr[i] = val;
        }

        private static double SampleGaussian(System.Random rnd, double mean, double stdDev)
        {
            // Box-Muller transform
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }

        private static float LeakyReLU(float x) => x >= 0 ? x : 0.01f * x;
        private static float LeakyReLUDeriv(float x) => x >= 0 ? 1.0f : 0.01f;
        private static float Sigmoid(float x) => 1.0f / (1.0f + Mathf.Exp(-Mathf.Clamp(x, -15f, 15f)));

        /// <summary>
        /// Forward Inference Pass: Returns Softmax probabilities for Archetypes & Tactics, and Value estimate V(s).
        /// </summary>
        public void Forward(float[] input, out float[] archProbs, out float[] tactProbs, out float valueEstimate)
        {
            Array.Copy(input, LastInput, INPUT_DIM);

            // Layer 1: [16] -> [32]
            for (int j = 0; j < HIDDEN1_DIM; j++)
            {
                float sum = b1[j];
                for (int i = 0; i < INPUT_DIM; i++)
                {
                    sum += input[i] * W1[i][j];
                }
                LastZ1[j] = sum;
                LastA1[j] = LeakyReLU(sum);
            }

            // Layer 2: [32] -> [16]
            for (int j = 0; j < HIDDEN2_DIM; j++)
            {
                float sum = b2[j];
                for (int i = 0; i < HIDDEN1_DIM; i++)
                {
                    sum += LastA1[i] * W2[i][j];
                }
                LastZ2[j] = sum;
                LastA2[j] = LeakyReLU(sum);
            }

            // Head 1: Archetype Softmax [16] -> [4]
            float[] archLogits = new float[ARCH_DIM];
            for (int j = 0; j < ARCH_DIM; j++)
            {
                float sum = b3_arch[j];
                for (int i = 0; i < HIDDEN2_DIM; i++)
                {
                    sum += LastA2[i] * W3_arch[i][j];
                }
                archLogits[j] = sum;
            }
            archProbs = ComputeSoftmax(archLogits);
            Array.Copy(archProbs, LastArchProbs, ARCH_DIM);

            // Head 2: Tactic Softmax [16] -> [4]
            float[] tactLogits = new float[TACTIC_DIM];
            for (int j = 0; j < TACTIC_DIM; j++)
            {
                float sum = b3_tact[j];
                for (int i = 0; i < HIDDEN2_DIM; i++)
                {
                    sum += LastA2[i] * W3_tact[i][j];
                }
                tactLogits[j] = sum;
            }
            tactProbs = ComputeSoftmax(tactLogits);
            Array.Copy(tactProbs, LastTactProbs, TACTIC_DIM);

            // Head 3: Value Head [16] -> [1] (Sigmoid)
            float valSum = b3_val;
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                valSum += LastA2[i] * W3_val[i];
            }
            valueEstimate = Sigmoid(valSum);
            LastValue = valueEstimate;
        }

        private static float[] ComputeSoftmax(float[] logits)
        {
            float max = float.MinValue;
            for (int i = 0; i < logits.Length; i++) if (logits[i] > max) max = logits[i];

            float sum = 0f;
            float[] exp = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                exp[i] = Mathf.Exp(Mathf.Clamp(logits[i] - max, -20f, 20f));
                sum += exp[i];
            }

            float[] probs = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = sum > 0.00001f ? (exp[i] / sum) : (1.0f / logits.Length);
            }
            return probs;
        }

        /// <summary>
        /// Analytical Backpropagation: Updates weights via SGD based on observed combat Reward.
        /// Policy Gradient (Advantage Actor-Critic style) + Mean Squared Error Value Loss.
        /// </summary>
        public float TrainStep(float[] input, int chosenArch, int chosenTactic, float reward, float lr = 0.01f)
        {
            // 1. Forward Pass
            Forward(input, out float[] archProbs, out float[] tactProbs, out float valueEstimate);

            // 2. Advantage: A = R - V(s)
            float advantage = reward - valueEstimate;

            // 3. Loss Calculation
            // Value Loss = 0.5 * (V - R)^2
            float valueLoss = 0.5f * advantage * advantage;
            // Policy Loss = -ln(prob_arch) * A - ln(prob_tact) * A
            float eps = 1e-6f;
            float archProbClamped = Mathf.Clamp(archProbs[chosenArch], eps, 1f);
            float tactProbClamped = Mathf.Clamp(tactProbs[chosenTactic], eps, 1f);
            float policyLoss = -Mathf.Log(archProbClamped) * advantage - Mathf.Log(tactProbClamped) * advantage;

            float totalLoss = policyLoss + 0.5f * valueLoss;
            LastLoss = totalLoss;
            LastReward = reward;
            TotalTrainSteps++;

            // 4. Output Head Gradients
            // dL/dz_arch = (prob - 1) * A for chosen, else (prob) * A
            float[] dZ_arch = new float[ARCH_DIM];
            for (int j = 0; j < ARCH_DIM; j++)
            {
                float target = (j == chosenArch) ? 1.0f : 0.0f;
                dZ_arch[j] = (archProbs[j] - target) * advantage;
            }

            // dL/dz_tact = (prob - 1) * A for chosen, else (prob) * A
            float[] dZ_tact = new float[TACTIC_DIM];
            for (int j = 0; j < TACTIC_DIM; j++)
            {
                float target = (j == chosenTactic) ? 1.0f : 0.0f;
                dZ_tact[j] = (tactProbs[j] - target) * advantage;
            }

            // Value Head Gradient: dL/dz_val = (V - R) * V * (1 - V)
            float dZ_val = (valueEstimate - reward) * valueEstimate * (1.0f - valueEstimate);

            // 5. Backprop through Layer 2 (Hidden 2: 16 units)
            float[] dA2 = new float[HIDDEN2_DIM];
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                float sum = 0f;
                for (int j = 0; j < ARCH_DIM; j++) sum += dZ_arch[j] * W3_arch[i][j];
                for (int j = 0; j < TACTIC_DIM; j++) sum += dZ_tact[j] * W3_tact[i][j];
                sum += dZ_val * W3_val[i];
                dA2[i] = sum;
            }

            float[] dZ2 = new float[HIDDEN2_DIM];
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                dZ2[i] = dA2[i] * LeakyReLUDeriv(LastZ2[i]);
            }

            // 6. Backprop through Layer 1 (Hidden 1: 32 units)
            float[] dA1 = new float[HIDDEN1_DIM];
            for (int i = 0; i < HIDDEN1_DIM; i++)
            {
                float sum = 0f;
                for (int j = 0; j < HIDDEN2_DIM; j++)
                {
                    sum += dZ2[j] * W2[i][j];
                }
                dA1[i] = sum;
            }

            float[] dZ1 = new float[HIDDEN1_DIM];
            for (int i = 0; i < HIDDEN1_DIM; i++)
            {
                dZ1[i] = dA1[i] * LeakyReLUDeriv(LastZ1[i]);
            }

            // 7. Gradient Descent Updates (with Gradient Clipping [-5, 5])
            // Update W3_arch & b3_arch
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                for (int j = 0; j < ARCH_DIM; j++)
                {
                    float grad = Mathf.Clamp(dZ_arch[j] * LastA2[i], -5f, 5f);
                    W3_arch[i][j] -= lr * grad;
                }
            }
            for (int j = 0; j < ARCH_DIM; j++) b3_arch[j] -= lr * Mathf.Clamp(dZ_arch[j], -5f, 5f);

            // Update W3_tact & b3_tact
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                for (int j = 0; j < TACTIC_DIM; j++)
                {
                    float grad = Mathf.Clamp(dZ_tact[j] * LastA2[i], -5f, 5f);
                    W3_tact[i][j] -= lr * grad;
                }
            }
            for (int j = 0; j < TACTIC_DIM; j++) b3_tact[j] -= lr * Mathf.Clamp(dZ_tact[j], -5f, 5f);

            // Update W3_val & b3_val
            for (int i = 0; i < HIDDEN2_DIM; i++)
            {
                float grad = Mathf.Clamp(dZ_val * LastA2[i], -5f, 5f);
                W3_val[i] -= lr * grad;
            }
            b3_val -= lr * Mathf.Clamp(dZ_val, -5f, 5f);

            // Update W2 & b2
            for (int i = 0; i < HIDDEN1_DIM; i++)
            {
                for (int j = 0; j < HIDDEN2_DIM; j++)
                {
                    float grad = Mathf.Clamp(dZ2[j] * LastA1[i], -5f, 5f);
                    W2[i][j] -= lr * grad;
                }
            }
            for (int j = 0; j < HIDDEN2_DIM; j++) b2[j] -= lr * Mathf.Clamp(dZ2[j], -5f, 5f);

            // Update W1 & b1
            for (int i = 0; i < INPUT_DIM; i++)
            {
                for (int j = 0; j < HIDDEN1_DIM; j++)
                {
                    float grad = Mathf.Clamp(dZ1[j] * input[i], -5f, 5f);
                    W1[i][j] -= lr * grad;
                }
            }
            for (int j = 0; j < HIDDEN1_DIM; j++) b1[j] -= lr * Mathf.Clamp(dZ1[j], -5f, 5f);

            return totalLoss;
        }

        public float ComputeWeightNorm()
        {
            float sumSq = 0f;
            for (int i = 0; i < INPUT_DIM; i++)
                for (int j = 0; j < HIDDEN1_DIM; j++) sumSq += W1[i][j] * W1[i][j];

            for (int i = 0; i < HIDDEN1_DIM; i++)
                for (int j = 0; j < HIDDEN2_DIM; j++) sumSq += W2[i][j] * W2[i][j];

            for (int i = 0; i < HIDDEN2_DIM; i++)
                for (int j = 0; j < ARCH_DIM; j++) sumSq += W3_arch[i][j] * W3_arch[i][j];

            return Mathf.Sqrt(sumSq);
        }

        // ==========================================
        // Serialization & JSON Weight Persistence
        // ==========================================

        [System.Serializable]
        private class SerializableWeights
        {
            public float[] flatW1;
            public float[] b1;
            public float[] flatW2;
            public float[] b2;
            public float[] flatW3_arch;
            public float[] b3_arch;
            public float[] flatW3_tact;
            public float[] b3_tact;
            public float[] W3_val;
            public float b3_val;
            public int totalTrainSteps;
            public float weightNorm;
        }

        public void SaveWeights(string filePath)
        {
            try
            {
                SerializableWeights s = new SerializableWeights
                {
                    flatW1 = Flatten(W1),
                    b1 = (float[])b1.Clone(),
                    flatW2 = Flatten(W2),
                    b2 = (float[])b2.Clone(),
                    flatW3_arch = Flatten(W3_arch),
                    b3_arch = (float[])b3_arch.Clone(),
                    flatW3_tact = Flatten(W3_tact),
                    b3_tact = (float[])b3_tact.Clone(),
                    W3_val = (float[])W3_val.Clone(),
                    b3_val = b3_val,
                    totalTrainSteps = TotalTrainSteps,
                    weightNorm = ComputeWeightNorm()
                };

                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(s, true);
                File.WriteAllText(filePath, json);
                Debug.Log($"<color=green>[PrehistoricTacticsNet]</color> Successfully saved weights to: {filePath} (Norm: {s.weightNorm:F3}, Steps: {TotalTrainSteps})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PrehistoricTacticsNet] Failed to save weights: {ex.Message}");
            }
        }

        public bool LoadWeights(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;

                string json = File.ReadAllText(filePath);
                SerializableWeights s = JsonUtility.FromJson<SerializableWeights>(json);
                if (s == null) return false;

                Unflatten(s.flatW1, W1);
                Array.Copy(s.b1, b1, b1.Length);

                Unflatten(s.flatW2, W2);
                Array.Copy(s.b2, b2, b2.Length);

                Unflatten(s.flatW3_arch, W3_arch);
                Array.Copy(s.b3_arch, b3_arch, b3_arch.Length);

                Unflatten(s.flatW3_tact, W3_tact);
                Array.Copy(s.b3_tact, b3_tact, b3_tact.Length);

                Array.Copy(s.W3_val, W3_val, W3_val.Length);
                b3_val = s.b3_val;
                TotalTrainSteps = s.totalTrainSteps;

                Debug.Log($"<color=green>[PrehistoricTacticsNet]</color> Successfully loaded pre-trained weights from: {filePath} (Norm: {s.weightNorm:F3}, Steps: {TotalTrainSteps})");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PrehistoricTacticsNet] Could not load weights: {ex.Message}");
                return false;
            }
        }

        private static float[] Flatten(float[][] matrix)
        {
            int rows = matrix.Length;
            int cols = matrix[0].Length;
            float[] flat = new float[rows * cols];
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    flat[i * cols + j] = matrix[i][j];
            return flat;
        }

        private static void Unflatten(float[] flat, float[][] matrix)
        {
            int rows = matrix.Length;
            int cols = matrix[0].Length;
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    matrix[i][j] = flat[i * cols + j];
        }
    }
}
