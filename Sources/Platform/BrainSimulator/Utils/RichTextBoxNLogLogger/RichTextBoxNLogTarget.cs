using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger
{
    [Target("RichTextBox")]
    public sealed class RichTextBoxNLogTarget : TargetWithLayout
    {
        private static readonly TypeConverter ColorConverter = new ColorConverter();

        private static readonly int ADDITIONAL_LINES_REMOVED_PER_CHECK = 50;
        private readonly object _controlSync = new object();

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

        [DefaultValue(true)]
        public bool ToolWindow { get; set; }

        public int MaxLines { get; set; }

        private Form TargetForm { get; set; }

        private RichTextBox TargetRichTextBox { get; set; }

        protected override void InitializeTarget()
        {
        }

        protected override void Write(LogEventInfo logEvent)
        {
            EnsureControl();

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

        private void EnsureControl()
        {
            FindControl();
            while (TargetForm == null || TargetRichTextBox == null)
            {
                Thread.Sleep(100);
                FindControl();
            }
        }

        private void FindControl()
        {
            if (TargetForm == null)
            {
                lock (_controlSync)
                {
                    if (TargetForm == null)
                    {
                        TargetForm = Application.OpenForms[FormName];
                    }
                }
            }

            if (TargetForm != null)
            {
                if (TargetRichTextBox == null)
                {
                    lock (_controlSync)
                    {
                        if (TargetRichTextBox == null)
                        {
                            TargetRichTextBox = TargetForm.Controls[ControlName] as RichTextBox;
                        }
                    }
                }
            }
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