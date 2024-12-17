using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.appidea.MiniGamePlatform.Core
{
    public class MiniGameLogger : ILogger
    {
        public ILogHandler logHandler
        {
            get => _innerLogger.logHandler;
            set => _innerLogger.logHandler = value;
        }

        public bool logEnabled
        {
            get => _innerLogger.logEnabled;
            set => _innerLogger.logEnabled = value;
        }

        public LogType filterLogType
        {
            get => _innerLogger.filterLogType;
            set => _innerLogger.filterLogType = value;
        }

        private readonly ILogger _innerLogger;
        private readonly Func<string, string> _messageDecorator;

        public MiniGameLogger(ILogger innerLogger, Func<string, string> messageDecorator)
        {
            _innerLogger = innerLogger;
            _messageDecorator = messageDecorator;
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
            return _innerLogger.IsLogTypeAllowed(logType);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            _innerLogger.LogFormat(logType, context, _messageDecorator(format), args);
        }

        public void LogException(Exception exception, Object context)
        {
            _innerLogger.LogException(exception, context);
        }

        public void Log(LogType logType, object message)
        {
            _innerLogger.Log(logType, _messageDecorator(message.ToString()));
        }

        public void Log(LogType logType, object message, Object context)
        {
            _innerLogger.Log(logType, (object)_messageDecorator(message.ToString()), context);
        }

        public void Log(LogType logType, string tag, object message)
        {
            _innerLogger.Log(logType, tag, _messageDecorator(message.ToString()));
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            _innerLogger.Log(logType, tag, _messageDecorator(message.ToString()), context);
        }

        public void Log(object message)
        {
            _innerLogger.Log(_messageDecorator(message.ToString()));
        }

        public void Log(string tag, object message)
        {
            _innerLogger.Log(tag, _messageDecorator(message.ToString()));
        }

        public void Log(string tag, object message, Object context)
        {
            _innerLogger.Log(tag, _messageDecorator(message.ToString()), context);
        }

        public void LogWarning(string tag, object message)
        {
            _innerLogger.LogWarning(tag, _messageDecorator(message.ToString()));
        }

        public void LogWarning(string tag, object message, Object context)
        {
            _innerLogger.LogWarning(tag, _messageDecorator(message.ToString()), context);
        }

        public void LogError(string tag, object message)
        {
            _innerLogger.LogError(tag, _messageDecorator(message.ToString()));
        }

        public void LogError(string tag, object message, Object context)
        {
            _innerLogger.LogError(tag, _messageDecorator(message.ToString()), context);
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            _innerLogger.LogFormat(logType, _messageDecorator(format), args);
        }

        public void LogException(Exception exception)
        {
            _innerLogger.LogException(exception);
        }
    }
}