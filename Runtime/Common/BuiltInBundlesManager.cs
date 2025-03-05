using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace com.appidea.MiniGamePlatform.Core
{
    public class BuiltInBundlesManager
    {
        public static readonly string BuiltInPath = Path.Combine(Application.streamingAssetsPath, "BuiltInBundles");
        private static readonly Dictionary<string, string> URLToGuidCache = new();

        public static string GetCatalogDirectory(string url) =>
            Path.Combine(BuiltInPath, GetCatalogGuid(url));

        public static string GetCatalogFilePath(string url) =>
            Path.Combine(GetCatalogDirectory(url), $"catalog.json");

        public static string GetCatalogHashFilePath(string url) =>
            Path.Combine(GetCatalogDirectory(url), $"catalog.hash");

        public static string GetBundleFilePath(string catalogGuid, string bundleUrl) =>
            Path.Combine(BuiltInPath, catalogGuid, Path.GetFileName(bundleUrl));

        public static string GetCatalogGuid(string url)
        {
            if (URLToGuidCache.TryGetValue(url, out string guid))
                return guid;

            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            guid = BitConverter.ToString(hash).Replace("-", "").ToLower();
            URLToGuidCache.Add(url, guid);
            return guid;
        }

        // я возвращаю хендл на старый каталог локатор, но сам его оверрайжу и забываю
        public static async Task<AsyncOperationHandle<IResourceLocator>> LoadLocalCatalogAndReplaceLocator(string catalogUrl)
        {
            var localCatalogPath = GetCatalogFilePath(catalogUrl);
            if (!File.Exists(localCatalogPath))
            {
                Debug.LogError($"Local catalog not found: {localCatalogPath}");
                return default;
            }

            var handle = AddressableLifecycleManager.Instance.LoadContentCatalogAsync(localCatalogPath, false);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load local catalog: {localCatalogPath}\n{handle.OperationException}");
                Addressables.Release(handle);
                return default;
            }

            var originalLocator = handle.Result;
            if (originalLocator == null)
            {
                Debug.LogError($"Loaded catalog but no locator found: {localCatalogPath}");
                return default;
            }

            var localLocator = new BuiltInBundlesLocator(originalLocator, catalogUrl);

            Addressables.RemoveResourceLocator(originalLocator);
            Addressables.AddResourceLocator(localLocator);

            return handle;
        }
    }
}