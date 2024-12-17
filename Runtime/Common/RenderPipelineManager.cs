using System;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace com.appidea.MiniGamePlatform.Core
{
    public class RenderPipelineManager : IRenderPipelineManager
    {
        private RenderPipelineAsset _originalPipelineAsset;
        private VolumeProfile _originalPostProcessingProfile;
        private bool _isPipelineOverridden;
        private bool _isPostProcessingOverridden;

        public void OverrideRenderPipeline(RenderPipelineAsset newPipelineAsset)
        {
            if (_isPipelineOverridden)
                return;

            if (newPipelineAsset == null)
                throw new ArgumentNullException(nameof(newPipelineAsset), "New Render Pipeline Asset is null!");

            _originalPipelineAsset = GraphicsSettings.renderPipelineAsset;

            GraphicsSettings.renderPipelineAsset = newPipelineAsset;
            _isPipelineOverridden = true;
        }

        public void OverridePostProcessing(VolumeProfile newProfile)
        {
            if (_isPostProcessingOverridden)
                return;

            if (newProfile == null)
                throw new ArgumentNullException(nameof(newProfile), "New Post-Processing Profile is null!");

            var volume = Object.FindObjectOfType<Volume>();
            if (volume != null)
            {
                _originalPostProcessingProfile = volume.sharedProfile;
                volume.sharedProfile = newProfile;
                _isPostProcessingOverridden = true;
            }
        }

        public void RestoreOriginalSettings()
        {
            if (_isPipelineOverridden)
            {
                if (_originalPipelineAsset != null)
                {
                    GraphicsSettings.renderPipelineAsset = _originalPipelineAsset;
                    _originalPipelineAsset = null;
                }

                _isPipelineOverridden = false;
            }

            if (_isPostProcessingOverridden)
            {
                var volume = Object.FindObjectOfType<Volume>();
                if (volume != null)
                {
                    if (_originalPostProcessingProfile != null)
                    {
                        volume.sharedProfile = _originalPostProcessingProfile;
                        _originalPostProcessingProfile = null;
                    }
                }

                _isPostProcessingOverridden = false;
            }
        }

        public bool AreSettingsOverridden()
        {
            return _isPipelineOverridden || _isPostProcessingOverridden;
        }
    }
}