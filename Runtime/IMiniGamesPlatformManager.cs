using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IMiniGamesPlatformManager
    {
        IReadOnlyList<string> MiniGameNames { get; }
        bool IsMiniGameRunning { get; }
        string CurrentMiniGameName { get; }
        IMiniGameLoadingProgressHandler MiniGameLoadingProgressHandler { get; } 
        
        Task<bool> IsMiniGameCacheReady(string miniGameName);
        Task<bool> PreloadGame(string miniGameName, CancellationToken cancellationToken);

        Task LoadAndRunMiniGame(string miniGameName, CancellationToken cancellationToken);
    }
}