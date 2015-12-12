using System;

namespace GoodAI.Platform.Core.Logging.MyLog
{
    internal class MyLogLogWrapper : ILog
    {
        public void Debug(string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.DEBUG.WriteLine(format, args);
        }

        public void Debug(Exception ex)
        {
            GoodAI.Core.Utils.MyLog.DEBUG.WriteLine(ex);
        }

        public void Debug(Exception ex, string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.DEBUG.WriteLine(format, args);
            GoodAI.Core.Utils.MyLog.DEBUG.WriteLine(ex);
        }

        public void Error(string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(format, args);
        }

        public void Error(Exception ex)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(ex);
        }

        public void Error(Exception ex, string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(format, args);
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(ex);
        }

        public void Fatal(string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(format, args);
        }

        public void Fatal(Exception ex)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(ex);
        }

        public void Fatal(Exception ex, string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(format, args);
            GoodAI.Core.Utils.MyLog.ERROR.WriteLine(ex);
        }

        public void Info(string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.INFO.WriteLine(format, args);
        }

        public void Info(Exception ex)
        {
            GoodAI.Core.Utils.MyLog.INFO.WriteLine(ex);
        }

        public void Info(Exception ex, string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.INFO.WriteLine(format, args);
            GoodAI.Core.Utils.MyLog.INFO.WriteLine(ex);
        }

        public void Warn(string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.WARNING.WriteLine(format, args);
        }

        public void Warn(Exception ex)
        {
            GoodAI.Core.Utils.MyLog.WARNING.WriteLine(ex);
        }

        public void Warn(Exception ex, string format, params object[] args)
        {
            GoodAI.Core.Utils.MyLog.WARNING.WriteLine(format, args);
            GoodAI.Core.Utils.MyLog.WARNING.WriteLine(ex);
        }
    }
}