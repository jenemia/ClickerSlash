using System;
using UnityEditor;
using UnityEngine;

namespace ClikerSlash.Editor
{
    /// <summary>
    /// 전투 시각 자산 논리 키를 현재 에디터 자산 경로로 해석합니다.
    /// </summary>
    public sealed class BattleAssetEditorLocator : ClikerSlash.Battle.IBattleAssetReferenceProvider
    {
        public const string RemoteRootPath = "Assets/Game/RemoteResources";
        public const string PrefabDirectoryPath = RemoteRootPath + "/Prefabs";
        public const string MaterialDirectoryPath = PrefabDirectoryPath + "/Materials";

        public static BattleAssetEditorLocator Instance { get; } = new BattleAssetEditorLocator();

        private BattleAssetEditorLocator()
        {
        }

        /// <summary>
        /// 현재 단계에서는 논리 키 자체를 미래 런타임 참조 식별자로 사용합니다.
        /// </summary>
        public string GetReferenceId(string assetKey)
        {
            return assetKey;
        }

        /// <summary>
        /// 논리 키에 대응하는 현재 에디터 자산 경로를 반환합니다.
        /// </summary>
        public string GetEditorAssetPath(string assetKey)
        {
            return assetKey switch
            {
                ClikerSlash.Battle.BattleAssetKeys.PlayerView => PrefabDirectoryPath + "/PlayerCube.prefab",
                ClikerSlash.Battle.BattleAssetKeys.CargoView => PrefabDirectoryPath + "/CargoCube.prefab",
                ClikerSlash.Battle.BattleAssetKeys.PlayerMaterial => MaterialDirectoryPath + "/PlayerCube.mat",
                ClikerSlash.Battle.BattleAssetKeys.CargoMaterial => MaterialDirectoryPath + "/CargoCube.mat",
                ClikerSlash.Battle.BattleAssetKeys.LaneMaterial => MaterialDirectoryPath + "/LaneStrip.mat",
                ClikerSlash.Battle.BattleAssetKeys.AccentMaterial => MaterialDirectoryPath + "/LineAccent.mat",
                _ => throw new ArgumentOutOfRangeException(nameof(assetKey), assetKey, "등록되지 않은 전투 자산 키입니다.")
            };
        }

        /// <summary>
        /// 현재 경로 기준으로 에디터 자산을 읽습니다.
        /// </summary>
        public T LoadAsset<T>(string assetKey) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(GetEditorAssetPath(assetKey));
        }
    }
}
