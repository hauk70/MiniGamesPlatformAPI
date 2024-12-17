using System;
using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core
{
    [Serializable]
    public class MiniGameBehaviourConfig
    {
        public MiniGameConfig Config => config;
        [SerializeField] private MiniGameConfig config;
        public MiniGameLoadType LoadType;

        public MiniGameBehaviourConfig(MiniGameConfig config, MiniGameLoadType loadType)
        {
            this.config = config;
            LoadType = loadType;
        }
    }
}