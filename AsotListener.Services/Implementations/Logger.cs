﻿namespace AsotListener.Services.Implementations
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Contracts;
    using Windows.Foundation.Diagnostics;
    using Windows.Storage;
    public sealed class Logger : ILogger, IDisposable
    {
        LoggingSession loggingSession;
        LoggingChannel loggingChannel;

        public Logger()
        {
            loggingSession = new LoggingSession("ASOT Listener");
            loggingChannel = new LoggingChannel($"Logging channel for task {Task.CurrentId}");
            loggingSession.AddLoggingChannel(loggingChannel);
            LogMessage("Logger initialized.");
        }

        public void LogMessage(string message)
        {
            LogMessage(message, LoggingLevel.Verbose);
        }

        public void LogMessage(string message, LoggingLevel loggingLevel)
        {
            loggingChannel.LogMessage(message, loggingLevel);
#if DEBUG
            Debug.WriteLine($"{loggingLevel}: {message}");
#endif
        }

        public void SaveLogsToFile()
        {
            var saveTask = loggingSession.SaveToFileAsync(ApplicationData.Current.LocalFolder, "logging.etl").AsTask();
            saveTask.ConfigureAwait(false);
            saveTask.Wait();

        }

        public void Dispose()
        {
            loggingSession.RemoveLoggingChannel(loggingChannel);
            loggingChannel.Dispose();
            loggingSession.Dispose();
        }
    }
}
