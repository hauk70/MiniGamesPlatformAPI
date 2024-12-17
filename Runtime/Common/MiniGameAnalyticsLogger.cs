using System;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class MiniGameAnalyticsLogger : IAnalyticsLogger
    {
        private readonly IAnalyticsLogger _innerAnalyticsLogger;
        private readonly Func<string, string> _keyDecorator;

        public MiniGameAnalyticsLogger(IAnalyticsLogger innerAnalyticsLogger,
            Func<string, string> keyDecorator)
        {
            _innerAnalyticsLogger = innerAnalyticsLogger;
            _keyDecorator = keyDecorator;
        }

        public void LogEvent(string key, string value)
        {
            var modifiedKey = _keyDecorator(key);
            _innerAnalyticsLogger.LogEvent(modifiedKey, value);
        }

        public void LogEvent(string key, int value)
        {
            var modifiedKey = _keyDecorator(key);
            _innerAnalyticsLogger.LogEvent(modifiedKey, value);
        }

        public void LogEvent(string key, float value)
        {
            var modifiedKey = _keyDecorator(key);
            _innerAnalyticsLogger.LogEvent(modifiedKey, value);
        }
    }
}