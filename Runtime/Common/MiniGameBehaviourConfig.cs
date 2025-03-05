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
        public CatalogLoadType LoadType;

        public MiniGameBehaviourConfig(MiniGameConfig config, CatalogLoadType loadType)
        {
            this.config = config;
            LoadType = loadType;
        }
    }
}