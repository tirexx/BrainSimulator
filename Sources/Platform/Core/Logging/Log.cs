using System;
using Microsoft.Practices.ServiceLocation;

namespace GoodAI.Platform.Core.Logging
{
    public class Log
    {
        private static readonly Lazy<ILogManager> LazyLogManager =
            new Lazy<ILogManager>(() => ServiceLocator.Current.GetInstance<ILogManager>(), true);

        public static void Info(object typeOrNameForTyppedLogger, Exception ex)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Info(ex);
            }
        }

        public static void Info(object typeOrNameForTyppedLogger, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Info(format, args);
            }
        }

        public static void Info(object typeOrNameForTyppedLogger, Exception ex, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Info(ex, format, args);
            }
        }

        public static void Warn(object typeOrNameForTyppedLogger, Exception ex)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Warn(ex);
            }
        }

        public static void Warn(object typeOrNameForTyppedLogger, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Warn(format, args);
            }
        }

        public static void Warn(object typeOrNameForTyppedLogger, Exception ex, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Warn(ex, format, args);
            }
        }

        public static void Debug(object typeOrNameForTyppedLogger, Exception ex)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Debug(ex);
            }
        }

        public static void Debug(object typeOrNameForTyppedLogger, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Debug(format, args);
            }
        }

        public static void Debug(object typeOrNameForTyppedLogger, Exception ex, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Debug(ex, format, args);
            }
        }

        public static void Error(object typeOrNameForTyppedLogger, Exception ex)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Error(ex);
            }
        }

        public static void Error(object typeOrNameForTyppedLogger, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Error(format, args);
            }
        }

        public static void Error(object typeOrNameForTyppedLogger, Exception ex, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Error(ex, format, args);
            }
        }

        public static void Fatal(object typeOrNameForTyppedLogger, Exception ex)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Fatal(ex);
            }
        }

        public static void Fatal(object typeOrNameForTyppedLogger, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Fatal(format, args);
            }
        }

        public static void Fatal(object typeOrNameForTyppedLogger, Exception ex, string format, params object[] args)
        {
            var log = GetLog(typeOrNameForTyppedLogger);
            if (log != null)
            {
                log.Fatal(ex, format, args);
            }
        }

        private static ILog GetLog(object typeOrNameForTyppedLogger)
        {
            return LazyLogManager.Value.GetLog(typeOrNameForTyppedLogger);
        }
    }
}