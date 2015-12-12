namespace GoodAI.Platform.Core.Logging.MyLog
{
    public class MyLogLogManager : ILogManager
    {
        public ILog GetLog(object typeOrNameForTyppedLogger)
        {
            return new MyLogLogWrapper();
        }
    }
}