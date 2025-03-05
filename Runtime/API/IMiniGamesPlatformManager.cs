using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IMiniGamesPlatformManager
    {
        event Action ReadyToRun;
        bool IsReadyToRun { get; }
        
        IReadOnlyList<string> MiniGameNames { get; }
        IMiniGameRunningBehaviour MiniGameRunningBehaviour { get; }

        // Task<bool> IsMiniGameCacheReady(MiniGameBehaviourConfig miniGameBehaviourConfig);
        // Task<bool> PreloadGame(MiniGameBehaviourConfig miniGameBehaviourConfig);

        IMiniGameRunningBehaviour LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken,
            GameOverScreenData gameOverScreenData = null);
    }
}