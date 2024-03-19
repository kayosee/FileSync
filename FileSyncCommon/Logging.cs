using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon
{
    public class Logging
    {
        public delegate void InformationHandler(string message);
        public delegate void ErrorHandler(string message, Exception e);
        public event InformationHandler? OnInformation;
        public event ErrorHandler? OnError;
        public Logging() { }
        public void LogInformation(string message)
        {
            if (OnInformation != null) OnInformation(message);
            Log.Information(message);
        }
        public void LogError(string message, Exception? e = null)
        {
            if (OnError != null) OnError(message, e);
            Log.Error(message, e);
        }
    }

}
