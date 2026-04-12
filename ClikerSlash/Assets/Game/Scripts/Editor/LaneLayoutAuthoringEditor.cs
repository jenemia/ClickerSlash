using System.Collections.Generic;
using System.Linq;
using ClikerSlash.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClikerSlash.Editor
{
    /// <summary>
    /// 레인 월드 X 목록을 씬 비주얼 배치와 동기화하는 인스펙터 버튼을 제공합니다.
    /// </summary>
    [CustomEditor(typeof(LaneLayoutAuthoring))]
    public sealed class LaneLayoutAuthoringEditor : UnityEditor.Editor
    {
        private const string WorkerSpawnObjectName = "WorkerSpawn";
        private const string CargoSpawnLineObjectName = "CargoSpawnLine";
        private const string JudgmentLineObjectName = "JudgmentLine";
        private const string FailLineObjectName = "FailLine";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "LaneWorldXs 값을 수정한 뒤 버튼을 누르면 현재 씬의 레인 비주얼, WorkerSpawn, 기준 라인 X를 즉시 다시 맞춥니다.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Sync Scene Layout From LaneWorldXs"))
                {
                    SyncSceneLayout((LaneLayoutAuthoring)target);
                }
            }
        }

        private static void SyncSceneLayout(LaneLayoutAuthoring authoring)
        {
            if (authoring == null)
            {
                return;
            }

            var laneWorldXs = authoring.LaneWorldXs;
            if (laneWorldXs == null || laneWorldXs.Count == 0)
            {
                Debug.LogWarning("LaneLayoutAuthoring sync aborted: LaneWorldXs is empty.", authoring);
                return;
            }

            var laneRoot = authoring.transform;
            var laneTransforms = laneRoot.Cast<Transform>()
                .Where(child => child.name.StartsWith("Lane_", System.StringComparison.Ordinal))
                .OrderBy(ResolveLaneIndex)
                .ToList();

            if (laneTransforms.Count != laneWorldXs.Count)
            {
                Debug.LogWarning(
                    $"LaneLayoutAuthoring sync aborted: found {laneTransforms.Count} lane visuals under '{laneRoot.name}' " +
                    $"but LaneWorldXs has {laneWorldXs.Count} entries.",
                    authoring);
                return;
            }

            var undoTargets = new List<Object> { laneRoot };
            undoTargets.AddRange(laneTransforms.Select(lane => lane));

            var playerAuthoring = FindComponentInScene<PlayerAuthoring>(authoring.gameObject.scene);
            if (playerAuthoring != null)
            {
                undoTargets.Add(playerAuthoring.transform);
            }

            AddNamedTransformIfPresent(authoring.gameObject.scene, CargoSpawnLineObjectName, undoTargets);
            AddNamedTransformIfPresent(authoring.gameObject.scene, JudgmentLineObjectName, undoTargets);
            AddNamedTransformIfPresent(authoring.gameObject.scene, FailLineObjectName, undoTargets);

            Undo.RecordObjects(undoTargets.Distinct().ToArray(), "Sync Lane Layout From LaneWorldXs");

            for (var index = 0; index < laneTransforms.Count; index += 1)
            {
                var laneTransform = laneTransforms[index];
                var currentPosition = laneTransform.position;
                laneTransform.position = new Vector3(laneWorldXs[index], currentPosition.y, currentPosition.z);
                EditorUtility.SetDirty(laneTransform);
            }

            SyncWorkerSpawn(authoring.gameObject.scene, laneWorldXs, playerAuthoring, authoring);
            SyncLineCenter(authoring.gameObject.scene, CargoSpawnLineObjectName, laneWorldXs, authoring);
            SyncLineCenter(authoring.gameObject.scene, JudgmentLineObjectName, laneWorldXs, authoring);
            SyncLineCenter(authoring.gameObject.scene, FailLineObjectName, laneWorldXs, authoring);

            EditorSceneManager.MarkSceneDirty(authoring.gameObject.scene);
        }

        private static void SyncWorkerSpawn(
            UnityEngine.SceneManagement.Scene scene,
            IReadOnlyList<float> laneWorldXs,
            PlayerAuthoring playerAuthoring,
            LaneLayoutAuthoring authoring)
        {
            if (playerAuthoring == null)
            {
                Debug.LogWarning($"LaneLayoutAuthoring sync could not find '{WorkerSpawnObjectName}' in the current scene.", authoring);
                return;
            }

            if (playerAuthoring.InitialLane < 0 || playerAuthoring.InitialLane >= laneWorldXs.Count)
            {
                Debug.LogWarning(
                    $"LaneLayoutAuthoring sync skipped '{WorkerSpawnObjectName}': InitialLane {playerAuthoring.InitialLane} is outside the LaneWorldXs range.",
                    authoring);
                return;
            }

            var playerTransform = playerAuthoring.transform;
            var currentPosition = playerTransform.position;
            playerTransform.position = new Vector3(laneWorldXs[playerAuthoring.InitialLane], currentPosition.y, currentPosition.z);
            EditorUtility.SetDirty(playerTransform);
        }

        private static void SyncLineCenter(
            UnityEngine.SceneManagement.Scene scene,
            string objectName,
            IReadOnlyList<float> laneWorldXs,
            LaneLayoutAuthoring authoring)
        {
            var lineTransform = FindNamedTransformInScene(scene, objectName);
            if (lineTransform == null)
            {
                Debug.LogWarning($"LaneLayoutAuthoring sync could not find '{objectName}' in the current scene.", authoring);
                return;
            }

            var currentPosition = lineTransform.position;
            lineTransform.position = new Vector3(laneWorldXs.Average(), currentPosition.y, currentPosition.z);
            EditorUtility.SetDirty(lineTransform);
        }

        private static void AddNamedTransformIfPresent(
            UnityEngine.SceneManagement.Scene scene,
            string objectName,
            ICollection<Object> targets)
        {
            var transform = FindNamedTransformInScene(scene, objectName);
            if (transform != null)
            {
                targets.Add(transform);
            }
        }

        private static int ResolveLaneIndex(Transform laneTransform)
        {
            var name = laneTransform.name;
            var underscoreIndex = name.LastIndexOf('_');
            if (underscoreIndex < 0 || underscoreIndex >= name.Length - 1)
            {
                return int.MaxValue;
            }

            return int.TryParse(name[(underscoreIndex + 1)..], out var parsedIndex)
                ? parsedIndex
                : int.MaxValue;
        }

        private static T FindComponentInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var component = rootObject.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static Transform FindNamedTransformInScene(UnityEngine.SceneManagement.Scene scene, string objectName)
        {
            foreach (var rootObject in scene.GetRootGameObjects())
            {
                var match = FindNamedTransformRecursive(rootObject.transform, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static Transform FindNamedTransformRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
            {
                return root;
            }

            for (var childIndex = 0; childIndex < root.childCount; childIndex += 1)
            {
                var child = root.GetChild(childIndex);
                var match = FindNamedTransformRecursive(child, objectName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
