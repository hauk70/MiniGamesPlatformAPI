using UnityEngine.Rendering;

namespace com.appidea.MiniGamePlatform.Core
{
    public interface IRenderPipelineManager
    {
        void OverrideRenderPipeline(RenderPipelineAsset newPipelineAsset);
        void OverridePostProcessing(VolumeProfile newProfile);
        void RestoreOriginalSettings();
        bool AreSettingsOverridden();
    }
}