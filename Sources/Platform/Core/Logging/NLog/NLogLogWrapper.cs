using System;
using NLog;

namespace GoodAI.Platform.Core.Logging.NLog
{
    internal class NLogLogWrapper : ILog
    {
        private readonly Logger log;

        public NLogLogWrapper(Logger log)
        {
            this.log = log;
        }

        public void Debug(string format, params object[] args)
        {
            log.Debug(format, args);
        }

        public void Debug(Exception ex)
        {
            log.Debug(ex);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            log.Debug(ex, format, args);
        }

        public void Error(string format, params object[] args)
        {
            log.Error(format, args);
        }

        public void Error(Exception ex)
        {
            log.Error(ex);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            log.Error(ex, format, args);
        }

        public void Fatal(string format, params object[] args)
        {
            log.Fatal(format, args);
        }

        public void Fatal(Exception ex)
        {
            log.Fatal(ex);
        }

        public void Fatal(Exception ex, string format, params object[] args)
        {
            log.Fatal(ex, format, args);
        }

        public void Info(string format, params object[] args)
        {
            log.Info(format, args);
        }

        public void Info(Exception ex)
        {
            log.Info(ex);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            log.Info(ex, format, args);
        }

        public void Warn(string format, params object[] args)
        {
            log.Warn(format, args);
        }

        public void Warn(Exception ex)
        {
            log.Warn(ex);
        }

        public void Warn(Exception ex, string format, params object[] args)
        {
            log.Warn(ex, format, args);
        }
    }
}