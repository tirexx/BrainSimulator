using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace GoodAI.Platform.Core.Logging.NLog
{
    public class NLogLogManager : ILogManager
    {
        public ILog GetLog(object typeOrNameForTyppedLogger)
        {
            if (typeOrNameForTyppedLogger == null)
            {
                typeOrNameForTyppedLogger = "NULL";
            }

            var type = typeOrNameForTyppedLogger as Type;

            var log = type != null
                          ? LogManager.GetLogger(type.FullName, type)
                          : LogManager.GetLogger(typeOrNameForTyppedLogger.ToString());
            
            return new NLogLogWrapper(log);
        }
    }
}
