using System.Collections.Generic;
using System.Linq;
using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEditor;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    [CustomEditor(typeof(MiniGamesPlatformConfig))]
    public class MiniGamesPlatformConfigEditor : UnityEditor.Editor
    {
        private readonly List<MiniGameConfig> _newConfigs = new List<MiniGameConfig>();
        private bool _isScanned;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var config = (MiniGamesPlatformConfig)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan for mini games"))
                TryFindNotConnectedMiniGames(config);

            RenderNewConfigsControls(config);
        }

        private void OnDisable()
        {
            _newConfigs.Clear();
            _isScanned = false;
        }

        private void TryFindNotConnectedMiniGames(MiniGamesPlatformConfig coreConfig)
        {
            _newConfigs.Clear();

            var allConfigs = AssetDatabase.FindAssets($"t:{nameof(MiniGameConfig)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MiniGameConfig>);
            var presentConfigs = coreConfig.MiniGameConfigs
                .Select(c => c.Config);

            _newConfigs.AddRange(allConfigs.Where(c => presentConfigs.Contains(c) == false));
            _isScanned = true;
        }

        private void RenderNewConfigsControls(MiniGamesPlatformConfig coreConfig)
        {
            if (_newConfigs.Count == 0)
            {
                if (_isScanned)
                    GUILayout.Label($"Did not find any new mini games", EditorStyles.boldLabel);
                return;
            }

            GUILayout.Label($"Found {_newConfigs.Count} new mini game{(_newConfigs.Count > 1 ? "s" : "")}",
                EditorStyles.boldLabel);

            if (GUILayout.Button("Add all"))
            {
                coreConfig.MiniGameConfigs.AddRange(_newConfigs.Select(c =>
                    new MiniGameBehaviourConfig(c, MiniGameLoadType.RemoteLoad)));

                EditorUtility.SetDirty(coreConfig);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
    }
}