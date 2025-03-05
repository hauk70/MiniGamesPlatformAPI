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

        public async Task<CompositeHandle<bool>> IsMiniGameCacheReady(MiniGameBehaviourConfig miniGameConfig)
        {
            var catalogHandle = await LoadCatalog(miniGameConfig);
            var compositeHandle = new CompositeHandle<bool>(catalogHandle);
            if (catalogHandle.Status == AsyncOperationStatus.Failed)
                return compositeHandle;

            var result = true;

            await Task.WhenAll(catalogHandle.Result.Keys.Select(async key =>
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(key);
                compositeHandle.Add(sizeHandle);
                await sizeHandle.Task;

                if (sizeHandle.Status == AsyncOperationStatus.Failed)
                {
                    result = false;
                    return;
                }

                if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
                    result = false;
            }));

            compositeHandle.Value = result;

            return compositeHandle;
        }

        public async Task<CompositeHandle<bool>> DownloadGame(MiniGameBehaviourConfig miniGameConfig)
        {
            var catalogHandle = await LoadCatalog(miniGameConfig);
            var compositeHandle = new CompositeHandle<bool>(catalogHandle);
            if (catalogHandle.Status == AsyncOperationStatus.Failed)
                return compositeHandle;

            var keysToDownload = new List<object>();
            var success = true;
            await Task.WhenAll(catalogHandle.Result.Keys.Select(async key =>
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(key);
                compositeHandle.Add(sizeHandle);
                await sizeHandle.Task;

                if (sizeHandle.Status == AsyncOperationStatus.Failed)
                {
                    success = false;
                    return;
                }

                if (sizeHandle.Status == AsyncOperationStatus.Succeeded && sizeHandle.Result > 0)
                    keysToDownload.Add(key);
            }));

            if (success == false)
            {
                compositeHandle.Value = false;
                return compositeHandle;
            }

            if (keysToDownload.Count <= 0)
            {
                compositeHandle.Value = true;
                return compositeHandle;
            }

            var downloadHandle =
                Addressables.DownloadDependenciesAsync((IEnumerable)keysToDownload, Addressables.MergeMode.Union);
            compositeHandle.Add(downloadHandle);
            await downloadHandle.Task;

            compositeHandle.Value = downloadHandle.Status == AsyncOperationStatus.Succeeded;

            return compositeHandle;
        }

        public IMiniGameRunningBehaviour LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken,
            GameOverScreenData gameOverScreenData = null)
        {
            if (MiniGameRunningBehaviour != null)
                throw new InvalidOperationException("Another mini-game is already running.");

            EnsureMiniGameNameIsValid(miniGameName);
            var miniGameConfig = Config.MiniGameConfigs.First(c => c.Config.MiniGameName == miniGameName);

            var loadingProgressHandler = new MiniGameLoadingProgressHandler();
            var unloadingProgressHandler = new MiniGameLoadingProgressHandler();

            var taskCompletionSource = new TaskCompletionSource<object>();
            var handle = new CompositeHandle();

            _miniGameRunningBehaviour = new MiniGameRunningBehaviour(
                miniGameName,
                handle,
                cancellationToken,
                taskCompletionSource,
                loadingProgressHandler,
                unloadingProgressHandler
            );

            _miniGameRunningBehaviour.SetTask(RunMiniGameAsync(miniGameConfig, handle, gameOverScreenData,
                cancellationToken,
                taskCompletionSource));
            return MiniGameRunningBehaviour;
        }

        private async Task RunMiniGameAsync(MiniGameBehaviourConfig miniGameConfig,
                CompositeHandle compositeHandle, GameOverScreenData gameOverScreenData,
                CancellationToken cancellationToken, TaskCompletionSource<object> taskSource)
        {
            try
            {
                _miniGameRunningBehaviour.SetState(MiniGameState.ResourcesLoading);
                var downloadHandle = await DownloadGame(miniGameConfig);
                compositeHandle.Add(downloadHandle);
                if (downloadHandle.Value == false)
                    throw new Exception($"Failed to preload mini-game: {miniGameConfig.Config.MiniGameName}");

                var prevScene = SceneManager.GetActiveScene();
                var miniGameScene = CreateMiniGameScene(miniGameConfig.Config.MiniGameName);
                var catalogHandle = await LoadCatalog(miniGameConfig);
                compositeHandle.Add(catalogHandle);

                if (catalogHandle.IsValid() == false || catalogHandle.Status != AsyncOperationStatus.Succeeded)
                    throw new Exception($"Failed to load mini-game: {miniGameConfig.Config.MiniGameName}");

                var entryPoint = await CreateEntryPoint(catalogHandle.Result, miniGameScene);

                _miniGameRunningBehaviour.SetStateData(
                    new RunningMiniGameStateData(miniGameScene, prevScene, entryPoint));

                entryPoint.MessageSent += OnMiniGameMessageReceived;
                entryPoint.LoadingProgressHandler.ProgressChanged += OnEntryPointLoadingProgressChanged;

                if (miniGameConfig.Config.CustomRenderPipelineAsset != null)
                    RenderPipelineManager.OverrideRenderPipeline(miniGameConfig.Config.CustomRenderPipelineAsset);

                _miniGameRunningBehaviour.SetState(MiniGameState.Initializing);

                var runArguments =
                    new MiniGameRunArguments(AnalyticsLogger, SaveProvider, MiniGameLogger, gameOverScreenData);
                entryPoint.Initialize(runArguments);

                _miniGameRunningBehaviour.SetState(MiniGameState.Loading);

                await entryPoint.Load();

                await RunMiniGame(cancellationToken);
            }
            catch (Exception ex)
            {
                taskSource.TrySetException(ex);
                Logger.LogError(LogType.Exception.ToString(),
                    $"Error during mini-game execution: {ex.Message}\n{ex.StackTrace}");
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
                if (MiniGameRunningBehaviour.StateData.EntryPoint != null)
                {
                    MiniGameRunningBehaviour.StateData.EntryPoint.MessageSent -= OnMiniGameMessageReceived;
                    MiniGameRunningBehaviour.StateData.EntryPoint.LoadingProgressHandler.ProgressChanged -=
                        OnEntryPointLoadingProgressChanged;
                }

                if (RenderPipelineManager.AreSettingsOverridden())
                    RenderPipelineManager.RestoreOriginalSettings();

                if (MiniGameRunningBehaviour.StateData.Scene.IsValid())
                {
                    SceneManager.SetActiveScene(MiniGameRunningBehaviour.StateData.PrevScene);
                    await SceneManager.UnloadSceneAsync(MiniGameRunningBehaviour.StateData.Scene).AsTask();
                    if (MiniGameRunningBehaviour.StateData.EntryPoint != null)
                        await MiniGameRunningBehaviour.StateData.EntryPoint.DisposeAsync();
                }

                _miniGameRunningBehaviour.SetState(MiniGameState.Finished);
            }
            catch (Exception ex)
            {
                Logger.LogError(LogType.Exception.ToString(), $"Error during mini-game cleanup: {ex.Message}");
                _miniGameRunningBehaviour.SetException(ex);
            }
            finally
            {
                _miniGameRunningBehaviour.Handle.Dispose();
                _miniGameRunningBehaviour = null;
            }
        }

        private void OnMiniGameMessageReceived(IMessage message)
        {
            switch (message)
            {
                case GameFinished _:
                case GameFinishedRunNextMiniGame _:
                    _miniGameRunningBehaviour?.TaskCompletionSource.TrySetResult(null);
                    break;
                default:
                    throw new NotImplementedException($"Unhandled message type: {message.GetType()}");
            }

            _miniGameRunningBehaviour?.SetMessage(message);
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

        private async Task<AsyncOperationHandle<IResourceLocator>> LoadCatalog(MiniGameBehaviourConfig behaviourConfig)
        {
            if (behaviourConfig == null)
                return default;
            var config = behaviourConfig.Config;

            var platform = MiniGamePlatformUtils.GetPlatformTargetName();
            AsyncOperationHandle<IResourceLocator> handle;

            if (behaviourConfig.LoadType == CatalogLoadType.BuiltIn)
            {
                var localCatalogPath =
                    BuiltInBundlesManager.GetCatalogFilePath(behaviourConfig.Config.GetFullUrl(platform));
                handle = await BuiltInBundlesManager.LoadLocalCatalogAndReplaceLocator(localCatalogPath);
            }
            else
            {
                handle = AddressableLifecycleManager.Instance.LoadContentCatalogAsync(config.GetFullUrl(platform));
                await handle.Task;
            }

            if (handle.IsValid() == false)
            {
                Logger.Log(LogType.Error.ToString(),
                    $"Failed to load catalog(s) for the mini game {behaviourConfig.Config.MiniGameName}.");
                return default;
            }

            if (handle.Status == AsyncOperationStatus.Failed)
            {
                // удалить оверрайд локатора если он был тоже
                return handle;
            }

            return handle;
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
                throw new Exception($"The entry point not found in the catalog `{catalog.LocatorId}`");

            var prefab = await Addressables.LoadAssetAsync<GameObject>(d.First()).Task;
            var entryPointGameObject = UnityEngine.Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(entryPointGameObject, scene);
            var entryPoint = entryPointGameObject.GetComponent<IMiniGameEntryPoint>();

            if (entryPoint == null)
                throw new Exception($"The entry point not found in the game object `{entryPointGameObject}`");

            return entryPoint;
        }
    }

    public class CompositeHandle
    {
        protected bool IsDisposed;
        private readonly List<object> _handles = new();

        public CompositeHandle() : this(Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(AsyncOperationHandle handle) : this(new[] { handle })
        {
        }

        public CompositeHandle(params AsyncOperationHandle[] handles)
        {
            Add(handles);
        }

        public void Add(params AsyncOperationHandle[] handles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));

            if (handles == null)
                throw new ArgumentNullException(nameof(handles));

            foreach (var handle in handles)
                if (handle.IsValid())
                    _handles.Add(handle);
        }

        public void Add<T>(params AsyncOperationHandle<T>[] handles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));


            if (handles == null)
                throw new ArgumentNullException(nameof(handles));

            foreach (var handle in handles)
                if (handle.IsValid())
                    _handles.Add(handle);
        }

        public void Add(params CompositeHandle[] compositeHandles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));

            if (compositeHandles == null)
                throw new ArgumentNullException(nameof(compositeHandles));

            foreach (var compositeHandle in compositeHandles)
                _handles.Add(compositeHandle);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            for (int i = _handles.Count - 1; i >= 0; i--)
            {
                switch (_handles[i])
                {
                    case AsyncOperationHandle handle when handle.IsValid():
                        Addressables.Release(handle);
                        break;
                    case AsyncOperationHandle<object> genericHandle when genericHandle.IsValid():
                        Addressables.Release(genericHandle);
                        break;
                    case CompositeHandle composite:
                        composite.Dispose();
                        break;
                }
            }

            _handles.Clear();
        }
    }

    public class CompositeHandle<T> : CompositeHandle
    {
        public T Value { get; set; }

        public CompositeHandle() : this(default, Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(params AsyncOperationHandle[] handles) : this(default, handles)
        {
        }

        public CompositeHandle(AsyncOperationHandle handle) : this(default, new[] { handle })
        {
        }

        public CompositeHandle(T value) : this(value, Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(T value, params AsyncOperationHandle[] handles) : base(handles)
        {
            Value = value;
        }

        public void SetValue(T value)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle<T>));

            Value = value;
        }

        public new void Dispose()
        {
            Value = default;
            base.Dispose();
        }
    }
}