using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IMiniGamesPlatformManager
    {
        IReadOnlyList<string> MiniGameNames { get; }
        IMiniGameRunningBehaviour MiniGameRunningBehaviour { get; }

        Task<bool> IsMiniGameCacheReady(MiniGameBehaviourConfig miniGameBehaviourConfig);
        Task<bool> PreloadGame(MiniGameBehaviourConfig miniGameBehaviourConfig);

        IMiniGameRunningBehaviour LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken);
    }
}