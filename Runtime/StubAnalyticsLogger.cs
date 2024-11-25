using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class StubAnalyticsLogger : IAnalyticsLogger
    {
        public void LogEvent(string key, string value)
        {
        }

        public void LogEvent(string key, int value)
        {
        }

        public void LogEvent(string key, float value)
        {
        }
    }
}