using System;
using System.Linq;
using GoodAI.BrainSimulator.Properties;
using GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger;
using GoodAI.Core.Utils;
using NLog;
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
                Enumerable.Range(0, 6).Select(x => LogLevel.FromOrdinal(x).Name).ToArray());
            logLevelStripComboBox.SelectedIndexChanged += logLevelStripComboBox_SelectedIndexChanged;
            logLevelStripComboBox.SelectedIndex = LogLevel.FromOrdinal(Settings.Default.LogLevel).Ordinal;
        }

        private void logLevelStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (logLevelStripComboBox.SelectedIndex >= 0)
            {
                var logLevel = LogLevel.FromOrdinal(logLevelStripComboBox.SelectedIndex);
                RichTextBoxNLogConfigurator.ChangeMinLogLevel(logLevel);
                Settings.Default.LogLevel = logLevel.Ordinal;
            }
        }
    }
}