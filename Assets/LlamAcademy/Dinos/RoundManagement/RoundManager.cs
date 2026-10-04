using System.Collections;
using System.Collections.Generic;
using LlamAcademy.Dinos.Config;
using LlamAcademy.Dinos.Enemy;
using LlamAcademy.Dinos.Player;
using LlamAcademy.Dinos.UI;
using LlamAcademy.Dinos.Unit;
using TMPro;
using UnityEngine;

namespace LlamAcademy.Dinos.RoundManagement
{
    [DefaultExecutionOrder(0)]
    public class RoundManager : MonoBehaviour
    {
        public static RoundManager Instance { get; private set; }

        public int Round { get; private set; }
        [field: SerializeField] public int[] ResourcesPerRound { get; private set; } = new []
        {
            0,
            100,
            200,
            500,
            1000,
            2500,
            5000,
            10_000,
            25_000,
            50_000,
            100_000,
            250_000
        };
        [field: SerializeField] public Transform DinoTarget { get; private set; }
        [SerializeField] private GameState _State;
        public GameState State
        {
            get => _State;
            private set
            {
                OnGameStateChange?.Invoke(_State, value);
                _State = value;
            }
        }

        [SerializeField] private AttackRadius EggRadius;
        public int AliveDinos => ActiveDinos.Count;
        [SerializeField] private ResourceSO DinoSupplyResource;

        public delegate void GameStateChangeEvent(GameState oldState, GameState newState);
        public event GameStateChangeEvent OnGameStateChange;

        private List<Dino> ActiveDinos = new();
        private List<Defender> AliveDefenders = new();
        private bool IsEndingRound;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"Multiple RoundManagers detected. Deleting the second one {name}");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (EggRadius != null) EggRadius.OnTargetEnter += HandleDinoEnterEggRadius;
            if (DinoSupplyResource != null) DinoSupplyResource.Amount = 0;
            if (DinoTarget == null)
            {
                GameObject eggObj = GameObject.Find("Dino Egg Spawn");
                if (eggObj != null) DinoTarget = eggObj.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResetRoundToStart()
        {
            Round = 1;
            State = GameState.Setup;
            IsEndingRound = false;
            ActiveDinos.Clear();
            AliveDefenders.Clear();
        }

        private IEnumerator Start()
        {
            if (DinoSpawner.Instance != null)
            {
                DinoSpawner.Instance.OnSpawnDino += OnSpawnDino;
                DinoSpawner.Instance.OnDinoDeath += OnDinoDeath;
            }
            if (EnemyAIController.Instance != null)
            {
                EnemyAIController.Instance.OnSpawnDefender += OnSpawnDefender;
                EnemyAIController.Instance.OnDefenderDeath += OnDefenderDeath;
                EnemyAIController.Instance.OnSpawnWall += OnSpawnWall;
                EnemyAIController.Instance.OnWallDeath += OnWallDeath;
            }
            Round = 1;
            AddRoundResources();

            yield return null;
            State = GameState.Setup;
        }

        public void StartRound()
        {
            if (PrehistoricGameplayManager.Instance != null && PrehistoricGameplayManager.Instance.CurrentPhase == PrehistoricWavePhase.BuildingPhase)
            {
                PrehistoricGameplayManager.Instance.StartNextWave();
            }

            if (State == GameState.Setup)
            {
                State = GameState.Running;
                if (DinoTarget != null)
                {
                    foreach (Dino dino in ActiveDinos)
                    {
                        dino.SetDestination(DinoTarget.position);
                    }
                }
            }
        }

        public void CompleteEnemySetup()
        {
            if (State == GameState.EnemyRepairs)
            {
                State = GameState.Ended;
                Invoke(nameof(SetSetup), 1.5f);
            }
        }

        private void AddRoundResources()
        {
            if (DinoSpawner.Instance != null && Round - 1 >= 0 && Round - 1 < ResourcesPerRound.Length)
            {
                DinoSpawner.Instance.ResourcesToSpend += ResourcesPerRound[Round - 1];
            }
            if (EnemyAIController.Instance != null && Round - 1 >= 0 && Round - 1 < ResourcesPerRound.Length)
            {
                EnemyAIController.Instance.ResourcesToSpend += Mathf.CeilToInt(ResourcesPerRound[Round - 1] * GetEnemyDifficultyResourceModifier());
            }
        }

        private void HandleDinoEnterEggRadius(IDamageable target)
        {
            if (PrehistoricGameplayManager.Instance != null && target is Unit.Unit u)
            {
                PrehistoricGameplayManager.Instance.OnDinoReachedBase(u, 10);
                return;
            }

            if (IsEndingRound) return;

            IsEndingRound = true;
            State = GameState.Ending;
            CancelInvoke(nameof(EndRound));
            Invoke(nameof(EndRound), 2.5f);
        }

        private void EndRound()
        {
            Round++;
            for (int i = ActiveDinos.Count - 1; i >= 0; i--)
            {
                DinoSpawner.Instance.ResourcesToSpend += ActiveDinos[i].UnitType.Cost; // give back cost of any surviving dinos
                ActiveDinos[i].TakeDamage(int.MaxValue);
                // Destroy(dino.gameObject);
            }
            ActiveDinos.Clear();

            for (int i = AliveDefenders.Count - 1; i >= 0; i--)
            {
                EnemyAIController.Instance.ResourcesToSpend += AliveDefenders[i].UnitType.Cost; // give back cost of any surviving units
                AliveDefenders[i].TakeDamage(int.MaxValue);
                // Destroy(aliveDefender.gameObject);
            }
            AliveDefenders.Clear();

            AddRoundResources();
            IsEndingRound = false;
            State = GameState.Scoring;

            Invoke(nameof(SetEnemyRepairs), 3f);
        }

        private void SetEnemyRepairs()
        {
            State = GameState.EnemyRepairs;
            Invoke(nameof(CompleteEnemySetup), 2f);
        }

        private void SetSetup()
        {
            State = GameState.Setup;
        }

        private void OnDinoDeath(Unit.Unit deadDino)
        {
            if (deadDino is not Dino dino) return;
            ActiveDinos.Remove(dino);
            DinoSupplyResource.Amount = ActiveDinos.Count;

            if (ActiveDinos.Count == 0 && !IsEndingRound)
            {
                IsEndingRound = true;
                State = GameState.Ending;
                CancelInvoke(nameof(EndRound));
                Invoke(nameof(EndRound), 2.5f);
            }
        }

        private void OnSpawnDino(Unit.Unit spawnedDino)
        {
            if (spawnedDino is not Dino dino) return;
            ActiveDinos.Add(dino);
            DinoSupplyResource.Amount = ActiveDinos.Count;
        }

        private void OnDefenderDeath(Defender defender)
        {
            AliveDefenders.Remove(defender);
            if (State == GameState.Running)
            {
                DinoSpawner.Instance.ResourcesToSpend += Mathf.FloorToInt(defender.UnitType.Cost / 2f);
            }
        }

        private void OnSpawnDefender(Defender defender)
        {
            AliveDefenders.Add(defender);
        }

        private void OnSpawnWall(Wall wall)
        {
            // do we care?
        }

        private void OnWallDeath(Wall wall)
        {
            if (State == GameState.Running)
            {
                if (DinoSpawner.Instance != null && wall != null && wall.UnitType != null)
                {
                    DinoSpawner.Instance.ResourcesToSpend += Mathf.FloorToInt(wall.UnitType.Cost / 2f);
                }
            }
        }

        private float GetEnemyDifficultyResourceModifier()
        {
            return EnemyAIController.Instance.Difficulty switch
            {
                Difficulty.Normal => 1,
                Difficulty.Hard => 1.5f,
                Difficulty.Insane => 2f,
                _ => 1
            };
        }
    }
}
