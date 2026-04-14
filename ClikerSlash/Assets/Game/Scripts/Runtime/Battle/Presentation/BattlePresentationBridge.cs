using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
        /// <summary>
        /// ECS의 현재 활성 물류를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
        /// 승인 phase는 보조 카메라, 레인선택 phase는 메인 레인 카메라를 사용합니다.
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
        private EntityQuery _laneRobotQuery;
        private GameObject _playerInstance;
        private GameObject _laneRobotInstance;
        private GameObject _dockRobotInstance;
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
            SyncLaneRobot(entityManager);
            SyncCargo(entityManager);
            SyncDockRobot();
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
            _laneRobotQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LaneRobotTag>(),
                ComponentType.ReadOnly<LaneRobotState>(),
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

        private void SyncLaneRobot(EntityManager entityManager)
        {
            if (playerViewPrefab == null || _laneRobotQuery.IsEmptyIgnoreFilter)
            {
                if (_laneRobotInstance != null)
                {
                    _laneRobotInstance.SetActive(false);
                }

                return;
            }

            using var laneRobotStates = _laneRobotQuery.ToComponentDataArray<LaneRobotState>(Allocator.Temp);
            using var transforms = _laneRobotQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (laneRobotStates.Length == 0)
            {
                return;
            }

            if (_laneRobotInstance == null)
            {
                _laneRobotInstance = CreateRobotInstance("LaneRobotView", new Color(0.92f, 0.55f, 0.18f), 0.9f);
            }

            var laneRobotState = laneRobotStates[0];
            var shouldShow = laneRobotState.IsAssigned != 0;
            _laneRobotInstance.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            _laneRobotInstance.transform.position = transforms[0].Position;
        }

        private void SyncLoadingDockCamera()
        {
            if (!EnsureCameraRigInitialized())
            {
                return;
            }

            var shouldUseDockCamera = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase == BattleMiniGamePhase.Approval;

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

        /// <summary>
        /// 현재 작업 영역 상태가 적재구역 시점을 메인 카메라로 써야 하는 상황인지 판정합니다.
        /// </summary>
        internal static bool ShouldUseLoadingDockCamera(LoadingDockRuntimeState dockState)
        {
            return dockState.CurrentArea == WorkAreaType.LoadingDock;
        }

        /// <summary>
        /// 씬 빌더와 프리뷰 프레젠터가 이름으로 가상 카메라를 재연결할 수 있게 도와줍니다.
        /// </summary>
        internal static CinemachineCamera FindVirtualCamera(string cameraName)
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

        private void SyncDockRobot()
        {
            if (playerViewPrefab == null || loadingDockEnvironment == null)
            {
                if (_dockRobotInstance != null)
                {
                    _dockRobotInstance.SetActive(false);
                }

                return;
            }

            var shouldShow = PrototypeSessionRuntime.GetBattleMiniGamePhaseSnapshot().CurrentPhase == BattleMiniGamePhase.Approval;

            if (_dockRobotInstance == null)
            {
                _dockRobotInstance = CreateRobotInstance("DockRobotView", new Color(0.28f, 0.86f, 0.64f), 0.82f);
            }

            _dockRobotInstance.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            var anchor = loadingDockEnvironment.dockRobotAnchor != null
                ? loadingDockEnvironment.dockRobotAnchor
                : loadingDockEnvironment.truckDropZone;
            if (anchor != null)
            {
                _dockRobotInstance.transform.position = anchor.position;
            }
        }

        private GameObject CreateRobotInstance(string name, Color color, float scale)
        {
            var robotInstance = Instantiate(playerViewPrefab, transform);
            robotInstance.name = name;
            robotInstance.transform.localScale = Vector3.one * scale;

            foreach (var renderer in robotInstance.GetComponentsInChildren<Renderer>())
            {
                renderer.material.color = color;
            }

            return robotInstance;
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

            if (_laneRobotInstance != null)
            {
                Destroy(_laneRobotInstance);
                _laneRobotInstance = null;
            }

            if (_dockRobotInstance != null)
            {
                Destroy(_dockRobotInstance);
                _dockRobotInstance = null;
            }
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
