using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public static class PlatformLinksManager
    {
        private enum OperationType
        {
            Add,
            Remove,
            Rebuild
        }

        private const string LinkerFolderPath = "Assets";
        private const string LinkerFileName = "link.xml";
        private static string FullLinkerPath => Path.Combine(LinkerFolderPath, LinkerFileName);

        public static void RemoveConfig(MiniGameBehaviourConfig[] configs)
        {
            ModifyLinkXml(configs, OperationType.Remove);
        }

        public static void AddNewConfigs(MiniGameBehaviourConfig[] newBehaviourConfigs)
        {
            ModifyLinkXml(newBehaviourConfigs, OperationType.Add);
        }

        public static void RebuildConfigs(MiniGameBehaviourConfig[] configs)
        {
            ModifyLinkXml(configs, OperationType.Rebuild);
        }

        private static void ModifyLinkXml(MiniGameBehaviourConfig[] configs, OperationType operation)
        {
            var assemblyNames = configs.Select(GetAssemblyName)
                .Where(name => name != null)
                .Distinct()
                .ToHashSet();

            var xmlDoc = new XmlDocument();
            if (File.Exists(FullLinkerPath) && operation != OperationType.Rebuild)
                xmlDoc.Load(FullLinkerPath);
            else
                xmlDoc.LoadXml("<linker></linker>");

            var linkerNode = xmlDoc.SelectSingleNode("linker") ?? xmlDoc.AppendChild(xmlDoc.CreateElement("linker"));

            switch (operation)
            {
                case OperationType.Add:
                    AddAssemblies(xmlDoc, linkerNode, assemblyNames);
                    break;

                case OperationType.Remove:
                    RemoveAssemblies(linkerNode, assemblyNames);
                    break;

                case OperationType.Rebuild:
                    RebuildAssemblies(xmlDoc, linkerNode, assemblyNames);
                    break;
            }

            xmlDoc.Save(FullLinkerPath);
            AssetDatabase.Refresh();
        }

        private static void AddAssemblies(XmlDocument xmlDoc, XmlNode linkerNode, HashSet<string> assemblyNames)
        {
            foreach (var assemblyName in assemblyNames)
            {
                var existingNode = linkerNode.SelectSingleNode($"assembly[@fullname='{assemblyName}']");
                if (existingNode == null)
                {
                    var newAssemblyNode = xmlDoc.CreateElement("assembly");
                    newAssemblyNode.SetAttribute("fullname", assemblyName);
                    newAssemblyNode.SetAttribute("preserve", "all");
                    linkerNode.AppendChild(newAssemblyNode);
                }
            }
        }

        private static void RemoveAssemblies(XmlNode linkerNode, HashSet<string> assemblyNames)
        {
            foreach (var assemblyName in assemblyNames)
            {
                var existingNode = linkerNode.SelectSingleNode($"assembly[@fullname='{assemblyName}']");
                if (existingNode != null)
                    linkerNode.RemoveChild(existingNode);
            }
        }

        private static void RebuildAssemblies(XmlDocument xmlDoc, XmlNode linkerNode, HashSet<string> assemblyNames)
        {
            linkerNode.RemoveAll(); 

            foreach (var assemblyName in assemblyNames)
            {
                var newAssemblyNode = xmlDoc.CreateElement("assembly");
                newAssemblyNode.SetAttribute("fullname", assemblyName);
                newAssemblyNode.SetAttribute("preserve", "all");
                linkerNode.AppendChild(newAssemblyNode);
            }
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