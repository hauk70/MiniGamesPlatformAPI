using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public static class BuiltInBundlesUtils
    {
        public static async Task<bool> EnsureOnlyNecessaryCatalogsAreStored(IList<string> catalogUrls)
        {
            var existsFolders = Directory.GetDirectories(BuiltInBundlesManager.BuiltInPath)
                .Select(Path.GetDirectoryName)
                .ToHashSet();
            var expectedFolders = catalogUrls
                .Select(BuiltInBundlesManager.GetCatalogDirectory)
                .ToHashSet();

            foreach (var path in existsFolders.Except(expectedFolders))
                DeleteFolder(path);

            var results = await Task.WhenAll(catalogUrls.Select(EnsureCatalogDownloaded));

            if (results.Any() == false)
                Debug.LogError($"Some error while loading necessary catalogs: {string.Join('\n', catalogUrls)}");

            return true;
        }

        public static void ClearBuiltInFolder()
        {
            foreach (var path in Directory.GetDirectories(BuiltInBundlesManager.BuiltInPath))
                DeleteFolder(path);
        }

        public static async Task<bool> EnsureCatalogDownloaded(string catalogUrl)
        {
            var catalogGuid = BuiltInBundlesManager.GetCatalogGuid(catalogUrl);
            var catalogPath = BuiltInBundlesManager.GetCatalogFilePath(catalogUrl);
            if (Directory.Exists(Path.GetDirectoryName(catalogPath)) == false)
                Directory.CreateDirectory(Path.GetDirectoryName(catalogPath));

            if (File.Exists(catalogPath) == false)
            {
                var result = await DownloadFileAsync(catalogUrl, catalogPath);
                if (result == false)
                {
                    Debug.LogError($"Error downloading catalog file: {catalogUrl}");
                    return false;
                }
            }

            var hashPath = BuiltInBundlesManager.GetCatalogHashFilePath(catalogUrl);
            if (File.Exists(hashPath) == false)
            {
                var hashUrl = catalogUrl.Replace(".json", ".hash");
                var result = await DownloadFileAsync(hashUrl, hashPath);
                if (result == false)
                {
                    Debug.LogError($"Error downloading catalog file: {hashUrl}");
                    return false;
                }
            }

            var catalogHandle = Addressables.LoadContentCatalogAsync(catalogPath, false);
            await catalogHandle.Task;

            if (catalogHandle.Status != AsyncOperationStatus.Succeeded || catalogHandle.IsValid() == false)
            {
                Debug.LogError(
                    $"Failed to load catalog into Addressable: {catalogPath}\n{catalogHandle.OperationException}");
                Addressables.Release(catalogHandle);
                return false;
            }

            var bundleUrls = ExtractBundleUrls(catalogHandle.Result);
            if (bundleUrls.Count == 0)
            {
                Debug.LogError($"Catalog {catalogUrl} has no bundles to download");
                return false;
            }

            var results = await Task.WhenAll(bundleUrls.Select(bundleUrl =>
                DownloadFileAsync(bundleUrl, BuiltInBundlesManager.GetBundleFilePath(catalogGuid, bundleUrl))));

            if (results.Any(r => r) == false)
            {
                var failedUrls = bundleUrls.Zip(results, (url, result) => (url, result))
                    .Where(pair => pair.result == false)
                    .Select(pair => pair.url)
                    .ToArray();
                Debug.LogError($"Loading catalog bundles failed:\n\t{string.Join("\n\t", failedUrls)}");
                return false;
            }

            return true;
        }

        private static void DeleteFolder(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException e)
            {
                Debug.LogError($"Failed to delete folder {path}: {e.Message}");
            }
        }

        private static async Task<bool> DownloadFileAsync(string url, string destinationPath)
        {
            var data = await DataLoaderWithCache.DownloadData(url);
            if (data == null)
            {
                Debug.LogWarning($"Failed to download: {url}");
                return false;
            }

            await File.WriteAllBytesAsync(destinationPath, data);
            return true;
        }

        private static HashSet<string> ExtractBundleUrls(IResourceLocator locator)
        {
            var bundlePaths = new HashSet<string>();

            foreach (var key in locator.Keys)
            {
                if (!locator.Locate(key, null, out var locations))
                    continue;

                foreach (var location in locations)
                    if (location.ResourceType == typeof(IAssetBundleResource) && location.InternalId.StartsWith("http"))
                        bundlePaths.Add(location.InternalId);
            }

            return bundlePaths;
        }
    }
}