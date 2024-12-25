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

        private bool _isListExpanded = true;
        private readonly List<bool> _isItemExpanded = new List<bool>();
        private readonly Dictionary<MiniGameConfig, bool> _isNewItemExpanded = new Dictionary<MiniGameConfig, bool>();

        private bool _isNewConfigsListExpanded = true;

        public override void OnInspectorGUI()
        {
            var platformConfig = (MiniGamesPlatformConfig)target;

            RenderPresentConfigs(platformConfig);

            EditorGUILayout.Space();

            if (GUILayout.Button("Scan for mini games"))
                TryFindNotConnectedMiniGames(platformConfig);

            RenderNewConfigsList(_newConfigs, platformConfig);

            if (GUILayout.Button("Rebuild link.xml"))
                PlatformLinksManager.RebuildConfigs(platformConfig.MiniGameConfigs.ToArray());
        }

        private void OnDisable()
        {
            _newConfigs.Clear();
        }

        private void RenderPresentConfigs(MiniGamesPlatformConfig platformConfig)
        {
            _isListExpanded = EditorGUILayout.Foldout(_isListExpanded, "MiniGame Configs", true);
            if (_isListExpanded)
            {
                while (_isItemExpanded.Count < platformConfig.MiniGameConfigs.Count)
                    _isItemExpanded.Add(true);
                while (_isItemExpanded.Count > platformConfig.MiniGameConfigs.Count)
                    _isItemExpanded.RemoveAt(_isItemExpanded.Count - 1);

                EditorGUILayout.BeginVertical("box");
                for (int i = 0; i < platformConfig.MiniGameConfigs.Count; i++)
                {
                    var behaviourConfig = platformConfig.MiniGameConfigs[i];

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(15);
                    _isItemExpanded[i] =
                        EditorGUILayout.Foldout(_isItemExpanded[i], behaviourConfig.Config.MiniGameName, true);
                    EditorGUILayout.EndHorizontal();

                    if (!_isItemExpanded[i])
                        continue;

                    EditorGUILayout.BeginVertical("box");
                    using (new EditorGUI.DisabledGroupScope(true))
                    {
                        EditorGUILayout.ObjectField("Config", behaviourConfig.Config, typeof(MiniGameConfig),
                            false);

                        EditorGUI.indentLevel++;
                        EditorGUILayout.TextField("Version", behaviourConfig.Config.Version);
                        EditorGUILayout.TextField("URL", behaviourConfig.Config.Url);
                        EditorGUI.indentLevel--;

                        EditorGUILayout.EnumPopup("LoadType", behaviourConfig.LoadType);
                    }

                    if (GUILayout.Button("Remove"))
                    {
                        RemoveConfig(platformConfig, i);
                        break;
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
            }

            if (GUI.changed)
                EditorUtility.SetDirty(platformConfig);
        }

        public void RenderNewConfigsList(List<MiniGameConfig> newConfigs, MiniGamesPlatformConfig platformConfig)
        {
            if (newConfigs == null || newConfigs.Count == 0)
            {
                _isNewConfigsListExpanded = false;
                return;
            }

            _isNewConfigsListExpanded =
                EditorGUILayout.Foldout(_isNewConfigsListExpanded, "New MiniGame Configs", true);
            if (_isNewConfigsListExpanded == false)
                return;

            EditorGUILayout.HelpBox($"Found {newConfigs.Count} new mini game{(newConfigs.Count > 1 ? "s" : "")}.",
                MessageType.Info);

            EditorGUILayout.BeginVertical("box");
            for (var i = 0; i < newConfigs.Count; i++)
            {
                var current = newConfigs[i];

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(15);
                if (_isNewItemExpanded.ContainsKey(current) == false)
                    _isNewItemExpanded.Add(current, false);
                _isNewItemExpanded[current] = EditorGUILayout.Foldout(_isNewItemExpanded[current], current.MiniGameName, true);
                EditorGUILayout.EndHorizontal();

                if (_isNewItemExpanded[current] == false)
                    continue;

                EditorGUILayout.BeginVertical("box");
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.ObjectField("Config", current, typeof(MiniGameConfig), false);

                    EditorGUI.indentLevel++;
                    EditorGUILayout.TextField("MiniGame Name", current.MiniGameName);
                    EditorGUILayout.TextField("Version", current.Version);
                    EditorGUILayout.TextField("URL", current.Url);
                    EditorGUI.indentLevel--;
                }

                if (GUILayout.Button("Add"))
                {
                    newConfigs.RemoveAt(i);
                    _isNewItemExpanded.Remove(current);
                    AddConfigs(new[] { current });

                    return;
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Add all"))
                AddConfigs(newConfigs.ToArray());

            void AddConfigs(MiniGameConfig[] configs)
            {
                var newBehaviourConfigs = configs
                    .Select(c => new MiniGameBehaviourConfig(c, MiniGameLoadType.RemoteLoad))
                    .ToArray();
                platformConfig.MiniGameConfigs.AddRange(newBehaviourConfigs);
                PlatformLinksManager.AddNewConfigs(newBehaviourConfigs);

                EditorUtility.SetDirty(platformConfig);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private void TryFindNotConnectedMiniGames(MiniGamesPlatformConfig coreConfig)
        {
            _newConfigs.Clear();

            var allConfigs = AssetDatabase.FindAssets($"t:{nameof(MiniGameConfig)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<MiniGameConfig>)
                .ToArray();
            var presentConfigs = coreConfig.MiniGameConfigs
                .Select(c => c.Config);

            _newConfigs.AddRange(allConfigs.Where(c => presentConfigs.Contains(c) == false));
            _isNewConfigsListExpanded = true;
        }

        private void RemoveConfig(MiniGamesPlatformConfig platformConfig, int i)
        {
            var config = platformConfig.MiniGameConfigs[i];
            platformConfig.MiniGameConfigs.RemoveAt(i);
            PlatformLinksManager.RemoveConfig(new[] { config });
        }
    }
}