using System;
using System.Collections.Generic;
using System.Linq;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public class LoadTypeResolver
    {
        public IReadOnlyDictionary<string, CatalogLoadType> ResolvedLoadTypes => _resolvedLoadTypes;

        private readonly Dictionary<string, List<string>> _dependencies;
        private readonly Dictionary<string, int> _builtinRefCount = new();
        private readonly Dictionary<string, CatalogLoadType> _resolvedLoadTypes = new();

        public LoadTypeResolver(Dictionary<string, List<string>> dependencies)
        {
            _dependencies = dependencies;
            BuildDependencyTree();
        }

        private void BuildDependencyTree()
        {
            foreach (var sharedUrl in _dependencies.Values.SelectMany(sharedUrls => sharedUrls))
                _builtinRefCount.TryAdd(sharedUrl, 0);
        }
        
        public void ResolveLoadTypes(Dictionary<string, CatalogLoadType> configLoadTypes)
        {
            _builtinRefCount.Clear();
            _resolvedLoadTypes.Clear();

            foreach (var (gameUrl, sharedUrls) in _dependencies)
            {
                if (configLoadTypes.TryGetValue(gameUrl, out var loadType) == false)
                    throw new ArgumentException($"Cannot find dependency for game {gameUrl}");

                foreach (var sharedUrl in sharedUrls)
                    if (loadType == CatalogLoadType.BuiltIn)
                        _builtinRefCount[sharedUrl]++;
            }

            foreach (var sharedUrl in _builtinRefCount.Keys)
            {
                _resolvedLoadTypes[sharedUrl] = _builtinRefCount[sharedUrl] > 0
                    ? CatalogLoadType.BuiltIn
                    : CatalogLoadType.RemoteLoad;
            }
        }

        public void UpdateMiniGameLoadTypeChanges(string changedGameUrl, CatalogLoadType oldLoadType,
            CatalogLoadType newLoadType)
        {
            if (_dependencies.TryGetValue(changedGameUrl, out var sharedUrls) == false)
                throw new ArgumentException($"Cannot find dependency for game {changedGameUrl}");

            foreach (var sharedUrl in sharedUrls)
            {
                if (oldLoadType == CatalogLoadType.BuiltIn)
                    _builtinRefCount[sharedUrl]--;

                if (newLoadType == CatalogLoadType.BuiltIn)
                    _builtinRefCount[sharedUrl]++;
            }

            foreach (var sharedUrl in sharedUrls)
            {
                _resolvedLoadTypes[sharedUrl] = _builtinRefCount[sharedUrl] > 0
                    ? CatalogLoadType.BuiltIn
                    : CatalogLoadType.RemoteLoad;
            }
        }
    }
}