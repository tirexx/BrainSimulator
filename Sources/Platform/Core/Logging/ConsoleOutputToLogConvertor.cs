using System.IO;
using System.Text;

namespace GoodAI.Platform.Core.Logging
{
    public class ConsoleOutputToLogConvertor : TextWriter
    {
        private const string LoggerName = "Console";

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }

        public override void WriteLine(string message)
        {
            Log.Info(LoggerName, message);
        }

        public override void Write(string value)
        {
            Log.Info(LoggerName, value);
        }

        public override void Write(char value)
        {
            Log.Info(LoggerName, value.ToString());
        }
    }
}