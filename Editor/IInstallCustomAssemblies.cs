using System.Collections.Generic;
using System.Reflection;

namespace com.appidea.MiniGamePlatform.Core.Editor
{
    public interface IInstallCustomAssemblies
    {
        IEnumerable<Assembly> GetAssemblies();
    }
}