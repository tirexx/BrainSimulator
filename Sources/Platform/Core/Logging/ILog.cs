using System;

namespace GoodAI.Platform.Core.Logging
{
    public interface ILog
    {
        void Debug(Exception ex);

        void Debug(string format, params object[] args);

        void Debug(Exception ex, string format, params object[] args);

        void Error(Exception ex);

        void Error(string format, params object[] args);

        void Error(Exception ex, string format, params object[] args);

        void Fatal(Exception ex);

        void Fatal(string format, params object[] args);

        void Fatal(Exception ex, string format, params object[] args);

        void Info(Exception ex);

        void Info(string format, params object[] args);

        void Info(Exception ex, string format, params object[] args);

        void Warn(Exception ex);

        void Warn(string format, params object[] args);

        void Warn(Exception ex, string format, params object[] args);
    }
}