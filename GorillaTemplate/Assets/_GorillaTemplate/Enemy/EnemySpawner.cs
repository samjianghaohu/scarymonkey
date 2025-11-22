using Normal.Realtime;
using Normal.Utility;
using UnityEngine;
using static Normal.Realtime.Realtime;

namespace ScaryMonkey.Enemy
{
    [RequireComponent(typeof(AutoDistributeViewOwnership))]
    public class EnemySpawner : RealtimeSingleton<EnemySpawner, EnemySpawnerModel>
    {
        public enum SpawnState : ushort
        {
            NotSpawned = 0,
            SpawnFinished = 1
        }

        [SerializeField]
        private GameObject enemyPrefab;

        [SerializeField]
        private Transform[] spawnPoints;

        private AutoDistributeViewOwnership _autoDistributeOwnership;
        private bool IsOwnerConfirmed => _autoDistributeOwnership != null && _autoDistributeOwnership.isLocallyOwnedConfirmed;

        private SpawnState CurrentState
        {
            get => model != null ? (SpawnState)model.currentState : SpawnState.SpawnFinished; // Use spawn finished as fallback so it doesn't spawn unnecessary enemies
            set
            {
                if (model == null)
                {
                    Debug.LogWarning($"{nameof(CurrentState)} setter: No model assigned yet, skipping");
                    return;
                }

                if (!IsOwnerConfirmed)
                {
                    Debug.LogWarning($"{nameof(CurrentState)} setter: Only the confirmed owner can set the value, skipping");
                    return;
                }

                if (model.currentState != (ushort)value)
                {
                    model.currentState = (ushort)value;
                }
            }
        }

        protected override void OnRealtimeModelReplaced(EnemySpawnerModel previousModel, EnemySpawnerModel currentModel)
        {
            base.OnRealtimeModelReplaced(previousModel, currentModel);

            // Are we creating this model?
            if (currentModel != null && currentModel.isFreshModel)
            {
                currentModel.currentState = (ushort)SpawnState.NotSpawned;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (!TryGetComponent<AutoDistributeViewOwnership>(out _autoDistributeOwnership))
            {
                Debug.LogError($"Enemy Spawner {gameObject.name} is missing ownership distrubition component");
            }
        }

        private void Update()
        {
            if (!realtime.connected || model == null)
            {
                return;
            }

            // Only proceed if we're the owner
            if (!IsOwnerConfirmed)
            {
                return;
            }

            if (CurrentState == SpawnState.NotSpawned)
            {
                SpawnEnemiesAtLocations();
            }
        }

        private void SpawnEnemiesAtLocations()
        {
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"No enemy prefab assigned on enemy spawner {gameObject.name}. Nothing will spawn.");
                CurrentState = SpawnState.SpawnFinished;
                return;
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning($"No spawn points specified on enemy spawner {gameObject.name}. Nothing will spawn.");
                CurrentState = SpawnState.SpawnFinished;
                return;
            }

            InstantiateOptions options = new InstantiateOptions()
            {
                ownedByClient = true,
                preventOwnershipTakeover = false,
                destroyWhenLastClientLeaves = true,
                destroyWhenOwnerLeaves = false
            };

            foreach (Transform spawnPoint in spawnPoints)
            {
                GameObject newEnemy = Realtime.Instantiate(enemyPrefab.name, options);
                newEnemy.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            }

            CurrentState = SpawnState.SpawnFinished;
        }
    }

    [RealtimeModel(true)]
    public partial class EnemySpawnerModel
    {
        [RealtimeProperty(1, true, true)]
        private ushort _currentState;
    }
}
