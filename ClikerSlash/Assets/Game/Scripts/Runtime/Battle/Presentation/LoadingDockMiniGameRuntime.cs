using System.Collections.Generic;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 상하차 화물의 기본 상호작용 규칙 분류입니다.
    /// </summary>
    public enum LoadingDockCargoInteractionType
    {
        StandardClick = 0,
        FragileDrag = 1,
        HeavyClick = 2
    }

    /// <summary>
    /// 상하차 화물의 현재 처리 상태입니다.
    /// </summary>
    public enum LoadingDockCargoDeliveryState
    {
        Waiting = 0,
        Dragging = 1,
        Delivered = 2
    }

    /// <summary>
    /// 단일 상하차 화물의 진행 상태를 저장합니다.
    /// </summary>
    public sealed class LoadingDockCargoRuntimeState
    {
        public string cargoId;
        public string displayName;
        public LoadingDockCargoInteractionType interactionType;
        public LoadingDockCargoDeliveryState deliveryState;
        public int requiredClicks;
        public int remainingClicks;
        public float dragProgressNormalized;
    }

    /// <summary>
    /// 상하차 미니게임 한 라운드의 전체 상태입니다.
    /// </summary>
    public sealed class LoadingDockMiniGameRuntimeState
    {
        public List<LoadingDockCargoRuntimeState> cargos = new List<LoadingDockCargoRuntimeState>();
        public int deliveredCargoCount;

        public bool IsCompleted => cargos.Count > 0 && deliveredCargoCount >= cargos.Count;
    }

    /// <summary>
    /// 클릭/드래그/연타 규칙만 담당하는 상하차 미니게임 상태 머신입니다.
    /// </summary>
    public static class LoadingDockMiniGameRuntime
    {
        private const float FragileDeliveryThreshold = 0.85f;

        public static LoadingDockMiniGameRuntimeState CreatePrototypeRound()
        {
            return new LoadingDockMiniGameRuntimeState
            {
                cargos = new List<LoadingDockCargoRuntimeState>
                {
                    CreateCargo("dock.standard_box", "표준 박스", LoadingDockCargoInteractionType.StandardClick, 1),
                    CreateCargo("dock.fragile_box", "깨지기 쉬운 박스", LoadingDockCargoInteractionType.FragileDrag, 1),
                    CreateCargo("dock.heavy_box", "무거운 박스", LoadingDockCargoInteractionType.HeavyClick, 3)
                },
                deliveredCargoCount = 0
            };
        }

        public static LoadingDockCargoRuntimeState GetCargo(LoadingDockMiniGameRuntimeState state, string cargoId)
        {
            if (state?.cargos == null || string.IsNullOrWhiteSpace(cargoId))
            {
                return null;
            }

            foreach (var cargo in state.cargos)
            {
                if (cargo != null && cargo.cargoId == cargoId)
                {
                    return cargo;
                }
            }

            return null;
        }

        public static bool RegisterClick(LoadingDockMiniGameRuntimeState state, string cargoId)
        {
            var cargo = GetCargo(state, cargoId);
            if (cargo == null ||
                cargo.deliveryState == LoadingDockCargoDeliveryState.Delivered ||
                cargo.interactionType == LoadingDockCargoInteractionType.FragileDrag)
            {
                return false;
            }

            cargo.remainingClicks = Mathf.Max(0, cargo.remainingClicks - 1);
            if (cargo.remainingClicks == 0)
            {
                MarkDelivered(state, cargo);
            }

            return true;
        }

        public static bool BeginFragileDrag(LoadingDockMiniGameRuntimeState state, string cargoId)
        {
            var cargo = GetCargo(state, cargoId);
            if (cargo == null ||
                cargo.interactionType != LoadingDockCargoInteractionType.FragileDrag ||
                cargo.deliveryState == LoadingDockCargoDeliveryState.Delivered)
            {
                return false;
            }

            cargo.deliveryState = LoadingDockCargoDeliveryState.Dragging;
            cargo.dragProgressNormalized = 0f;
            return true;
        }

        public static bool UpdateFragileDrag(LoadingDockMiniGameRuntimeState state, string cargoId, float progressNormalized)
        {
            var cargo = GetCargo(state, cargoId);
            if (cargo == null || cargo.deliveryState != LoadingDockCargoDeliveryState.Dragging)
            {
                return false;
            }

            cargo.dragProgressNormalized = Mathf.Clamp01(progressNormalized);
            return true;
        }

        public static bool EndFragileDrag(LoadingDockMiniGameRuntimeState state, string cargoId)
        {
            var cargo = GetCargo(state, cargoId);
            if (cargo == null || cargo.deliveryState != LoadingDockCargoDeliveryState.Dragging)
            {
                return false;
            }

            if (cargo.dragProgressNormalized >= FragileDeliveryThreshold)
            {
                MarkDelivered(state, cargo);
                return true;
            }

            cargo.deliveryState = LoadingDockCargoDeliveryState.Waiting;
            cargo.dragProgressNormalized = 0f;
            return false;
        }

        private static LoadingDockCargoRuntimeState CreateCargo(
            string cargoId,
            string displayName,
            LoadingDockCargoInteractionType interactionType,
            int requiredClicks)
        {
            return new LoadingDockCargoRuntimeState
            {
                cargoId = cargoId,
                displayName = displayName,
                interactionType = interactionType,
                deliveryState = LoadingDockCargoDeliveryState.Waiting,
                requiredClicks = requiredClicks,
                remainingClicks = requiredClicks,
                dragProgressNormalized = 0f
            };
        }

        private static void MarkDelivered(LoadingDockMiniGameRuntimeState state, LoadingDockCargoRuntimeState cargo)
        {
            if (cargo.deliveryState == LoadingDockCargoDeliveryState.Delivered)
            {
                return;
            }

            cargo.deliveryState = LoadingDockCargoDeliveryState.Delivered;
            cargo.remainingClicks = 0;
            cargo.dragProgressNormalized = 1f;
            state.deliveredCargoCount += 1;
        }
    }
}
