using System.Collections;
using ClikerSlash.Battle;
using NUnit.Framework;
using UnityEngine;
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

        [UnityTest]
        public IEnumerator CompletedRoundCreatesResultSnapshotWhenFlightsFinish()
        {
            var state = LoadingDockMiniGameRuntime.CreatePrototypeRound();
            LoadingDockMiniGameRuntime.RegisterClick(state, "dock.standard_box");
            LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box");
            LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box");
            LoadingDockMiniGameRuntime.RegisterClick(state, "dock.heavy_box");
            LoadingDockMiniGameRuntime.BeginFragileDrag(state, "dock.fragile_box");
            LoadingDockMiniGameRuntime.UpdateFragileDrag(state, "dock.fragile_box", 1f);
            LoadingDockMiniGameRuntime.EndFragileDrag(state, "dock.fragile_box");

            Assert.That(LoadingDockMiniGameRuntime.TryCreateCompletionResult(state, 1, out _), Is.False);
            Assert.That(LoadingDockMiniGameRuntime.TryCreateCompletionResult(state, 0, out var result), Is.True);
            Assert.That(result.DeliveredCargoCount, Is.EqualTo(3));
            Assert.That(result.TotalCargoCount, Is.EqualTo(3));
            Assert.That(result.CompletedSuccessfully, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoViewPoolReusesReleasedViewsByKind()
        {
            var root = new GameObject("CargoViewPoolRoot").transform;
            var pool = new LoadingDockCargoViewPool();

            var firstStandard = pool.Acquire(1, LoadingDockCargoKind.Standard, root, Vector3.zero);
            var firstFragile = pool.Acquire(2, LoadingDockCargoKind.Fragile, root, Vector3.one);

            pool.Release(firstStandard);
            pool.Release(firstFragile);

            var reusedStandard = pool.Acquire(3, LoadingDockCargoKind.Standard, root, Vector3.right);
            var reusedFragile = pool.Acquire(4, LoadingDockCargoKind.Fragile, root, Vector3.left);

            Assert.That(reusedStandard.gameObject, Is.SameAs(firstStandard.gameObject));
            Assert.That(reusedFragile.gameObject, Is.SameAs(firstFragile.gameObject));
            Assert.That(reusedStandard.EntryId, Is.EqualTo(3));
            Assert.That(reusedFragile.EntryId, Is.EqualTo(4));
            Assert.That(reusedStandard.Kind, Is.EqualTo(LoadingDockCargoKind.Standard));
            Assert.That(reusedFragile.Kind, Is.EqualTo(LoadingDockCargoKind.Fragile));
            Assert.That(reusedStandard.gameObject.activeSelf, Is.True);
            Assert.That(reusedFragile.gameObject.activeSelf, Is.True);

            Object.Destroy(root.gameObject);
            yield return null;
        }
    }
}
