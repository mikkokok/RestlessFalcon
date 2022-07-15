using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace RestlessFalcon.Helpers.Impl
{



    public class Logger
    {
        private string _source = "RestlessFalcon";
        private string _log = "Application";

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
