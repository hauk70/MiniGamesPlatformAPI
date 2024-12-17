using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEngine.SceneManagement;

namespace com.appidea.MiniGamePlatform.Core
{
    public readonly struct RunningMiniGameStateData
    {
        public readonly Scene Scene;
        public readonly Scene PrevScene;
        public readonly IMiniGameEntryPoint EntryPoint;

        public RunningMiniGameStateData(Scene scene, Scene prevScene, IMiniGameEntryPoint entryPoint)
        {
            Scene = scene;
            PrevScene = prevScene;
            EntryPoint = entryPoint;
        }
    }
}