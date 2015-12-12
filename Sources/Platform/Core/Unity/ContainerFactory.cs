using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.ServiceLocation;
using Microsoft.Practices.Unity;
using Microsoft.Practices.Unity.Configuration;
using NLog.Internal;

namespace GoodAI.Platform.Core.Unity
{
    /// <summary>
    /// The application-level factory for Unity container management.
    /// </summary>
    public static class ContainerFactory
    {
        /// <summary>
        /// Configures a default Unity container from the application configuration file (if any exists) and returns the <see cref="UnityServiceLocator"/> instance.
        /// </summary>
        /// <returns>
        /// The configured service locator instance. 
        /// </returns>
        public static IServiceLocator Configure()
        {
            return Configure(new UnityContainer());
        }

        /// <summary>
        /// Configures a default Unity container from the application configuration file (if any exists) and returns the <see cref="UnityServiceLocator"/> instance.
        /// </summary>
        /// <param name="container">
        /// A unity container with default settings. 
        /// </param>
        /// <returns>
        /// The configured service locator instance. 
        /// </returns>
        public static IServiceLocator Configure(IUnityContainer container)
        {
            var section = System.Configuration.ConfigurationManager.GetSection("unity") as UnityConfigurationSection;
            if (section != null)
            {
                section.Configure(container);
            }

            var unityServiceLocator = new UnityServiceLocator(container);
            ServiceLocator.SetLocatorProvider(() => unityServiceLocator);

            return unityServiceLocator;
        }
    }
}
