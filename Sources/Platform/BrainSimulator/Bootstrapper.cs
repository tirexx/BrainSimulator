using System;
using GoodAI.Platform.Core.Logging;
using GoodAI.Platform.Core.Logging.MyLog;
using GoodAI.Platform.Core.Unity;
using Microsoft.Practices.Unity;

namespace GoodAI.BrainSimulator
{
    public class Bootstrapper : BaseBootstrapper
    {
        protected override IDisposable CreateAndSetupUnityContainer()
        {
            var container = new UnityContainer();

            container.RegisterType<ILogManager, MyLogLogManager>(new InjectionConstructor());
            
            ContainerFactory.Configure(container);

            return container;
        }
    }
}