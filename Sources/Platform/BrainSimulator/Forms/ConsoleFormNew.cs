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
        public ConsoleFormNew()
        {
            InitializeComponent();

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
                LoggingConfigurator.ChangeMinLogLevel(logLevel);
                Settings.Default.LogLevel = logLevel.Ordinal;
            }
        }
    }
}