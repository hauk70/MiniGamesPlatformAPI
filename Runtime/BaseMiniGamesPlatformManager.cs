using System;
using System.Collections;
using System.Collections.Generic;
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
     * TODO 
     * subscribe to unity`s log message event to detect exceptions from mini games (current mini game) to force stop it
     * simple preloader
     * build in load
     */

    public class BaseMiniGamesPlatformManager : IMiniGamesPlatformManager
    {
        public IReadOnlyList<string> MiniGameNames =>
            Config.MiniGameConfigs.Select(mg => mg.Config.MiniGameName).ToList();

        public bool IsMiniGameRunning => MiniGameState?.RunningTask != null;
        public string CurrentMiniGameName => MiniGameState?.Name;
        public IMiniGameLoadingProgressHandler MiniGameLoadingProgressHandler => LoadingProgressHandler;

        protected readonly MiniGamesPlatformConfig Config;
        protected readonly IRenderPipelineManager RenderPipelineManager;
        protected readonly ISaveProvider SaveProvider;
        protected readonly IAnalyticsLogger AnalyticsLogger;
        protected readonly ILogger Logger;
        protected readonly ILogger MiniGameLogger;

        protected readonly MiniGameProxyLoadingProgressHandler LoadingProgressHandler =
            new MiniGameProxyLoadingProgressHandler();

        protected RunningMiniGameState MiniGameState;

        public BaseMiniGamesPlatformManager(MiniGamesPlatformConfig config,
            IRenderPipelineManager renderPipelineManager, ISaveProvider saveProvider, IAnalyticsLogger analyticsLogger,
            ILogger logger)
        {
            Config = config;
            RenderPipelineManager = renderPipelineManager;
            SaveProvider = new MiniGameSaveProvider(saveProvider, DecorateSaveProviderKey);
            AnalyticsLogger = new MiniGameAnalyticsLogger(analyticsLogger, DecorateAnalyticsKey);
            Logger = logger;
            MiniGameLogger = new MiniGameLogger(logger, DecorateLogger);
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

            var downloadHandle =
                Addressables.DownloadDependenciesAsync((IEnumerable)keysToDownload, Addressables.MergeMode.Union);
            await downloadHandle.Task;

            if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
                return false;
            Addressables.Release(downloadHandle);

            return true;
        }

        public async Task LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken)
        {
            if (MiniGameState != null)
                throw new InvalidOperationException("Another mini-game is already running.");

            EnsureMiniGameNameIsValid(miniGameName);
            var miniGameConfig = Config.MiniGameConfigs.First(c => c.Config.MiniGameName == miniGameName);

            var runningTaskSource = new TaskCompletionSource<object>();

            try
            {
                if (await IsMiniGameCacheReady(miniGameName) == false)
                    if (await PreloadGame(miniGameName) == false)
                    {
                        Logger.LogError(LogType.Exception.ToString(), $"Failed to preload mini-game: {miniGameName}");
                        return;
                    }

                var prevScene = SceneManager.GetActiveScene();
                var miniGameScene = CreateMiniGameScene(miniGameName);
                var catalog = await LoadCatalog(miniGameName);
                var entryPoint = await CreateEntryPoint(catalog, miniGameScene);

                MiniGameState = new RunningMiniGameState(miniGameName, miniGameScene, prevScene, entryPoint,
                    runningTaskSource);

                LoadingProgressHandler.SetHandler(MiniGameState.EntryPoint.LoadingProgressHandler);

                entryPoint.GameFinished += OnGameFinished;

                if (miniGameConfig.Config.CustomRenderPipelineAsset != null)
                    RenderPipelineManager.OverrideRenderPipeline(miniGameConfig.Config.CustomRenderPipelineAsset);

                entryPoint.Initialize(AnalyticsLogger, SaveProvider, MiniGameLogger);

                await entryPoint.Load();

                MiniGameState.SetTask(RunMiniGame(cancellationToken));

                await WaitForTaskWithCancellation(MiniGameState.RunningTask, cancellationToken);
            }
            catch (Exception ex)
            {
                runningTaskSource.TrySetException(ex);
                Logger.LogError(LogType.Exception.ToString(), $"Error during mini-game execution: {ex.Message}");
            }
            finally
            {
                await CleanupMiniGame();
            }
        }

        public async Task ForceEndMiniGame()
        {
            if (MiniGameState == null)
                return;

            try
            {
                MiniGameState.EntryPoint.ForceEndGame();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(),
                    $"Error during forced mini-game termination: {ex.Message}");
            }
            finally
            {
                await CleanupMiniGame();
            }
        }

        protected virtual string DecorateSaveProviderKey(string key)
        {
            if (MiniGameState == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate key for save provider.");
            if (MiniGameState.TaskCompletionSource.Task.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameState.Name}` is already completed. Cannot decorate key for save provider.");

            return $"{MiniGameState.Name}_{key}";
        }

        protected virtual string DecorateAnalyticsKey(string key)
        {
            if (MiniGameState == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate key for analytics logger.");
            if (MiniGameState.TaskCompletionSource.Task.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameState.Name}` is already completed. Cannot decorate key for analytics logger.");

            return $"{MiniGameState.Name}/{key}";
        }

        protected virtual string DecorateLogger(string message)
        {
            if (MiniGameState == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate message for logger.");
            if (MiniGameState.TaskCompletionSource.Task.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameState.Name}` is already completed. Cannot decorate message for logger.");

            return $"[{MiniGameState.Name}]: {message}";
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
                await MiniGameState.EntryPoint.GameEndAwaiter(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Log(LogType.Exception.ToString(), "Mini-game was canceled.");
                MiniGameState.TaskCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(), $"Error in mini-game: {ex.Message}");
                MiniGameState.TaskCompletionSource.TrySetException(ex);
            }
            finally
            {
                await MiniGameState.EntryPoint.Unload();
                MiniGameState.TaskCompletionSource.TrySetResult(null);
            }
        }

        private async Task CleanupMiniGame()
        {
            if (MiniGameState == null)
                return;

            try
            {
                MiniGameState.EntryPoint.GameFinished -= OnGameFinished;
                if(RenderPipelineManager.AreSettingsOverridden())
                    RenderPipelineManager.RestoreOriginalSettings();
                SceneManager.SetActiveScene(MiniGameState.PrevScene);
                await SceneManager.UnloadSceneAsync(MiniGameState.Scene).AsTask();
                await MiniGameState.EntryPoint.DisposeAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(), $"Error during mini-game cleanup: {ex.Message}");
            }
            finally
            {
                MiniGameState = null;
            }
        }

        private void OnGameFinished()
        {
            MiniGameState?.TaskCompletionSource.TrySetResult(null);
        }

        private void EnsureMiniGameNameIsValid(string miniGameName)
        {
            if (string.IsNullOrWhiteSpace(miniGameName))
                throw new ArgumentException("Mini-game name cannot be null or empty.", nameof(miniGameName));

            if (Config.MiniGameConfigs.Any(c => c.Config.MiniGameName == miniGameName) == false)
                throw new ArgumentException("Mini-game name not found.", nameof(miniGameName));
        }

        private async Task<IResourceLocator> LoadCatalog(string miniGameName)
        {
            var miniGameBehaviourConfig =
                Config.MiniGameConfigs.FirstOrDefault(c => c.Config.MiniGameName == miniGameName);
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
                throw new Exception($"Entry point not found in the catalog `{catalog.LocatorId}`");

            var prefab = await Addressables.LoadAssetAsync<GameObject>(d.First()).Task;
            var entryPointGameObject = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(entryPointGameObject, scene);
            var entryPoint = entryPointGameObject.GetComponent<IMiniGameEntryPoint>();

            if (entryPoint == null)
                throw new Exception($"Entry point not found in the game object `{entryPointGameObject}`");

            return entryPoint;
        }

        protected sealed class RunningMiniGameState
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