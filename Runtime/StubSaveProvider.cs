using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class StubSaveProvider : ISaveProvider
    {
        public void SaveData(string key, object data)
        {
        }

        public int GetInt(string key, int defaultValue)
        {
            return defaultValue;
        }

        public float GetFloat(string key, float defaultValue)
        {
            return defaultValue;
        }

        public string GetFloat(string key, string defaultValue)
        {
            return defaultValue;
        }
    }
}