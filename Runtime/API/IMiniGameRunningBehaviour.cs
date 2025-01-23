using System;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IMiniGameRunningBehaviour
    {
        event Action<MiniGameState> StateChanged;
        event Action<Exception> ExceptionHappened;
        event Action<IMessage> MessageReceived;
        MiniGameState State { get; }
        string Name { get; }
        Exception LastException { get; }
        IMessage LastMessage { get; }
        CancellationToken CancellationToken { get; }
        Task ActiveTask { get; }
        RunningMiniGameStateData StateData { get; }
        IMiniGameLoadingProgressHandler LoadingProgressHandler { get; }
        IMiniGameLoadingProgressHandler UnloadingProgressHandler { get; }
    }
}