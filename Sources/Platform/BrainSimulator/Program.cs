using System;
using System.Windows.Forms;
using GoodAI.BrainSimulator.Forms;

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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}