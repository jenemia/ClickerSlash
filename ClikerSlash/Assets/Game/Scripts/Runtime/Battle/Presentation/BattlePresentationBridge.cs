using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS의 플레이어와 물류 위치를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
    /// 상하차 진입 연출은 Cinemachine 가상 카메라 우선순위 전환으로 처리합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private CargoVisualPrefabSet cargoVisualPrefabs;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private BattleViewAuthoring battleView;
        [SerializeField] private LoadingDockEnvironmentAuthoring loadingDockEnvironment;
        [SerializeField] private CinemachineCamera laneVirtualCamera;
        [SerializeField] private CinemachineCamera loadingDockVirtualCamera;
        [SerializeField] [Min(0.05f)] private float loadingDockTransitionDuration = PrototypeSessionRuntime.DefaultLoadingDockTransitionDurationSeconds;
        [SerializeField] [Min(0)] private int standbyCameraPriority = 10;
        [SerializeField] [Min(1)] private int liveCameraPriority = 20;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _cargoQuery;
        private GameObject _playerInstance;
        private readonly Dictionary<Entity, GameObject> _cargoInstances = new();
        private bool _cameraRigInitialized;
        private bool _dockCameraIsLive;

        private void Update()
        {
            if (PrototypeSessionRuntime.IsPauseMenuOpen)
            {
                return;
            }

            PrototypeSessionRuntime.AdvanceLoadingDockTransition(Mathf.Max(Time.unscaledDeltaTime, 1f / 60f));
            SyncLoadingDockCamera();

            if (!TryPrepareQueries(out var entityManager))
            {
                CleanupAllViews();
                return;
            }

            SyncPlayer(entityManager);
            SyncCargo(entityManager);
        }

        /// <summary>
        /// 테스트나 씬 빌더가 카메라/뷰 참조를 명시적으로 연결할 때 사용합니다.
        /// </summary>
        public void BindSceneReferences(
            Camera targetCamera,
            BattleViewAuthoring targetBattleView,
            LoadingDockEnvironmentAuthoring targetLoadingDockEnvironment = null,
            CinemachineCamera targetLaneVirtualCamera = null,
            CinemachineCamera targetLoadingDockVirtualCamera = null)
        {
            sceneCamera = targetCamera;
            battleView = targetBattleView;
            loadingDockEnvironment = targetLoadingDockEnvironment;
            laneVirtualCamera = targetLaneVirtualCamera;
            loadingDockVirtualCamera = targetLoadingDockVirtualCamera;
            _cameraRigInitialized = false;
            _dockCameraIsLive = false;
            EnsureCameraRigInitialized();
        }

        /// <summary>
        /// 레인과 상하차가 공유하는 물류 프리팹 세트를 명시적으로 연결합니다.
        /// </summary>
        public void BindVisualPrefabs(GameObject targetPlayerViewPrefab, CargoVisualPrefabSet targetCargoVisualPrefabs)
        {
            playerViewPrefab = targetPlayerViewPrefab;
            cargoVisualPrefabs = targetCargoVisualPrefabs;
        }

        private bool TryPrepareQueries(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            if (_cachedWorld == world)
            {
                return true;
            }

            _cachedWorld = world;
            _playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _cargoQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CargoTag>(),
                ComponentType.ReadOnly<CargoKind>(),
                ComponentType.ReadOnly<LocalTransform>());
            return true;
        }

        private void SyncPlayer(EntityManager entityManager)
        {
            if (playerViewPrefab == null || _playerQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var transforms = _playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (_playerInstance == null)
            {
                _playerInstance = Instantiate(playerViewPrefab, transform);
                _playerInstance.name = "WorkerView";
            }

            _playerInstance.transform.position = transforms[0].Position;
        }

        private void SyncLoadingDockCamera()
        {
            if (!EnsureCameraRigInitialized())
            {
                return;
            }

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var shouldUseDockCamera =
                dockState.TransitionPhase == WorkAreaTransitionPhase.EnteringLoadingDock ||
                dockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;

            if (_dockCameraIsLive == shouldUseDockCamera)
            {
                return;
            }

            _dockCameraIsLive = shouldUseDockCamera;
            laneVirtualCamera.Priority = shouldUseDockCamera ? standbyCameraPriority : liveCameraPriority;
            loadingDockVirtualCamera.Priority = shouldUseDockCamera ? liveCameraPriority : standbyCameraPriority;
        }

        private bool EnsureCameraRigInitialized()
        {
            if (_cameraRigInitialized &&
                sceneCamera != null &&
                laneVirtualCamera != null &&
                loadingDockVirtualCamera != null)
            {
                return true;
            }

            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            laneVirtualCamera ??= FindVirtualCamera("LaneVirtualCamera");
            loadingDockVirtualCamera ??= FindVirtualCamera("LoadingDockVirtualCamera");

            if (sceneCamera == null ||
                laneVirtualCamera == null ||
                loadingDockVirtualCamera == null)
            {
                return false;
            }

            var brain = sceneCamera.GetComponent<CinemachineBrain>() ?? sceneCamera.gameObject.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                Mathf.Max(0.05f, loadingDockTransitionDuration));

            _cameraRigInitialized = true;
            _dockCameraIsLive = false;
            laneVirtualCamera.Priority = liveCameraPriority;
            loadingDockVirtualCamera.Priority = standbyCameraPriority;
            return true;
        }

        private static CinemachineCamera FindVirtualCamera(string cameraName)
        {
            var cameras = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                if (camera != null && string.Equals(camera.name, cameraName, System.StringComparison.Ordinal))
                {
                    return camera;
                }
            }

            return null;
        }

        private void SyncCargo(EntityManager entityManager)
        {
            if (cargoVisualPrefabs == null || !cargoVisualPrefabs.IsComplete)
            {
                return;
            }

            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var kinds = _cargoQuery.ToComponentDataArray<CargoKind>(Allocator.Temp);
            using var transforms = _cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var cargoEntity = entities[i];
                alive.Add(cargoEntity);
                var prefab = cargoVisualPrefabs.Resolve(kinds[i].Value);
                if (prefab == null)
                {
                    continue;
                }

                if (!_cargoInstances.TryGetValue(cargoEntity, out var cargoView) || cargoView == null)
                {
                    cargoView = Instantiate(prefab, transform);
                    cargoView.name = $"CargoView_{cargoEntity.Index}";
                    _cargoInstances[cargoEntity] = cargoView;
                }

                cargoView.transform.position = transforms[i].Position;
            }

            if (_cargoInstances.Count == 0)
            {
                return;
            }

            var staleEntities = ListPool<Entity>.Get();
            foreach (var pair in _cargoInstances)
            {
                if (alive.Contains(pair.Key))
                {
                    continue;
                }

                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }

                staleEntities.Add(pair.Key);
            }

            foreach (var staleEntity in staleEntities)
            {
                _cargoInstances.Remove(staleEntity);
            }
            ListPool<Entity>.Release(staleEntities);
        }

        private void CleanupAllViews()
        {
            if (_playerInstance != null)
            {
                Destroy(_playerInstance);
                _playerInstance = null;
            }

            foreach (var pair in _cargoInstances)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }
            _cargoInstances.Clear();
        }

        private void OnDisable()
        {
            CleanupAllViews();
        }

        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> Pool = new();

            public static List<T> Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new List<T>();
            }

            public static void Release(List<T> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
