using System;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    [Serializable]
    public class MiniGameBehaviourConfig
    {
        public readonly MiniGameConfig Config;
        public MiniGameLoadType LoadType;

        public MiniGameBehaviourConfig(MiniGameConfig config, MiniGameLoadType loadType)
        {
            Config = config;
            LoadType = loadType;
        }
    }
}