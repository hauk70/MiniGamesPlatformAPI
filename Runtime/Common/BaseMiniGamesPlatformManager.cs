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
     * builtin mini-games
     */

    public class BaseMiniGamesPlatformManager : IMiniGamesPlatformManager
    {
        public IReadOnlyList<string> MiniGameNames =>
            Config.MiniGameConfigs.Select(mg => mg.Config.MiniGameName).ToList();

        public IMiniGameRunningBehaviour MiniGameRunningBehaviour => _miniGameRunningBehaviour;
        private MiniGameRunningBehaviour _miniGameRunningBehaviour;

        protected readonly MiniGamesPlatformConfig Config;
        protected readonly IRenderPipelineManager RenderPipelineManager;
        protected readonly ISaveProvider SaveProvider;
        protected readonly IAnalyticsLogger AnalyticsLogger;
        protected readonly ILogger Logger;
        protected readonly ILogger MiniGameLogger;

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

        public async Task<bool> IsMiniGameCacheReady(MiniGameBehaviourConfig miniGameConfig)
        {
            var catalog = await LoadCatalog(miniGameConfig);
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

        public async Task<bool> PreloadGame(MiniGameBehaviourConfig miniGameConfig)
        {
            var catalog = await LoadCatalog(miniGameConfig);
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

        public IMiniGameRunningBehaviour LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken)
        {
            if (MiniGameRunningBehaviour != null)
                throw new InvalidOperationException("Another mini-game is already running.");

            EnsureMiniGameNameIsValid(miniGameName);
            var miniGameConfig = Config.MiniGameConfigs.First(c => c.Config.MiniGameName == miniGameName);

            var loadingProgressHandler = new MiniGameLoadingProgressHandler();
            var unloadingProgressHandler = new MiniGameLoadingProgressHandler();

            var taskCompletionSource = new TaskCompletionSource<object>();

            _miniGameRunningBehaviour = new MiniGameRunningBehaviour(
                miniGameName,
                cancellationToken,
                taskCompletionSource,
                loadingProgressHandler,
                unloadingProgressHandler
            );
            
            _miniGameRunningBehaviour.SetTask(RunMiniGameAsync(miniGameConfig, cancellationToken, taskCompletionSource));
            return MiniGameRunningBehaviour;
        }

        private async Task RunMiniGameAsync(MiniGameBehaviourConfig miniGameConfig, CancellationToken cancellationToken,
            TaskCompletionSource<object> taskSource)
        {
            try
            {
                _miniGameRunningBehaviour.SetState(MiniGameState.ResourcesLoading);
                if (await IsMiniGameCacheReady(miniGameConfig) == false)
                {
                    if (await PreloadGame(miniGameConfig) == false)
                        throw new Exception($"Failed to preload mini-game: {miniGameConfig.Config.MiniGameName}");
                }

                var prevScene = SceneManager.GetActiveScene();
                var miniGameScene = CreateMiniGameScene(miniGameConfig.Config.MiniGameName);
                var catalog = await LoadCatalog(miniGameConfig);
                var entryPoint = await CreateEntryPoint(catalog, miniGameScene);

                _miniGameRunningBehaviour.SetStateData(
                    new RunningMiniGameStateData(miniGameScene, prevScene, entryPoint));

                entryPoint.GameFinished += OnGameFinished;
                entryPoint.LoadingProgressHandler.ProgressChanged += OnEntryPointLoadingProgressChanged;

                if (miniGameConfig.Config.CustomRenderPipelineAsset != null)
                    RenderPipelineManager.OverrideRenderPipeline(miniGameConfig.Config.CustomRenderPipelineAsset);

                _miniGameRunningBehaviour.SetState(MiniGameState.Initializing);

                entryPoint.Initialize(AnalyticsLogger, SaveProvider, MiniGameLogger);

                _miniGameRunningBehaviour.SetState(MiniGameState.Loading);

                await entryPoint.Load();

                await RunMiniGame(cancellationToken);
            }
            catch (Exception ex)
            {
                taskSource.TrySetException(ex);
                Logger.LogError(LogType.Exception.ToString(), $"Error during mini-game execution: {ex.Message}\n{ex.StackTrace}");
                _miniGameRunningBehaviour.SetException(ex);
            }
            finally
            {
                await CleanupMiniGame();
                taskSource.TrySetResult(null);
            }
        }

        public async Task ForceEndMiniGame()
        {
            if (MiniGameRunningBehaviour == null)
                return;

            try
            {
                MiniGameRunningBehaviour.StateData.EntryPoint.ForceEndGame();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(),
                    $"Error during forced mini-game termination: {ex.Message}");
                _miniGameRunningBehaviour.SetException(ex);
            }
            finally
            {
                await CleanupMiniGame();
            }
        }

        protected virtual string DecorateSaveProviderKey(string key)
        {
            if (MiniGameRunningBehaviour == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate key for save provider.");
            if (MiniGameRunningBehaviour.ActiveTask.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameRunningBehaviour.Name}` is already completed. Cannot decorate key for save provider.");

            return $"{MiniGameRunningBehaviour.Name}_{key}";
        }

        protected virtual string DecorateAnalyticsKey(string key)
        {
            if (MiniGameRunningBehaviour == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate key for analytics logger.");
            if (MiniGameRunningBehaviour.ActiveTask.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameRunningBehaviour.Name}` is already completed. Cannot decorate key for analytics logger.");

            return $"{MiniGameRunningBehaviour.Name}/{key}";
        }

        protected virtual string DecorateLogger(string message)
        {
            if (MiniGameRunningBehaviour == null)
                throw new InvalidOperationException("Mini game is null. Cannot decorate message for logger.");
            if (MiniGameRunningBehaviour.ActiveTask.IsCompleted)
                throw new InvalidOperationException(
                    $"Mini game `{MiniGameRunningBehaviour.Name}` is already completed. Cannot decorate message for logger.");

            return $"[{MiniGameRunningBehaviour.Name}]: {message}";
        }

        private async Task RunMiniGame(CancellationToken cancellationToken)
        {
            try
            {
                _miniGameRunningBehaviour.SetState(MiniGameState.Running);
                await MiniGameRunningBehaviour.StateData.EntryPoint.GameEndAwaiter(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Logger.Log(LogType.Exception.ToString(), "Mini-game was canceled.");
                _miniGameRunningBehaviour.TaskCompletionSource.TrySetCanceled();
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(), $"Error in mini-game: {ex.Message}");
                _miniGameRunningBehaviour.TaskCompletionSource.TrySetException(ex);
                _miniGameRunningBehaviour.SetException(ex);
            }
            finally
            {
                _miniGameRunningBehaviour.SetState(MiniGameState.Unloading);

                await MiniGameRunningBehaviour.StateData.EntryPoint.Unload();
                _miniGameRunningBehaviour.TaskCompletionSource.TrySetResult(null);
            }
        }

        private async Task CleanupMiniGame()
        {
            if (MiniGameRunningBehaviour == null)
                return;

            try
            {
                MiniGameRunningBehaviour.StateData.EntryPoint.GameFinished -= OnGameFinished;
                MiniGameRunningBehaviour.StateData.EntryPoint.LoadingProgressHandler.ProgressChanged -=
                    OnEntryPointLoadingProgressChanged;

                if (RenderPipelineManager.AreSettingsOverridden())
                    RenderPipelineManager.RestoreOriginalSettings();
                SceneManager.SetActiveScene(MiniGameRunningBehaviour.StateData.PrevScene);
                await SceneManager.UnloadSceneAsync(MiniGameRunningBehaviour.StateData.Scene).AsTask();
                await MiniGameRunningBehaviour.StateData.EntryPoint.DisposeAsync();

                _miniGameRunningBehaviour.SetState(MiniGameState.Finished);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(), $"Error during mini-game cleanup: {ex.Message}");
                _miniGameRunningBehaviour.SetException(ex);
            }
            finally
            {
                _miniGameRunningBehaviour = null;
            }
        }

        private void OnGameFinished()
        {
            _miniGameRunningBehaviour?.TaskCompletionSource.TrySetResult(null);
        }

        private void OnEntryPointLoadingProgressChanged(float progress)
        {
            if (MiniGameRunningBehaviour.State == MiniGameState.Loading)
                _miniGameRunningBehaviour.LoadingProgressHandlerImpl.Progress = progress;
            else if (MiniGameRunningBehaviour.State == MiniGameState.Unloading)
                _miniGameRunningBehaviour.UnloadingProgressHandlerImpl.Progress = progress;
        }

        private void EnsureMiniGameNameIsValid(string miniGameName)
        {
            if (string.IsNullOrWhiteSpace(miniGameName))
                throw new ArgumentException("Mini-game name cannot be null or empty.", nameof(miniGameName));

            if (Config.MiniGameConfigs.Any(c => c.Config.MiniGameName == miniGameName) == false)
                throw new ArgumentException("Mini-game name not found.", nameof(miniGameName));
        }

        private async Task<IResourceLocator> LoadCatalog(MiniGameBehaviourConfig behaviourConfig)
        {
            if (behaviourConfig == null)
                return null;
            var config = behaviourConfig.Config;

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
    }
}