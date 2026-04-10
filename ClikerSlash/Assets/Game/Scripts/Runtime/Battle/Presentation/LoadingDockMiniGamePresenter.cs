using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 구역 활성화 시 세션 큐의 활성 슬롯 물류를 고정 슬롯 위치에 반영합니다.
    /// </summary>
    public sealed class LoadingDockMiniGamePresenter : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private LoadingDockEnvironmentAuthoring environment;
        [SerializeField] [Min(0.1f)] private float fallbackSlotSpacing = 1.9f;

        private readonly Dictionary<int, GameObject> _cargoViews = new();
        private bool _wasLoadingDockActive;
        private Transform _cargoViewRoot;

        public void BindSceneReferences(Camera targetCamera, LoadingDockEnvironmentAuthoring targetEnvironment)
        {
            sceneCamera = targetCamera;
            environment = targetEnvironment;
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

            var loadingDockState = PrototypeSessionRuntime.GetLoadingDockRuntimeState();
            var isLoadingDockActive = loadingDockState.CurrentArea == WorkAreaType.LoadingDock &&
                                      loadingDockState.TransitionPhase == WorkAreaTransitionPhase.ActiveInLoadingDock;
            if (!isLoadingDockActive)
            {
                _wasLoadingDockActive = false;
                ClearCargoViews();
                return;
            }

            _wasLoadingDockActive = true;
            SyncVisibleCargoViews();
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
                GUILayout.Label($"#{entry.EntryId} {DescribeCargoKind(entry.Kind)}");
            }

            GUILayout.EndArea();
        }

        private void SyncVisibleCargoViews()
        {
            var activeEntries = PrototypeSessionRuntime.GetLoadingDockActiveCargoEntries();
            var staleEntryIds = new List<int>(_cargoViews.Keys);

            for (var slotIndex = 0; slotIndex < activeEntries.Length; slotIndex += 1)
            {
                var entry = activeEntries[slotIndex];
                staleEntryIds.Remove(entry.EntryId);

                if (!_cargoViews.TryGetValue(entry.EntryId, out var cargoView) || cargoView == null)
                {
                    cargoView = CreateCargoView(entry);
                    _cargoViews[entry.EntryId] = cargoView;
                }

                cargoView.transform.position = ResolveSlotPosition(slotIndex);
                cargoView.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                cargoView.name = $"LoadingDockCargo_{entry.EntryId}";

                var renderer = cargoView.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ResolveCargoColor(entry.Kind);
                }
            }

            foreach (var staleEntryId in staleEntryIds)
            {
                if (_cargoViews.TryGetValue(staleEntryId, out var cargoView) && cargoView != null)
                {
                    Destroy(cargoView);
                }

                _cargoViews.Remove(staleEntryId);
            }
        }

        private GameObject CreateCargoView(LoadingDockCargoQueueEntry entry)
        {
            var cargoView = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargoView.transform.SetParent(GetOrCreateCargoViewRoot(), false);
            cargoView.name = $"LoadingDockCargo_{entry.EntryId}";

            var renderer = cargoView.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = ResolveCargoColor(entry.Kind);
            }

            return cargoView;
        }

        private Vector3 ResolveSlotPosition(int slotIndex)
        {
            if (environment.cargoSlotAnchors != null &&
                slotIndex >= 0 &&
                slotIndex < environment.cargoSlotAnchors.Length &&
                environment.cargoSlotAnchors[slotIndex] != null)
            {
                return environment.cargoSlotAnchors[slotIndex].position;
            }

            if (environment.cargoBayRoot == null)
            {
                return transform.position;
            }

            var row = slotIndex / 3;
            var column = slotIndex % 3;
            var xOffset = (column - 1) * fallbackSlotSpacing;
            var zOffset = row * fallbackSlotSpacing;
            return environment.cargoBayRoot.position + new Vector3(xOffset, 1.1f, zOffset);
        }

        private void ClearCargoViews()
        {
            foreach (var cargoView in _cargoViews.Values)
            {
                if (cargoView != null)
                {
                    Destroy(cargoView);
                }
            }

            _cargoViews.Clear();
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

        private static string DescribeCargoKind(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => "깨지기 쉬운 물류",
                LoadingDockCargoKind.Heavy => "무거운 물류",
                _ => "표준 물류"
            };
        }

        private static Color ResolveCargoColor(LoadingDockCargoKind kind)
        {
            return kind switch
            {
                LoadingDockCargoKind.Fragile => new Color(0.4f, 0.85f, 1f),
                LoadingDockCargoKind.Heavy => new Color(0.72f, 0.72f, 0.72f),
                _ => new Color(1f, 0.6f, 0.2f)
            };
        }
    }
}
