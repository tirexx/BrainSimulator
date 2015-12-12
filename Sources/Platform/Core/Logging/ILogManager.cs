namespace GoodAI.Platform.Core.Logging
{
    public interface ILogManager
    {
        ILog GetLog(object typeOrNameForTyppedLogger);
    }
}