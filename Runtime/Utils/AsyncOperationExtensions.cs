using System.Threading.Tasks;
using UnityEngine;

namespace com.appidea.MiniGamePlatform.Core
{
    public static class AsyncOperationExtensions
    {
        public static Task AsTask(this AsyncOperation operation)
        {
            var tcs = new TaskCompletionSource<bool>();

            operation.completed += _ => tcs.SetResult(true);

            return tcs.Task;
        }
    }
}