using System;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class MiniGameRunningBehaviour : IMiniGameRunningBehaviour
    {
        public event Action<MiniGameState> StateChanged;
        public event Action<Exception> ExceptionHappened;
        public event Action<IMessage> MessageReceived;
        public MiniGameState State { get; private set; } = MiniGameState.None;
        public string Name { get; }
        public Exception LastException { get; private set; }
        public IMessage LastMessage { get; private set; }
        public RunningMiniGameStateData StateData { get; private set; }
        public CancellationToken CancellationToken { get; }
        public Task ActiveTask { get; private set; }
        public IMiniGameLoadingProgressHandler LoadingProgressHandler => LoadingProgressHandlerImpl;
        public IMiniGameLoadingProgressHandler UnloadingProgressHandler => UnloadingProgressHandlerImpl;
        public readonly TaskCompletionSource<object> TaskCompletionSource;

        public readonly MiniGameLoadingProgressHandler LoadingProgressHandlerImpl;
        public readonly MiniGameLoadingProgressHandler UnloadingProgressHandlerImpl;

        public MiniGameRunningBehaviour(string name, CancellationToken cancellationToken,
            TaskCompletionSource<object> taskCompletionSource,
            MiniGameLoadingProgressHandler loadingProgressHandler,
            MiniGameLoadingProgressHandler unloadingProgressHandler)
        {
            Name = name;
            CancellationToken = cancellationToken;
            TaskCompletionSource = taskCompletionSource;
            LoadingProgressHandlerImpl = loadingProgressHandler;
            UnloadingProgressHandlerImpl = unloadingProgressHandler;
        }

        public void SetTask(Task task)
        {
            ActiveTask = task;
        }

        public void SetState(MiniGameState state)
        {
            if (State == state)
                throw new Exception("Cannot set state twice");

            State = state;
            StateChanged?.Invoke(State);
        }

        public void SetException(Exception exception)
        {
            LastException = exception;
            ExceptionHappened?.Invoke(exception);
        }

        public void SetMessage(IMessage message)
        {
            LastMessage = message;
            MessageReceived?.Invoke(message);
        }

        public void SetStateData(RunningMiniGameStateData stateData)
        {
            StateData = stateData;
        }
    }
}