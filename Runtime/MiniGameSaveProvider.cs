using System;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class MiniGameSaveProvider : ISaveProvider
    {
        private readonly ISaveProvider _innerSaveProvider;
        private readonly Func<string, string> _keyDecorator;

        public MiniGameSaveProvider(ISaveProvider innerSaveProvider, Func<string, string> keyDecorator)
        {
            _innerSaveProvider = innerSaveProvider;
            _keyDecorator = keyDecorator;
        }

        public void SaveData(string key, object data)
        {
            var modifiedKey = _keyDecorator(key);
            _innerSaveProvider.SaveData(modifiedKey, data);
        }

        public int GetInt(string key, int defaultValue)
        {
            var modifiedKey = _keyDecorator(key);
            return _innerSaveProvider.GetInt(modifiedKey, defaultValue);
        }

        public float GetFloat(string key, float defaultValue)
        {
            var modifiedKey = _keyDecorator(key);
            return _innerSaveProvider.GetFloat(modifiedKey, defaultValue);
        }

        public string GetFloat(string key, string defaultValue)
        {
            var modifiedKey = _keyDecorator(key);
            return _innerSaveProvider.GetFloat(modifiedKey, defaultValue);
        }
    }
}