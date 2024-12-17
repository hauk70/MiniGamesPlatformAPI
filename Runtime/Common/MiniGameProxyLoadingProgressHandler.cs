using System;
using System.Collections.Generic;
using com.appidea.MiniGamePlatform.CommunicationAPI;

namespace com.appidea.MiniGamePlatform.Core
{
    public class MiniGameProxyLoadingProgressHandler : IMiniGameLoadingProgressHandler
    {
        public float Progress => _miniGameLoadingProgressHandler?.Progress ?? 0f;

        public event Action<float> ProgressChanged
        {
            add
            {
                _subscribers.Add(value);

                if (_miniGameLoadingProgressHandler != null)
                    _miniGameLoadingProgressHandler.ProgressChanged += value;
            }
            remove
            {
                _subscribers.Remove(value);

                if (_miniGameLoadingProgressHandler != null)
                    _miniGameLoadingProgressHandler.ProgressChanged -= value;
            }
        }

        private readonly List<Action<float>> _subscribers = new List<Action<float>>();

        private IMiniGameLoadingProgressHandler _miniGameLoadingProgressHandler;

        public void SetHandler(IMiniGameLoadingProgressHandler handler)
        {
            if (_miniGameLoadingProgressHandler != null)
            {
                foreach (var subscriber in _subscribers)
                    _miniGameLoadingProgressHandler.ProgressChanged -= subscriber;
            }

            _miniGameLoadingProgressHandler = handler;
            foreach (var subscriber in _subscribers)
            {
                _miniGameLoadingProgressHandler.ProgressChanged += subscriber;
            }
        }
    }
}