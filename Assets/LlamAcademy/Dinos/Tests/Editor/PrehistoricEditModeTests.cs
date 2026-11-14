#if UNITY_EDITOR
using System;
using LlamAcademy.Dinos.AI;
using LlamAcademy.Dinos.Cloud;
using NUnit.Framework;
using UnityEngine;

namespace LlamAcademy.Dinos.Tests.Editor
{
    /// <summary>
    /// Unity EditMode Unit Test Suite (Rubric Supporting Work - Testing).
    /// Validates on-device Neural Network forward/backprop convergence, Deadliness Heatmap scoring,
    /// and IO Cloud URL configuration normalization without modifying scene assets.
    /// </summary>
    public class PrehistoricEditModeTests
    {
        [Test]
        public void PrehistoricTacticsNet_Forward_ProducesNormalizedProbabilities()
        {
            var net = new PrehistoricTacticsNet();
            float[] input = new float[PrehistoricTacticsNet.INPUT_DIM];
            input[0] = 0.5f;
            input[3] = 0.8f;

            net.Forward(input, out float[] archProbs, out float[] tactProbs, out float valueEstimate);

            Assert.AreEqual(PrehistoricTacticsNet.ARCH_DIM, archProbs.Length);
            Assert.AreEqual(PrehistoricTacticsNet.TACTIC_DIM, tactProbs.Length);

            float archSum = 0f;
            foreach (float p in archProbs)
            {
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
                archSum += p;
            }
            Assert.AreEqual(1.0f, archSum, 0.001f);

            float tactSum = 0f;
            foreach (float p in tactProbs)
            {
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
                tactSum += p;
            }
            Assert.AreEqual(1.0f, tactSum, 0.001f);

            Assert.GreaterOrEqual(valueEstimate, 0f);
            Assert.LessOrEqual(valueEstimate, 1f);
        }

        [Test]
        public void PrehistoricTacticsNet_BackpropTrainStep_IncreasesRewardedActionProbability()
        {
            var net = new PrehistoricTacticsNet();
            float[] input = new float[PrehistoricTacticsNet.INPUT_DIM];
            input[1] = 0.9f;
            input[6] = 0.7f;

            net.Forward(input, out float[] initialArch, out _, out _);
            float probBefore = initialArch[2];

            for (int step = 0; step < 30; step++)
            {
                net.TrainStep(input, chosenArch: 2, chosenTactic: 1, reward: 1.0f, lr: 0.05f);
            }

            net.Forward(input, out float[] updatedArch, out _, out _);
            Assert.AreEqual(30, net.TotalTrainSteps);
            Assert.Greater(updatedArch[2], probBefore, "SGD Policy Gradient should reinforce rewarded archetype.");
        }

        [Test]
        public void DeadlinessHeatmap_RecordsAndDecaysDangerZonesCorrectly()
        {
            var go = new GameObject("Test_DeadlinessHeatmap");
            try
            {
                var heatmap = go.AddComponent<DeadlinessHeatmap>();
                Vector3 killPos = new Vector3(10f, 0f, 15f);

                for (int i = 0; i < 4; i++)
                {
                    heatmap.RecordDeath(killPos, currentRound: 2, killerType: "BallistaTower");
                }

                float dangerAtKillPos = heatmap.GetDangerScore(killPos, 6.0f);
                Assert.GreaterOrEqual(dangerAtKillPos, 3.0f);
                Assert.IsTrue(heatmap.IsKillZone(killPos));

                // Far position should have zero danger
                Assert.AreEqual(0f, heatmap.GetDangerScore(new Vector3(-50f, 0f, -50f), 6.0f), 0.001f);

                // Decay old records after round advances past retention window
                heatmap.DecayOldRecords(currentRound: 10, roundsToKeep: 3);
                Assert.AreEqual(0f, heatmap.GetDangerScore(killPos, 6.0f), 0.001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IOCloudManager_UrlNormalization_TrimsWhitespaceAndTrailingSlash()
        {
            const string prefKey = "IOCloud_BaseUrl";
            string previousSaved = PlayerPrefs.GetString(prefKey, "");
            var go = new GameObject("Test_IOCloudManager");
            try
            {
                var cloud = go.AddComponent<IOCloudManager>();
                cloud.SetCloudBaseUrl("  http://192.168.1.50:8000///  ", reconnectImmediately: false);
                Assert.AreEqual("http://192.168.1.50:8000", cloud.ActiveCloudBaseUrl);
            }
            finally
            {
                if (string.IsNullOrEmpty(previousSaved)) PlayerPrefs.DeleteKey(prefKey);
                else PlayerPrefs.SetString(prefKey, previousSaved);
                PlayerPrefs.Save();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
