using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public static class DataLoaderWithCache
    {
        private static readonly string CacheRoot = Path.Combine(Application.dataPath, "../Library/RemoteCache");

        public static async Task<byte[]> DownloadData(string url)
        {
            var guid = GenerateGuidFromUrl(url);
            var filePath = Path.Combine(CacheRoot, guid, Path.GetFileName(url));
            var metaPath = Path.Combine(CacheRoot, guid, "meta.json");

            Directory.CreateDirectory(Path.Combine(CacheRoot, guid));

            var meta = LoadMeta(metaPath);

            using var request = UnityWebRequest.Get(url);

            if (File.Exists(filePath))
            {
                if (!string.IsNullOrEmpty(meta.Etag))
                    request.SetRequestHeader("If-None-Match", meta.Etag);
                if (!string.IsNullOrEmpty(meta.LastModified))
                    request.SetRequestHeader("If-Modified-Since", meta.LastModified);
            }

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error downloading {url}: {request.error}");
                return File.Exists(filePath) ? await File.ReadAllBytesAsync(filePath) : null;
            }

            if (request.responseCode == 304)
                return await File.ReadAllBytesAsync(filePath);

            var fileData = request.downloadHandler.data;
            await File.WriteAllBytesAsync(filePath, fileData);
            SaveMeta(metaPath, request);

            return fileData;
        }

        // 14 days, 5 GB by default
        public static void CleanupCache(int cacheLifeTimeDays = 14, long maxCacheSizeBytes = 5368709120)
        {
            if (!Directory.Exists(CacheRoot))
                return;

            var directories = Directory.GetDirectories(CacheRoot);
            var now = DateTime.UtcNow;
            long totalCacheSize = 0;
            var fileInfos = new List<FileInfo>();

            foreach (var dir in directories)
            {
                var files = Directory.GetFiles(dir);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalCacheSize += fileInfo.Length;
                    fileInfos.Add(fileInfo);

                    if (now - fileInfo.LastWriteTimeUtc > TimeSpan.FromDays(cacheLifeTimeDays))
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Failed to delete file {file}: {e.Message}");
                        }
                    }
                }
            }

            if (totalCacheSize > maxCacheSizeBytes)
            {
                fileInfos = fileInfos.OrderBy(f => f.LastWriteTimeUtc).ToList();

                foreach (var file in fileInfos)
                {
                    if (totalCacheSize <= maxCacheSizeBytes)
                        break;

                    try
                    {
                        totalCacheSize -= file.Length;
                        File.Delete(file.FullName);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to delete file {file.FullName}: {e.Message}");
                    }
                }
            }
        }

        private static void SaveMeta(string metaPath, UnityWebRequest request)
        {
            var meta = new CacheMeta
            {
                Etag = request.GetResponseHeader("Etag"),
                LastModified = request.GetResponseHeader("Last-Modified")
            };
            File.WriteAllText(metaPath, JsonUtility.ToJson(meta, true));
        }

        private static CacheMeta LoadMeta(string metaPath)
        {
            if (File.Exists(metaPath) == false)
                return new CacheMeta();

            try
            {
                return JsonUtility.FromJson<CacheMeta>(File.ReadAllText(metaPath));
            }
            catch
            {
                return new CacheMeta();
            }
        }

        private static string GenerateGuidFromUrl(string url)
        {
            return BitConverter
                .ToString(
                    System.Security.Cryptography.MD5.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(url)))
                .Replace("-", "").ToLower();
        }

        [Serializable]
        private class CacheMeta
        {
            public string Etag;
            public string LastModified;
        }
    }
}