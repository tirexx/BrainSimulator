using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GoodAI.BrainSimulator.Utils.WpfRichTextBoxLogger;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace GoodAI.BrainSimulator.Helper
{
    [Target("RichTextBox")]
    public sealed class RichTextBoxNLogTarget : TargetWithLayout
    {
        private static readonly TypeConverter ColorConverter = new ColorConverter();

        private static readonly int ADDITIONAL_LINES_REMOVED_PER_CHECK = 50;
        private int _lineCount;

        static RichTextBoxNLogTarget()
        {
            var rules = new List<RichTextBoxRowColoringRule>
            {
                new RichTextBoxRowColoringRule("level == LogLevel.Fatal", "White", "Red",
                    FontStyle.Regular | FontStyle.Bold),
                new RichTextBoxRowColoringRule("level == LogLevel.Error", "Red", "Empty",
                    FontStyle.Italic | FontStyle.Bold),
                new RichTextBoxRowColoringRule("level == LogLevel.Warn", "Orange", "Empty"),
                new RichTextBoxRowColoringRule("level == LogLevel.Info", "Black", "Empty"),
                new RichTextBoxRowColoringRule("level == LogLevel.Debug", "Gray", "Empty"),
                new RichTextBoxRowColoringRule("level == LogLevel.Trace", "DarkGray", "Empty", FontStyle.Italic)
            };

            DefaultRowColoringRules = rules.AsReadOnly();
        }

        public RichTextBoxNLogTarget()
        {
            //WordColoringRules = new List<WpfRichTextBoxWordColoringRule>();
            RowColoringRules = new List<RichTextBoxRowColoringRule>();
            ToolWindow = true;
        }

        public static ReadOnlyCollection<RichTextBoxRowColoringRule> DefaultRowColoringRules { get; private set; }

        public string ControlName { get; set; }

        public string FormName { get; set; }

        [DefaultValue(false)]
        public bool UseDefaultRowColoringRules { get; set; }

        [ArrayParameter(typeof (RichTextBoxRowColoringRule), "row-coloring")]
        public IList<RichTextBoxRowColoringRule> RowColoringRules { get; private set; }

        //[ArrayParameter(typeof (WpfRichTextBoxWordColoringRule), "word-coloring")]
        //public IList<WpfRichTextBoxWordColoringRule> WordColoringRules { get; private set; }

        [DefaultValue(true)]
        public bool ToolWindow { get; set; }

        public bool ShowMinimized { get; set; }

        public int Width { get; set; } = 500;

        public int Height { get; set; } = 500;

        public int MaxLines { get; set; }

        public Form TargetForm { get; set; }

        public RichTextBox TargetRichTextBox { get; set; }

        protected override void InitializeTarget()
        {
        }

        protected override void Write(LogEventInfo logEvent)
        {
            RichTextBoxRowColoringRule matchingRule =
                RowColoringRules.FirstOrDefault(rr => rr.CheckCondition(logEvent));

            if (UseDefaultRowColoringRules && matchingRule == null)
            {
                foreach (var rr in DefaultRowColoringRules.Where(rr => rr.CheckCondition(logEvent)))
                {
                    matchingRule = rr;
                    break;
                }
            }

            if (matchingRule == null)
            {
                matchingRule = RichTextBoxRowColoringRule.Default;
            }

            var logMessage = Layout.Render(logEvent);

            TargetRichTextBox.Invoke((MethodInvoker) delegate { SendTheMessageToRichTextBox(logMessage, matchingRule); });
        }


        private static Color GetColorFromString(string color)
        {
            if (color == "Empty")
            {
                color = "White";
            }

            return (Color) ColorConverter.ConvertFromString(color);
        }

        private void SendTheMessageToRichTextBox(string logMessage, RichTextBoxRowColoringRule rule)
        {
            var tb = TargetRichTextBox;

            tb.SelectionStart = tb.Text.Length;
            tb.SelectionLength = 0;
            tb.SelectionColor = GetColorFromString(rule.FontColor);
            tb.SelectionBackColor = GetColorFromString(rule.BackgroundColor);

            tb.AppendText(logMessage + "\n");

            if (tb.Lines.Length > MaxLines)
            {
                tb.Lines = tb.Lines.Skip(
                    tb.Lines.Length - MaxLines + ADDITIONAL_LINES_REMOVED_PER_CHECK
                    ).ToArray();
            }

            tb.SelectionStart = tb.Text.Length;
            tb.ScrollToCaret();
        }

        private delegate void DelSendTheMessageToRichTextBox(string logMessage, RichTextBoxRowColoringRule rule);

        private delegate void FormCloseDelegate();
    }
}