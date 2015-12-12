using System;
using GoodAI.Platform.Core.Logging;

namespace GoodAI.Platform.Core.Unity
{
    public abstract class BaseBootstrapper
    {
        protected abstract IDisposable CreateAndSetupUnityContainer();

        protected virtual void ConfigureAfterContainerSetup()
        {
        }

        public IDisposable Start()
        {
            try
            {
                var unityContainer = CreateAndSetupUnityContainer();
                ConfigureAfterContainerSetup();
                return unityContainer;
            }
            catch (Exception ex)
            {
                Log.Error(typeof(BaseBootstrapper), ex);
                throw;
            }
        }
    }
}