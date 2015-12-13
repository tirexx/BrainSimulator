using System;
using System.Linq;
using GoodAI.BrainSimulator.Helper;
using GoodAI.BrainSimulator.Properties;
using GoodAI.Core.Utils;
using NLog;
using NLog.Config;
using NLog.Targets.Wrappers;
using WeifenLuo.WinFormsUI.Docking;

namespace GoodAI.BrainSimulator.Forms
{
    public partial class ConsoleFormNew : DockContent
    {
        private MainForm m_mainForm;

        public ConsoleFormNew(MainForm mainForm)
        {
            InitializeComponent();
            m_mainForm = mainForm;

            MyLog.GrabConsole();

            logLevelStripComboBox.Items.AddRange(
                Enumerable.Range(0, 5).Select(x => LogLevel.FromOrdinal(x).Name).ToArray());
            logLevelStripComboBox.SelectedIndexChanged += logLevelStripComboBox_SelectedIndexChanged;
            logLevelStripComboBox.SelectedIndex = Settings.Default.LogLevel;

            ConfigureNlogRichTextTarget();
        }

        private void logLevelStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyLog.Level = (MyLogLevel) logLevelStripComboBox.SelectedIndex;
            Settings.Default.LogLevel = (int) MyLog.Level;
        }

        private void ConsoleFormNew_Load_1(object sender, EventArgs e)
        {
            //this.BeginInvoke((MethodInvoker)(ConfigureNlogRichTextTarget));
        }

        public void ConfigureNlogRichTextTarget()
        {
            var target = new RichTextBoxNLogTarget
            {
                Name = "RichText",
                Layout =
                    "[${longdate:useUTC=false}] [${level:uppercase=true}] ${logger} :: ${message} ${exception:innerFormat=tostring:maxInnerExceptionLevel=10:separator=,:format=tostring}",
                ControlName = textBox.Name,
                FormName = GetType().Name,
                MaxLines = 10,
                UseDefaultRowColoringRules = true,
                TargetRichTextBox = textBox,
                TargetForm = this
            };
            var asyncWrapper = new AsyncTargetWrapper {Name = "RichTextAsync", WrappedTarget = target};

            LogManager.Configuration.AddTarget(asyncWrapper.Name, asyncWrapper);
            LogManager.Configuration.LoggingRules.Insert(0,
                new LoggingRule("*", LogLevel.FromString("Info"), asyncWrapper));
            LogManager.ReconfigExistingLoggers();
        }
    }
}