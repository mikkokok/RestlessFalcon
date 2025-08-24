using System.Diagnostics;
using System.Threading.Tasks;

namespace RestlessFalcon.Helpers.Impl
{
#pragma warning disable CA1416 // Validate platform compatibility
    public class Logger
    {
        private readonly string _source = "RestlessFalcon";
        private readonly string _log = "Application";

        public Logger()
        {
            if (!EventLog.SourceExists(_source))
                EventLog.CreateEventSource(_source, _log);
        }
        public void WriteWarningLog(string message)
        {
            _ = Task.Run(() => EventLog.WriteEntry(_source, message, EventLogEntryType.Warning));
        }
        public void WriteErrorLog(string message)
        {
            _ = Task.Run(() => EventLog.WriteEntry(_source, message, EventLogEntryType.Error));
        }
    }
}
