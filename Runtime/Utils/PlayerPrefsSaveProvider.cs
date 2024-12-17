using com.appidea.MiniGamePlatform.CommunicationAPI;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core
{
    public class PlayerPrefsSaveProvider : ISaveProvider
    {
        public void SaveData(string key, object data)
        {
            if (data is int intData)
                PlayerPrefs.SetInt(key, intData);
            else if (data is float floatData)
                PlayerPrefs.SetFloat(key, floatData);
            else if (data is string stringData)
                PlayerPrefs.SetString(key, stringData);
            else
                Debug.LogWarning("Unsupported data type for saving in PlayerPrefs.");

            PlayerPrefs.Save();
        }

        public int GetInt(string key, int defaultValue)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public float GetFloat(string key, float defaultValue)
        {
            return PlayerPrefs.GetFloat(key, defaultValue);
        }

        public string GetString(string key, string defaultValue)
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }
    }
}