using System;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IMiniGameRunningBehaviour
    {
        event Action<MiniGameState> StateChanged;
        MiniGameState State { get; }
        string Name { get; }
        CancellationToken CancellationToken { get; }
        Task ActiveTask { get; }
        RunningMiniGameStateData StateData { get; }
        IMiniGameLoadingProgressHandler LoadingProgressHandler { get; }
        IMiniGameLoadingProgressHandler UnloadingProgressHandler { get; }
    }
}