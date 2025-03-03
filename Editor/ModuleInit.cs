using System.Linq;
using UnityEditor;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    [InitializeOnLoad]
    public static class ModuleInit
    {
        [MenuItem("Tools/Mini games platform/Clear builtin cache data")]
        public static void CleanupCache()
        {
            DataLoaderWithCache.CleanupCache();
        }

        [MenuItem("Tools/Mini games platform/Mini games config")]
        public static void CreateOrSelectProjectConfig()
        {
            var data = AssetDatabase.FindAssets($"t:{nameof(MiniGamesPlatformConfig)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MiniGamesPlatformConfig>)
                .ToArray();

            if (data.Length == 0)
            {
                var instance = ScriptableObject.CreateInstance<MiniGamesPlatformConfig>();
                AssetDatabase.CreateAsset(instance, "Assets/MiniGamesPlatformConfig.asset");
                AssetDatabase.SaveAssets();
                Selection.activeObject = instance;
                return;
            }

            if (data.Length > 1)
                Selection.activeObject = data[0];
        }

        static ModuleInit()
        {
            const string define = "DISABLE_MINIGAME_PROJECT_SETUP";
            var currentSymbols =
                PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

            if (currentSymbols.Contains(define) == false)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup,
                    $"{currentSymbols};{define}");
            }
        }
    }
}