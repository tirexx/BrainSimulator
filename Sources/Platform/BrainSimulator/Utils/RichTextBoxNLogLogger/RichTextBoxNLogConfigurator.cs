using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;

namespace GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger
{
    public static class RichTextBoxNLogConfigurator
    {
        private static LoggingRule _loggingRule;

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
                UseDefaultRowColoringRules = true,
            };
            var asyncWrapper = new AsyncTargetWrapper { Name = "RichTextAsync", WrappedTarget = target };

            LogManager.Configuration.AddTarget(asyncWrapper.Name, asyncWrapper);
            _loggingRule = new LoggingRule("*", LogLevel.FromString("Info"), asyncWrapper);
            LogManager.Configuration.LoggingRules.Insert(0,
                _loggingRule);
            LogManager.ReconfigExistingLoggers();
        }
    }
}