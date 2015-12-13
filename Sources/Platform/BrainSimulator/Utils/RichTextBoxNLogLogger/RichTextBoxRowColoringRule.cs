using System.ComponentModel;
using System.Drawing;
using NLog;
using NLog.Conditions;
using NLog.Config;

namespace GoodAI.BrainSimulator.Utils.WpfRichTextBoxLogger
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

        public RichTextBoxRowColoringRule(string condition, string fontColor, string backColor, FontStyle fontStyle)
        {
            Condition = condition;
            FontColor = fontColor;
            BackgroundColor = backColor;
            Style = fontStyle;
        }

        public RichTextBoxRowColoringRule(string condition, string fontColor, string backColor)
        {
            Condition = condition;
            FontColor = fontColor;
            BackgroundColor = backColor;
            Style = FontStyle.Regular;
        }

        public static RichTextBoxRowColoringRule Default { get; private set; }

        [RequiredParameter]
        public ConditionExpression Condition { get; set; }

        [DefaultValue("Empty")]
        public string FontColor { get; set; }

        [DefaultValue("Empty")]
        public string BackgroundColor { get; set; }

        public FontStyle Style { get; set; }

        public bool CheckCondition(LogEventInfo logEvent)
        {
            return true.Equals(Condition.Evaluate(logEvent));
        }
    }
}