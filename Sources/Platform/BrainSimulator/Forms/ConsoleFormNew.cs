using System;
using System.Linq;
using GoodAI.BrainSimulator.Properties;
using GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger;
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
        }

        private void logLevelStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyLog.Level = (MyLogLevel) logLevelStripComboBox.SelectedIndex;
            Settings.Default.LogLevel = (int) MyLog.Level;
        }
    }
}