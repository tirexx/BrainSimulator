using System;
using GoodAI.Platform.Core.Logging;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger
{
    public static class LoggingConfigurator
    {
        private static LoggingRule _loggingRule;
        private static Target _asyncWrapper;

        public static void ChangeMinLogLevel(LogLevel logLevel)
        {
            LogManager.Configuration.LoggingRules.Remove(_loggingRule);
            _loggingRule = new LoggingRule("*", logLevel, _asyncWrapper);
            LogManager.Configuration.LoggingRules.Insert(0, _loggingRule);
            LogManager.ReconfigExistingLoggers();
        }

        public static void ConfigureConsoleOutputToLogRedirection()
        {
            Console.SetOut(new ConsoleOutputToLogConvertor());
        }

        public static void ConfigureRichTextTarget(string controlName, string formName)
        {
            var target = new RichTextBoxNLogTarget
            {
                Name = "RichText",
                Layout =
                    "[${longdate:useUTC=false}] [${level:uppercase=true}] ${logger} :: ${message} ${exception:innerFormat=tostring:maxInnerExceptionLevel=10:separator=,:format=tostring}",
                ControlName = controlName,
                FormName = formName,
                MaxLines = 1000,
                UseDefaultRowColoringRules = true
            };

            _asyncWrapper = new AsyncTargetWrapper {Name = "RichTextAsync", WrappedTarget = target};

            LogManager.Configuration.AddTarget(_asyncWrapper.Name, _asyncWrapper);
            _loggingRule = new LoggingRule("*", LogLevel.FromString("Info"), _asyncWrapper);
            LogManager.Configuration.LoggingRules.Insert(0, _loggingRule);
            LogManager.ReconfigExistingLoggers();
        }
    }
}