using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ClikerSlash.Tests.PlayMode
{
    /// <summary>
    /// 상하차 미니게임 입력 규칙 상태 머신을 검증합니다.
    /// </summary>
    public class LoadingDockMiniGamePlayModeTests
    {
        [UnityTest]
        public IEnumerator StandardCargoDeliversOnSingleClick()
        {
            var state = LoadingDockMiniGameRuntime.CreatePrototypeRound();

            Assert.That(LoadingDockMiniGameRuntime.RegisterClick(state, "dock.standard_box"), Is.True);

            var cargo = LoadingDockMiniGameRuntime.GetCargo(state, "dock.standard_box");
            Assert.That(cargo.deliveryState, Is.EqualTo(LoadingDockCargoDeliveryState.Delivered));
            Assert.That(state.deliveredCargoCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeavyCargoRequiresMultipleClicks()
        {
            var state = LoadingDockMiniGameRuntime.CreatePrototypeRound();

            Assert.That(LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box"), Is.True);
            Assert.That(LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box"), Is.True);
            var cargo = LoadingDockMiniGameRuntime.GetCargo(state, "dock.heavy_box");
            Assert.That(cargo.deliveryState, Is.EqualTo(LoadingDockCargoDeliveryState.Waiting));
            Assert.That(cargo.remainingClicks, Is.EqualTo(1));

            Assert.That(LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box"), Is.True);
            Assert.That(cargo.deliveryState, Is.EqualTo(LoadingDockCargoDeliveryState.Delivered));
            Assert.That(state.deliveredCargoCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FragileCargoRequiresSuccessfulDragThreshold()
        {
            var state = LoadingDockMiniGameRuntime.CreatePrototypeRound();

            Assert.That(LoadingDockMiniGameRuntime.BeginFragileDrag(state, "dock.fragile_box"), Is.True);
            Assert.That(LoadingDockMiniGameRuntime.UpdateFragileDrag(state, "dock.fragile_box", 0.4f), Is.True);
            Assert.That(LoadingDockMiniGameRuntime.EndFragileDrag(state, "dock.fragile_box"), Is.False);

            var cargo = LoadingDockMiniGameRuntime.GetCargo(state, "dock.fragile_box");
            Assert.That(cargo.deliveryState, Is.EqualTo(LoadingDockCargoDeliveryState.Waiting));
            Assert.That(cargo.dragProgressNormalized, Is.Zero);

            Assert.That(LoadingDockMiniGameRuntime.BeginFragileDrag(state, "dock.fragile_box"), Is.True);
            Assert.That(LoadingDockMiniGameRuntime.UpdateFragileDrag(state, "dock.fragile_box", 0.95f), Is.True);
            Assert.That(LoadingDockMiniGameRuntime.EndFragileDrag(state, "dock.fragile_box"), Is.True);
            Assert.That(cargo.deliveryState, Is.EqualTo(LoadingDockCargoDeliveryState.Delivered));
            Assert.That(state.deliveredCargoCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoArcMotionPeaksAboveEndpointsAndEndsAtTarget()
        {
            var start = new UnityEngine.Vector3(0f, 1f, 0f);
            var end = new UnityEngine.Vector3(4f, 1f, 0f);

            var startPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 0f);
            var midPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 0.5f);
            var endPoint = LoadingDockCargoArcMotion.Evaluate(start, end, 2f, 1f);

            Assert.That(startPoint.x, Is.EqualTo(start.x).Within(0.001f));
            Assert.That(startPoint.y, Is.EqualTo(start.y).Within(0.001f));
            Assert.That(startPoint.z, Is.EqualTo(start.z).Within(0.001f));
            Assert.That(endPoint.x, Is.EqualTo(end.x).Within(0.001f));
            Assert.That(endPoint.y, Is.EqualTo(end.y).Within(0.001f));
            Assert.That(endPoint.z, Is.EqualTo(end.z).Within(0.001f));
            Assert.That(midPoint.y, Is.GreaterThan(start.y));
            yield return null;
        }
    }
}
