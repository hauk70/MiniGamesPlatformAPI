using System;
using System.Linq;
using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public class BuiltinBundlesPreBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("Pre-build: Checking builtin bundles...");

            var configGuid = AssetDatabase.FindAssets($"t:{nameof(MiniGamesPlatformConfig)}").FirstOrDefault();
            if (configGuid == null)
                throw new ArgumentException($"Could not find config file for {nameof(MiniGamesPlatformConfig)}");

            var config =
                AssetDatabase.LoadAssetAtPath<MiniGamesPlatformConfig>(AssetDatabase.GUIDToAssetPath(configGuid));

            var platform = MiniGamePlatformUtils.GetPlatformTargetByBuildTarget(EditorUserBuildSettings
                .activeBuildTarget);

            var resolver = new LoadTypeResolver(
                config.MiniGameConfigs.ToDictionary(c => c.Config.GetFullUrl(platform),
                    c => c.Config.SharedCatalogs.Select(catalog => catalog.GetFullUrl(platform)).ToList()));

            resolver.ResolveLoadTypes(
                config.MiniGameConfigs.ToDictionary(c => c.Config.GetFullUrl(platform), c => c.LoadType));

            BuiltInBundlesUtils.EnsureOnlyNecessaryCatalogsAreStored(resolver.ResolvedLoadTypes.Keys.ToList())
                .Wait();
        }
    }
}