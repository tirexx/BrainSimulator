using System.ComponentModel;
using System.Drawing;
using NLog;
using NLog.Conditions;
using NLog.Config;

namespace GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger
{
    [NLogConfigurationItem]
    public class RichTextBoxRowColoringRule
    {
        static RichTextBoxRowColoringRule()
        {
            Default = new RichTextBoxRowColoringRule();
        }

        public RichTextBoxRowColoringRule()
            : this(null, "Empty", "Empty", FontStyle.Regular)
        {
        }

        public RichTextBoxRowColoringRule(/*string condition, */LogLevel level, string fontColor, string backColor, FontStyle fontStyle)
        {
            //Condition = condition;
            Level = level;
            FontColor = fontColor;
            BackgroundColor = backColor;
            Style = fontStyle;
        }

        public RichTextBoxRowColoringRule(/*string condition, */LogLevel level, string fontColor, string backColor)
        {
            //Condition = condition;
            Level = level;
            FontColor = fontColor;
            BackgroundColor = backColor;
            Style = FontStyle.Regular;
        }

        public static RichTextBoxRowColoringRule Default { get; private set; }

        //[RequiredParameter]
        //public ConditionExpression Condition { get; set; }

        [RequiredParameter]
        public LogLevel Level { get; set; }

        [DefaultValue("Empty")]
        public string FontColor { get; set; }

        [DefaultValue("Empty")]
        public string BackgroundColor { get; set; }

        public FontStyle Style { get; set; }

        //public bool CheckCondition(LogEventInfo logEvent)
        //{
        //    return true.Equals(Condition.Evaluate(logEvent));
        //}

        public bool CheckLevel(LogEventInfo logEvent)
        {
            return logEvent.Level.CompareTo(Level) == 0;
        }
    }
}