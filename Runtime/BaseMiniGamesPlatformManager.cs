using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace com.appidea.MiniGamePlatform.Core
{
    /*
     * read only built in property (for now)
     * build in load
     */
    public sealed class BaseMiniGamesPlatformManager : IMiniGamesPlatformManager
    {
        public IReadOnlyList<string> MiniGameNames =>
            _config.MiniGameConfigs.Select(mg => mg.Config.MiniGameName).ToList();

        public bool IsMiniGameRunning => _miniGameState?.RunningTask != null;
        public string CurrentMiniGameName => _miniGameState?.Name;
        public IMiniGameLoadingProgressHandler MiniGameLoadingProgressHandler => _loadingProgressHandler;

        private readonly MiniGamesPlatformConfig _config;
        private readonly ISaveProvider _saveProvider;
        private readonly IAnalyticsLogger _analyticsLogger;
        private readonly ILogger _logger;

        private readonly MiniGameProxyLoadingProgressHandler _loadingProgressHandler =
            new MiniGameProxyLoadingProgressHandler();

        private RunningMiniGameState _miniGameState;

        public BaseMiniGamesPlatformManager(MiniGamesPlatformConfig config, ISaveProvider saveProvider,
            IAnalyticsLogger analyticsLogger, ILogger logger)
        {
            _config = config;
            _saveProvider = saveProvider;
            _analyticsLogger = analyticsLogger;
            _logger = logger;
        }

        public async Task<bool> IsMiniGameCacheReady(string miniGameName)
        {
            EnsureMiniGameNameIsValid(miniGameName);

            var catalog = await LoadCatalog(miniGameName);
            if (catalog == null)
                return false;

            foreach (var key in catalog.Keys)
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(key);
                await sizeHandle.Task;

                if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
                    return false;
            }

            return true;
        }

        public async Task<bool> PreloadGame(string miniGameName)
        {
            EnsureMiniGameNameIsValid(miniGameName);

            var catalog = await LoadCatalog(miniGameName);
            if (catalog == null)
                return false;

            var keysToDownload = new List<object>();
            foreach (var key in catalog.Keys)
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(key);
                await sizeHandle.Task;

                if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
                    keysToDownload.Add(key);
            }

            if (keysToDownload.Count <= 0)
                return true;

            var downloadHandle = Addressables.DownloadDependenciesAsync((IEnumerable)keysToDownload, Addressables.MergeMode.Union);
            await downloadHandle.Task;

            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
                return false;
            Addressables.Release(downloadHandle);

            return true;
        }

        public async Task LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken)
        {
            if (_miniGameState != null)
                throw new InvalidOperationException("Another mini-game is already running.");

            EnsureMiniGameNameIsValid(miniGameName);

            var runningTaskSource = new TaskCompletionSource<object>();

            try
            {
                if (await IsMiniGameCacheReady(miniGameName) == false)
                    if (await PreloadGame(miniGameName) == false)
                    {
                        _logger.LogError(LogType.Exception.ToString(), $"Failed to preload mini-game: {miniGameName}");
                        return;
                    }

                var prevScene = SceneManager.GetActiveScene();
                var miniGameScene = CreateMiniGameScene(miniGameName);
                var catalog = await LoadCatalog(miniGameName);
                var entryPoint = await CreateEntryPoint(catalog, miniGameScene);

                _miniGameState = new RunningMiniGameState(miniGameName, miniGameScene, prevScene, entryPoint,
                    runningTaskSource);

                _loadingProgressHandler.SetHandler(_miniGameState.EntryPoint.LoadingProgressHandler);

                entryPoint.GameFinished += OnGameFinished;

                entryPoint.Initialize(_analyticsLogger, _saveProvider, _logger);

                await entryPoint.Load();

                _miniGameState.SetTask(RunMiniGame(cancellationToken));

                await WaitForTaskWithCancellation(_miniGameState.RunningTask, cancellationToken);
            }
            catch (Exception ex)
            {
                runningTaskSource.TrySetException(ex);
                _logger.LogError(LogType.Exception.ToString(), $"Error during mini-game execution: {ex.Message}");
            }
            finally
            {
                await CleanupMiniGame();
            }
        }

        public async Task ForceEndMiniGame()
        {
            if (_miniGameState == null)
                return;

            try
            {
                _miniGameState.EntryPoint.ForceEndGame();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogType.Exception.ToString(),
                    $"Error during forced mini-game termination: {ex.Message}");
            }
            finally
            {
                await CleanupMiniGame();
            }
        }

        private async Task WaitForTaskWithCancellation(Task task, CancellationToken cancellationToken)
        {
            var cancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

            var completedTask = await Task.WhenAny(task, cancellationTask);
            if (completedTask == cancellationTask)
                throw new OperationCanceledException(cancellationToken);

            await task;
        }

        private async Task RunMiniGame(CancellationToken cancellationToken)
        {
            try
            {
                await _miniGameState.EntryPoint.GameEndAwaiter(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogType.Exception.ToString(), "Mini-game was canceled.");
                _miniGameState.TaskCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogType.Exception.ToString(), $"Error in mini-game: {ex.Message}");
                _miniGameState.TaskCompletionSource.TrySetException(ex);
            }
            finally
            {
                await _miniGameState.EntryPoint.Unload();
                _miniGameState.TaskCompletionSource.TrySetResult(null);
            }
        }

        private async Task CleanupMiniGame()
        {
            if (_miniGameState == null)
                return;

            try
            {
                _miniGameState.EntryPoint.GameFinished -= OnGameFinished;
                SceneManager.SetActiveScene(_miniGameState.PrevScene);
                await SceneManager.UnloadSceneAsync(_miniGameState.Scene).AsTask();
                await _miniGameState.EntryPoint.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(LogType.Exception.ToString(), $"Error during mini-game cleanup: {ex.Message}");
            }
            finally
            {
                _miniGameState = null;
            }
        }

        private void OnGameFinished()
        {
            _miniGameState?.TaskCompletionSource.TrySetResult(null);
        }

        private void EnsureMiniGameNameIsValid(string miniGameName)
        {
            if (string.IsNullOrWhiteSpace(miniGameName))
                throw new ArgumentException("Mini-game name cannot be null or empty.", nameof(miniGameName));

            if (_config.MiniGameConfigs.Any(c => c.Config.MiniGameName == miniGameName) == false)
                throw new ArgumentException("Mini-game name not found.", nameof(miniGameName));
        }

        private async Task<IResourceLocator> LoadCatalog(string miniGameName)
        {
            var miniGameBehaviourConfig =
                _config.MiniGameConfigs.FirstOrDefault(c => c.Config.MiniGameName == miniGameName);
            if (miniGameBehaviourConfig == null)
                return null;
            var config = miniGameBehaviourConfig.Config;

            // todo load catalog from streaming assets if load type is MiniGameLoadType.BuiltIn 

            var catalogPath = MiniGamePlatformUtils.CombineUrl(
                                  config.Url,
                                  config.MiniGameName,
                                  MiniGamePlatformUtils.GetPlatformTargetName(),
                                  config.Version)
                              + $"catalog_{config.Version}.json";

            var handle = Addressables.LoadContentCatalogAsync(catalogPath);
            await handle.Task;

            return handle.Status != AsyncOperationStatus.Succeeded
                ? null
                : handle.Result;
        }

        private Scene CreateMiniGameScene(string miniGameName)
        {
            var scene = SceneManager.CreateScene($"MiniGame_{miniGameName}");
            SceneManager.SetActiveScene(scene);
            return scene;
        }

        private async Task<IMiniGameEntryPoint> CreateEntryPoint(IResourceLocator catalog, Scene scene)
        {
            if (catalog.Locate(MiniGamePlatformConstants.EntryPointAddress, typeof(GameObject), out var d) == false)
                throw new Exception($"Entry point not found in catalog `{catalog.LocatorId}`");

            var prefab = await Addressables.LoadAssetAsync<GameObject>(d.First()).Task;
            var entryPointGameObject = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(entryPointGameObject, scene);
            return entryPointGameObject.GetComponent<IMiniGameEntryPoint>();
        }

        private sealed class RunningMiniGameState
        {
            public readonly string Name;
            public readonly Scene Scene;
            public readonly Scene PrevScene;
            public readonly IMiniGameEntryPoint EntryPoint;
            public readonly TaskCompletionSource<object> TaskCompletionSource;
            public Task RunningTask { get; private set; }

            public RunningMiniGameState(string name, Scene scene, Scene prevScene, IMiniGameEntryPoint entryPoint,
                TaskCompletionSource<object> taskCompletionSource)
            {
                Name = name;
                Scene = scene;
                PrevScene = prevScene;
                EntryPoint = entryPoint;
                TaskCompletionSource = taskCompletionSource;
            }

            public void SetTask(Task task)
            {
                RunningTask = task;
            }
        }
    }
}