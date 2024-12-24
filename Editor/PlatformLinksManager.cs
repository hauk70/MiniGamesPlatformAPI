using System.IO;
using System.Linq;
using System.Text;
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
            var assembly = config?.Config?.GetType().Assembly;
            return assembly?.GetName().Name ?? "UnknownAssembly";
        }
    }
}