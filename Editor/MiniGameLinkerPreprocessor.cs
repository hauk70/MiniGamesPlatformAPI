using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public class MiniGameLinkerPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 50;

        private const string DestinationFolder = "Assets/Settings";
        private static readonly string LinkXmlPath = Path.Combine(DestinationFolder, "link.xml");

        public void OnPreprocessBuild(BuildReport report)
        {
            if (File.Exists(LinkXmlPath))
                File.Delete(LinkXmlPath);

            GenerateCustomLinkXml();
        }

        private static void GenerateCustomLinkXml()
        {
            Directory.CreateDirectory(DestinationFolder);

            var linkXmlGenerator = UnityEditor.Build.Pipeline.Utilities.LinkXmlGenerator.CreateDefault();

            var configGuid = AssetDatabase.FindAssets($"t:{nameof(MiniGamesPlatformConfig)}").FirstOrDefault();
            if (configGuid == null)
                throw new Exception($"Could not find {nameof(MiniGamesPlatformConfig)} asset.");

            var config =
                AssetDatabase.LoadAssetAtPath<MiniGamesPlatformConfig>(AssetDatabase.GUIDToAssetPath(configGuid));

            foreach (var miniGameConfig in config.MiniGameConfigs)
            {
                var assemblyNames = GetAssemblyNames(miniGameConfig);
                foreach (var assemblyName in assemblyNames)
                {
                    var assembly = GetAssemblyByName(assemblyName);
                    if (assembly != null)
                        linkXmlGenerator.AddAssemblies(assembly);
                }
            }

            var additionAssemblies = FindAndInstantiateInstallers().SelectMany(installer => installer.GetAssemblies());
            linkXmlGenerator.AddAssemblies(additionAssemblies);

            linkXmlGenerator.AddAssemblies(typeof(BaseMiniGamesPlatformManager).GetTypeInfo().Assembly);
            linkXmlGenerator.Save(LinkXmlPath);
        }

        public static List<IInstallCustomAssemblies> FindAndInstantiateInstallers()
        {
            var installers = new List<IInstallCustomAssemblies>();
            var interfaceType = typeof(IInstallCustomAssemblies);

            var types = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch (ReflectionTypeLoadException e)
                    {
                        return e.Types.Where(t => t != null);
                    }
                })
                .Where(t => interfaceType.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

            foreach (var type in types)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IInstallCustomAssemblies instance)
                        installers.Add(instance);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to instantiate {type.FullName}: {e.Message}");
                }
            }

            return installers;
        }

        private static List<string> GetAssemblyNames(MiniGameBehaviourConfig config)
        {
            var configAssetPath = AssetDatabase.GetAssetPath(config?.Config);
            if (string.IsNullOrEmpty(configAssetPath))
                return new List<string>();

            var packagePath = Path.GetDirectoryName(configAssetPath);
            var asmdefFiles = Directory.GetFiles(packagePath, "*.asmdef", SearchOption.AllDirectories);

            var assemblyNames = new HashSet<string>();

            foreach (var asmdefFile in asmdefFiles)
            {
                var asmdefContent = File.ReadAllText(asmdefFile);
                var assemblyName = ExtractAssemblyNameFromAsmdef(asmdefContent);

                if (string.IsNullOrEmpty(assemblyName) == false && IsEditorOnlyAssembly(asmdefContent) == false)
                {
                    assemblyNames.Add(assemblyName);
                    ResolveDependencies(asmdefContent, assemblyNames);
                }
            }

            return assemblyNames.ToList();
        }

        private static void ResolveDependencies(string asmdefContent, HashSet<string> assemblyNames)
        {
            try
            {
                var json = JObject.Parse(asmdefContent);
                var references = json["references"]?.ToObject<string[]>();

                if (references == null || references.Length == 0)
                    return;

                foreach (var reference in references)
                {
                    var assemblyName = reference.Replace("GUID:", "");
                    if (assemblyNames.Contains(assemblyName) || IsEditorOnlyAssemblyByName(assemblyName))
                        continue;

                    assemblyNames.Add(assemblyName);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MiniGameLinkerPreprocessor] Error while resolving dependencies: {e.Message}");
            }
        }

        private static string ExtractAssemblyNameFromAsmdef(string asmdefContent)
        {
            try
            {
                var json = JObject.Parse(asmdefContent);
                return json["name"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static Assembly GetAssemblyByName(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
        }

        private static bool IsEditorOnlyAssembly(string asmdefContent)
        {
            var json = JObject.Parse(asmdefContent);
            return json["includePlatforms"]?.Any(x => x.ToString() == "Editor") ?? false;
        }

        private static bool IsEditorOnlyAssemblyByName(string assemblyName)
        {
            var asmdefFile = FindAsmdefFileByAssemblyName(assemblyName);
            if (asmdefFile == null)
                return false;

            return IsEditorOnlyAssembly(File.ReadAllText(asmdefFile));
        }

        private static string FindAsmdefFileByAssemblyName(string assemblyName)
        {
            return Directory
                .GetFiles(Application.dataPath, "*.asmdef", SearchOption.AllDirectories)
                .FirstOrDefault(f => File.ReadAllText(f).Contains($"\"name\": \"{assemblyName}\""));
        }
    }
}