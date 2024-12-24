using System;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public static class PlatformLinksManager
    {
        private const string LinkerFolderPath = "Assets";
        private const string LinkerFileName = "MiniGamesPlatform.link.xml";
        private static string FullLinkerPath => Path.Combine(LinkerFolderPath, LinkerFileName);

        public static void RemoveConfig(MiniGamesPlatformConfig platformConfig, MiniGameBehaviourConfig[] configs)
        {
            UpdateLinkXml(platformConfig);
        }

        public static void AddNewConfigs(MiniGamesPlatformConfig platformConfig,
            MiniGameBehaviourConfig[] newBehaviourConfigs)
        {
            UpdateLinkXml(platformConfig);
        }

        private static void UpdateLinkXml(MiniGamesPlatformConfig platformConfig)
        {
            var builder = new StringBuilder();
            builder.AppendLine("<linker>");
            foreach (var assemblyName in platformConfig.MiniGameConfigs
                         .Select(GetAssemblyName)
                         .Where(name => name != null)
                         .Distinct())
            {
                builder.AppendLine($"  <assembly fullname=\"{assemblyName}\" preserve=\"all\" />");
            }

            builder.AppendLine("</linker>");

            if (File.Exists(FullLinkerPath) && File.ReadAllText(FullLinkerPath) == builder.ToString())
                return;

            File.WriteAllText(FullLinkerPath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static string GetAssemblyName(MiniGameBehaviourConfig config)
        {
            var configAssetPath = AssetDatabase.GetAssetPath(config?.Config);
            if (string.IsNullOrEmpty(configAssetPath))
                return null;

            var packagePath = Path.GetDirectoryName(configAssetPath);
            var asmdefFiles = Directory.GetFiles(packagePath, "*.asmdef", SearchOption.AllDirectories);

            foreach (var asmdefFile in asmdefFiles)
            {
                var asmdefContent = File.ReadAllText(asmdefFile);
                var assemblyName = ExtractAssemblyNameFromAsmdef(asmdefContent);
                if (!string.IsNullOrEmpty(assemblyName))
                    return assemblyName;
            }

            return null;
        }

        private static string ExtractAssemblyNameFromAsmdef(string asmdefContent)
        {
            try
            {
                var json = JObject.Parse(asmdefContent);
                var assemblyName = json["name"]?.ToString();
                return assemblyName;
            }
            catch
            {
                return null; 
            }
        }
    }
}