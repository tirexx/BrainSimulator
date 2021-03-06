﻿using System;
using System.Windows.Forms;
using GoodAI.BrainSimulator.Forms;
using GoodAI.BrainSimulator.Utils.RichTextBoxNLogLogger;

namespace GoodAI.BrainSimulator
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            new Bootstrapper().Start();
            LoggingConfigurator.ConfigureRichTextTarget("textBox", "ConsoleFormNew");
            LoggingConfigurator.ConfigureConsoleOutputToLogRedirection();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}