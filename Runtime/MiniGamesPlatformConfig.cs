using System.Collections.Generic;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core
{
    [CreateAssetMenu(menuName = "Mini game platform/Core platform config", fileName = "MiniGamesPlatformConfig", order = 0)]
    public class MiniGamesPlatformConfig : ScriptableObject
    {
        [SerializeField]
        public List<MiniGameBehaviourConfig> MiniGameConfigs = new List<MiniGameBehaviourConfig>();
    }
}