using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// ECS의 플레이어와 물류 위치를 프로토타입 씬의 단순 게임 오브젝트 뷰에 반영합니다.
    /// </summary>
    public sealed class BattlePresentationBridge : MonoBehaviour
    {
        [SerializeField] private GameObject playerViewPrefab;
        [SerializeField] private GameObject cargoViewPrefab;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private BattleViewAuthoring battleView;
        [SerializeField] [Min(0.05f)] private float loadingDockTransitionDuration = 0.35f;
        [SerializeField] private float loadingDockYawOffset = 90f;

        private World _cachedWorld;
        private EntityQuery _playerQuery;
        private EntityQuery _cargoQuery;
        private GameObject _playerInstance;
        private readonly Dictionary<Entity, GameObject> _cargoInstances = new();
        private bool _cameraPoseInitialized;
        private WorkAreaTransitionPhase _animatedPhase;
        private float _transitionElapsed;
        private Vector3 _cameraTransitionStartPosition;
        private Quaternion _cameraTransitionStartRotation;
        private Vector3 _laneCameraPosition;
        private Quaternion _laneCameraRotation;
        private Vector3 _loadingDockCameraPosition;
        private Quaternion _loadingDockCameraRotation;

        private void Update()
        {
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
        public void BindSceneReferences(Camera targetCamera, BattleViewAuthoring targetBattleView)
        {
            sceneCamera = targetCamera;
            battleView = targetBattleView;
            _cameraPoseInitialized = false;
            EnsureCameraPoseInitialized();
            ApplyCameraPose(_laneCameraPosition, _laneCameraRotation);
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
            if (!EnsureCameraPoseInitialized())
            {
                return;
            }

            var dockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            switch (dockState.TransitionPhase)
            {
                case WorkAreaTransitionPhase.EnteringLoadingDock:
                    AnimateCameraTransition(
                        WorkAreaTransitionPhase.EnteringLoadingDock,
                        _loadingDockCameraPosition,
                        _loadingDockCameraRotation,
                        PrototypeSessionRuntime.ConsumeLoadingDockEntryRequest);
                    break;

                case WorkAreaTransitionPhase.ReturningToLane:
                    AnimateCameraTransition(
                        WorkAreaTransitionPhase.ReturningToLane,
                        _laneCameraPosition,
                        _laneCameraRotation,
                        PrototypeSessionRuntime.ConsumeLoadingDockReturnRequest);
                    break;

                case WorkAreaTransitionPhase.ActiveInLoadingDock:
                    ApplyCameraPose(_loadingDockCameraPosition, _loadingDockCameraRotation);
                    _animatedPhase = WorkAreaTransitionPhase.ActiveInLoadingDock;
                    break;

                default:
                    ApplyCameraPose(_laneCameraPosition, _laneCameraRotation);
                    _animatedPhase = WorkAreaTransitionPhase.None;
                    break;
            }
        }

        private bool EnsureCameraPoseInitialized()
        {
            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            battleView ??= FindFirstObjectByType<BattleViewAuthoring>();
            if (sceneCamera == null)
            {
                return false;
            }

            if (_cameraPoseInitialized)
            {
                return true;
            }

            _laneCameraPosition = battleView != null ? battleView.CameraPosition : sceneCamera.transform.position;
            _laneCameraRotation = battleView != null
                ? Quaternion.Euler(battleView.CameraRotation)
                : sceneCamera.transform.rotation;
            _loadingDockCameraPosition = _laneCameraPosition;
            _loadingDockCameraRotation = _laneCameraRotation * Quaternion.Euler(0f, loadingDockYawOffset, 0f);
            _animatedPhase = WorkAreaTransitionPhase.None;
            _transitionElapsed = 0f;
            _cameraPoseInitialized = true;
            return true;
        }

        private void AnimateCameraTransition(
            WorkAreaTransitionPhase targetPhase,
            Vector3 targetPosition,
            Quaternion targetRotation,
            System.Action onTransitionFinished)
        {
            if (_animatedPhase != targetPhase)
            {
                _animatedPhase = targetPhase;
                _transitionElapsed = 0f;
                _cameraTransitionStartPosition = sceneCamera.transform.position;
                _cameraTransitionStartRotation = sceneCamera.transform.rotation;
            }

            _transitionElapsed += Mathf.Max(Time.deltaTime, 1f / 60f);
            var duration = Mathf.Max(0.05f, loadingDockTransitionDuration);
            var normalizedTime = Mathf.Clamp01(_transitionElapsed / duration);
            var easedTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            ApplyCameraPose(
                Vector3.Lerp(_cameraTransitionStartPosition, targetPosition, easedTime),
                Quaternion.Slerp(_cameraTransitionStartRotation, targetRotation, easedTime));

            if (normalizedTime < 1f)
            {
                return;
            }

            onTransitionFinished?.Invoke();
        }

        private void ApplyCameraPose(Vector3 position, Quaternion rotation)
        {
            if (sceneCamera == null)
            {
                return;
            }

            sceneCamera.transform.SetPositionAndRotation(position, rotation);
        }

        private void SyncCargo(EntityManager entityManager)
        {
            if (cargoViewPrefab == null)
            {
                return;
            }

            using var entities = _cargoQuery.ToEntityArray(Allocator.Temp);
            using var transforms = _cargoQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var alive = new HashSet<Entity>();

            for (var i = 0; i < entities.Length; i++)
            {
                var cargoEntity = entities[i];
                alive.Add(cargoEntity);

                if (!_cargoInstances.TryGetValue(cargoEntity, out var cargoView) || cargoView == null)
                {
                    cargoView = Instantiate(cargoViewPrefab, transform);
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
