using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClikerSlash.Battle
{
    /// <summary>
    /// 기본 메타 카탈로그 에셋을 생성하거나 갱신하는 에디터 유틸리티입니다.
    /// </summary>
    public static class MetaProgressionCatalogEditorUtility
    {
        private const string AssetDirectoryPath = "Assets/Game/Resources/MetaProgression";
        private const string AssetPath = AssetDirectoryPath + "/DefaultMetaProgressionCatalog.asset";

        /// <summary>
        /// 메뉴에서 기본 카탈로그 에셋을 생성하거나 물류센터 기본값으로 갱신합니다.
        /// </summary>
        [MenuItem("Tools/ClikerSlash/Meta/Ensure Default Meta Progression Catalog")]
        public static void EnsureDefaultCatalogAsset()
        {
            EnsureFoldersExist();

            var catalog = AssetDatabase.LoadAssetAtPath<MetaProgressionCatalogAsset>(AssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MetaProgressionCatalogAsset>();
                catalog.ResetToLogisticsDefaults();
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }
            else
            {
                catalog.ResetToLogisticsDefaults();
                EditorUtility.SetDirty(catalog);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFoldersExist()
        {
            if (AssetDatabase.IsValidFolder(AssetDirectoryPath))
            {
                return;
            }

            var segments = AssetDirectoryPath.Split('/');
            var currentPath = segments[0];
            for (var index = 1; index < segments.Length; index += 1)
            {
                var nextPath = currentPath + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, segments[index]);
                }

                currentPath = nextPath;
            }

            if (!Directory.Exists(AssetDirectoryPath))
            {
                Directory.CreateDirectory(AssetDirectoryPath);
            }
        }
    }
}
