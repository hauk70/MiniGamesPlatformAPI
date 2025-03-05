using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace com.appidea.MiniGamePlatform.Core
{
    public class BuiltInBundlesLocator : IResourceLocator
    {
        private readonly IResourceLocator _originalLocator;
        private readonly Dictionary<object, IList<IResourceLocation>> _remappedLocations = new();

        public IEnumerable<object> Keys => _remappedLocations.Keys;
        public string LocatorId => _originalLocator.LocatorId;
        public Dictionary<object, IList<IResourceLocation>> Locations => _remappedLocations;

        public BuiltInBundlesLocator(IResourceLocator originalLocator, string originalCatalogUrl)
        {
            _originalLocator = originalLocator;

            foreach (var key in originalLocator.Keys)
            {
                if (!originalLocator.Locate(key, null, out var locations))
                    continue;

                var newLocations = locations
                    .Select(loc => RemapLocation(loc, originalCatalogUrl))
                    .ToList();
                _remappedLocations[key] = newLocations;
            }
        }

        public bool Locate(object key, Type type, out IList<IResourceLocation> locations)
        {
            return _remappedLocations.TryGetValue(key, out locations);
        }

        private IResourceLocation RemapLocation(IResourceLocation location, string originalCatalogUrl)
        {
            if (location.ResourceType == typeof(IAssetBundleResource) && location.InternalId.StartsWith("http"))
            {
                // 🔄 Меняем URL на локальный путь
                var dependencies = location.Dependencies ?? new List<IResourceLocation>();
                var localBundlePath =
                    BuiltInBundlesManager.GetBundleFilePath(BuiltInBundlesManager.GetCatalogGuid(originalCatalogUrl),
                        location.InternalId);
                return new ResourceLocationBase(location.PrimaryKey, localBundlePath, location.ProviderId,
                    location.ResourceType, dependencies.ToArray());
            }

            return location;
        }
    }
}