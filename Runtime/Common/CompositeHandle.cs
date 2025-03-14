using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace com.appidea.MiniGamePlatform.Core
{
    public class CompositeHandle
    {
        protected bool IsDisposed;
        private readonly List<object> _handles = new();

        public CompositeHandle() : this(Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(AsyncOperationHandle handle) : this(new[] { handle })
        {
        }

        public CompositeHandle(params AsyncOperationHandle[] handles)
        {
            Add(handles);
        }

        public void Add(params AsyncOperationHandle[] handles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));

            if (handles == null)
                throw new ArgumentNullException(nameof(handles));

            foreach (var handle in handles)
                if (handle.IsValid())
                    _handles.Add(handle);
        }

        public void Add<T>(params AsyncOperationHandle<T>[] handles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));


            if (handles == null)
                throw new ArgumentNullException(nameof(handles));

            foreach (var handle in handles)
                if (handle.IsValid())
                    _handles.Add(handle);
        }

        public void Add(params CompositeHandle[] compositeHandles)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle));

            if (compositeHandles == null)
                throw new ArgumentNullException(nameof(compositeHandles));

            foreach (var compositeHandle in compositeHandles)
                _handles.Add(compositeHandle);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            for (int i = _handles.Count - 1; i >= 0; i--)
            {
                switch (_handles[i])
                {
                    case AsyncOperationHandle handle when handle.IsValid():
                        Addressables.Release(handle);
                        break;
                    case AsyncOperationHandle<object> genericHandle when genericHandle.IsValid():
                        Addressables.Release(genericHandle);
                        break;
                    case CompositeHandle composite:
                        composite.Dispose();
                        break;
                }
            }

            _handles.Clear();
        }
    }

    public class CompositeHandle<T> : CompositeHandle
    {
        public T Value { get; set; }

        public CompositeHandle() : this(default, Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(params AsyncOperationHandle[] handles) : this(default, handles)
        {
        }

        public CompositeHandle(AsyncOperationHandle handle) : this(default, new[] { handle })
        {
        }

        public CompositeHandle(T value) : this(value, Array.Empty<AsyncOperationHandle>())
        {
        }

        public CompositeHandle(T value, params AsyncOperationHandle[] handles) : base(handles)
        {
            Value = value;
        }

        public void SetValue(T value)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(CompositeHandle<T>));

            Value = value;
        }

        public new void Dispose()
        {
            Value = default;
            base.Dispose();
        }
    }
}