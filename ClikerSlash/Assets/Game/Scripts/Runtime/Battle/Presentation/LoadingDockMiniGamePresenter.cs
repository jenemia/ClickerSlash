using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 구역 활성화 시 세션 큐의 활성 슬롯 물류를 고정 슬롯 위치에 반영합니다.
    /// </summary>
    public sealed class LoadingDockMiniGamePresenter : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private LoadingDockEnvironmentAuthoring environment;
        [SerializeField] private CargoVisualPrefabSet cargoVisualPrefabs;
        [SerializeField] [Min(0.1f)] private float fallbackSlotSpacing = 1.9f;

        private readonly Dictionary<int, LoadingDockCargoView> _cargoViews = new();
        private readonly Dictionary<int, int> _slotByEntryId = new();
        private readonly LoadingDockCargoViewPool _cargoViewPool = new();
        private bool _wasLoadingDockActive;
        private Transform _cargoViewRoot;
        private double _nextDockRobotHandleTime;

        public void BindSceneReferences(
            Camera targetCamera,
            LoadingDockEnvironmentAuthoring targetEnvironment,
            CargoVisualPrefabSet targetCargoVisualPrefabs = null)
        {
            sceneCamera = targetCamera;
            environment = targetEnvironment;
            if (targetCargoVisualPrefabs != null)
            {
                cargoVisualPrefabs = targetCargoVisualPrefabs;
            }

            _cargoViewPool.Configure(cargoVisualPrefabs);
        }

        public void BindCargoVisualPrefabs(CargoVisualPrefabSet targetCargoVisualPrefabs)
        {
            cargoVisualPrefabs = targetCargoVisualPrefabs;
            _cargoViewPool.Configure(cargoVisualPrefabs);
        }

        private void Update()
        {
            sceneCamera ??= Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            environment ??= FindFirstObjectByType<LoadingDockEnvironmentAuthoring>();
            if (sceneCamera == null || environment == null)
            {
                ClearCargoViews();
                return;
            }

            _cargoViewPool.Configure(cargoVisualPrefabs);

            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var isLoadingDockActive = loadingDockState.CurrentArea == WorkAreaType.LoadingDock &&
                                      loadingDockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;
            SyncVisibleCargoViews();
            _wasLoadingDockActive = isLoadingDockActive;
            if (isLoadingDockActive)
            {
                TryHandleDockRobotCargo();
                HandlePointerInput();
                return;
            }

            _nextDockRobotHandleTime = 0d;
        }

        private void OnGUI()
        {
            if (!_wasLoadingDockActive)
            {
                return;
            }

            var queueSnapshot = PrototypeSessionRuntime.GetLoadingDockQueueSnapshot();
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();

            GUILayout.BeginArea(new Rect(Screen.width - 380f, 24f, 340f, 220f), GUI.skin.box);
            GUILayout.Label("Loading Dock");
            GUILayout.Label($"Visible {queueSnapshot.ActiveSlotCount}/{queueSnapshot.MaxActiveSlotCount}");
            GUILayout.Label($"Backlog {queueSnapshot.BacklogCount}");

            if (activeEntries.Length == 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("적재 대기 물류 없음");
                GUILayout.EndArea();
                return;
            }

            foreach (var entry in activeEntries)
            {
                GUILayout.Label($"#{entry.EntryId} {DescribeCargoKind(entry.Kind)} / {entry.Weight}kg");
            }

            GUILayout.EndArea();
        }

        private void SyncVisibleCargoViews()
        {
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            var staleEntryIds = new List<int>(_cargoViews.Keys);

            for (var activeEntryIndex = 0; activeEntryIndex < activeEntries.Length; activeEntryIndex += 1)
            {
                var entry = activeEntries[activeEntryIndex];
                staleEntryIds.Remove(entry.EntryId);

                if (!_cargoViews.TryGetValue(entry.EntryId, out var cargoView) || cargoView == null)
                {
                    cargoView = _cargoViewPool.Acquire(
                        entry.EntryId,
                        entry.Kind,
                        GetOrCreateCargoViewRoot(),
                        ResolveSlotPosition(entry.SlotIndex, entry.Kind));
                    if (cargoView == null)
                    {
                        continue;
                    }

                    _cargoViews[entry.EntryId] = cargoView;
                }

                var slotChanged = !_slotByEntryId.TryGetValue(entry.EntryId, out var previousSlotIndex) ||
                                  previousSlotIndex != entry.SlotIndex;
                _slotByEntryId[entry.EntryId] = entry.SlotIndex;
                cargoView.Bind(entry.EntryId, entry.Kind);
                if (slotChanged)
                {
                    // 활성 슬롯 물류는 대기 중 정지 상태이므로 슬롯이 바뀔 때만 목표 위치를 다시 맞춥니다.
                    cargoView.transform.position = ResolveSlotPosition(entry.SlotIndex, entry.Kind);
                }

                cargoView.gameObject.name = $"LoadingDockCargo_{entry.EntryId}";
            }

            foreach (var staleEntryId in staleEntryIds)
            {
                if (_cargoViews.TryGetValue(staleEntryId, out var cargoView) && cargoView != null)
                {
                    _cargoViewPool.Release(cargoView);
                }

                _cargoViews.Remove(staleEntryId);
                _slotByEntryId.Remove(staleEntryId);
            }
        }

        public bool TryDeliverCargoEntry(int entryId)
        {
            return PrototypeSessionRuntime.TryDeliverLoadingDockCargo(entryId, out _);
        }

        private void TryHandleDockRobotCargo()
        {
            var resolvedProgression = PrototypeSessionRuntime.GetResolvedMetaProgression();
            if (!resolvedProgression.HasDockRobotAccess || !PrototypeSessionRuntime.HasInstalledDockRobot())
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var entityManager = world.EntityManager;
            using var battleConfigQuery = entityManager.CreateEntityQuery(typeof(BattleConfig));
            if (battleConfigQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var now = Time.unscaledTimeAsDouble;
            if (_nextDockRobotHandleTime > now)
            {
                return;
            }

            var battleConfig = battleConfigQuery.GetSingleton<BattleConfig>();
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            foreach (var entry in activeEntries)
            {
                if (!RobotHandlingRules.CanHandle(
                        resolvedProgression.RobotMaxHandleWeight,
                        resolvedProgression.RobotPrecisionTier,
                        entry.Kind,
                        entry.Weight))
                {
                    continue;
                }

                if (TryDeliverCargoEntry(entry.EntryId))
                {
                    _nextDockRobotHandleTime = now + battleConfig.HandleDurationSeconds;
                }

                return;
            }
        }

        private void HandlePointerInput()
        {
            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            if (PrototypeSessionRuntime.IsPauseMenuOpen ||
                loadingDockState.CurrentArea != WorkAreaType.LoadingDock ||
                loadingDockState.TransitionPhase != WorkAreaTransitionPhase.ActiveInLoadingDock)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            if (!TryRaycastCargoEntry(out var entryId))
            {
                return;
            }

            TryDeliverCargoEntry(entryId);
        }

        /// <summary>
        /// 슬롯 앵커와 종류별 보정값을 합쳐 벨트 위 최종 물류 배치 위치를 계산합니다.
        /// </summary>
        private Vector3 ResolveSlotPosition(int slotIndex, LoadingDockCargoKind kind)
        {
            var cargoOffset = environment != null ? environment.GetCargoOffset(kind) : Vector3.zero;
            if (environment.cargoSlotAnchors != null &&
                slotIndex >= 0 &&
                slotIndex < environment.cargoSlotAnchors.Length &&
                environment.cargoSlotAnchors[slotIndex] != null)
            {
                return environment.cargoSlotAnchors[slotIndex].position + cargoOffset;
            }

            if (environment.cargoBayRoot == null)
            {
                return transform.position + cargoOffset;
            }

            var row = slotIndex / 3;
            var column = slotIndex % 3;
            var xOffset = (column - 1) * fallbackSlotSpacing;
            var zOffset = row * fallbackSlotSpacing;
            return environment.cargoBayRoot.position + new Vector3(xOffset, 1.1f, zOffset) + cargoOffset;
        }

        private void ClearCargoViews()
        {
            foreach (var cargoView in _cargoViews.Values)
            {
                if (cargoView != null)
                {
                    _cargoViewPool.Release(cargoView);
                }
            }

            _cargoViews.Clear();
            _slotByEntryId.Clear();
        }

        private Transform GetOrCreateCargoViewRoot()
        {
            if (_cargoViewRoot != null)
            {
                return _cargoViewRoot;
            }

            var rootObject = new GameObject("LoadingDockCargoViewRoot");
            rootObject.transform.SetParent(transform, false);
            _cargoViewRoot = rootObject.transform;
            return _cargoViewRoot;
        }

        private bool TryRaycastCargoEntry(out int entryId)
        {
            entryId = default;
            if (sceneCamera == null || Mouse.current == null)
            {
                return false;
            }

            var ray = sceneCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, 200f))
            {
                return false;
            }

            var cargoView = hit.collider.GetComponent<LoadingDockCargoView>();
            if (cargoView == null)
            {
                return false;
            }

            entryId = cargoView.EntryId;
            return true;
        }

        private static string DescribeCargoKind(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => "깨지기 쉬운 물류",
                LoadingDockCargoKind.Frozen => "냉동 물류",
                _ => "일반 물류"
            };
        }
    }
}
