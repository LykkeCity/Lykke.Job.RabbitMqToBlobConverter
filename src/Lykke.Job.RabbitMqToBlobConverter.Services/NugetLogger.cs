using Common.Log;
using Lykke.Common.Log;
using NuGet.Common;
using System;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    internal class NugetLogger : ILogger
    {
        private readonly ILog _log;

        internal NugetLogger(ILogFactory log)
        {
            _log = log.CreateLog(this);
        }

        public void LogDebug(string data)
        {
            _log.Debug(data);
        }

        public void LogError(string data)
        {
            _log.Error(new InvalidOperationException(data));
        }

        public void LogErrorSummary(string data)
        {
            _log.Error(new InvalidOperationException(data));
        }

        public void LogInformation(string data)
        {
            _log.Info(data);
        }

        public void LogInformationSummary(string data)
        {
            _log.Info(data);
        }

        public void LogMinimal(string data)
        {
            _log.Info(data);
        }

        public void LogVerbose(string data)
        {
            _log.Info(data);
        }

        public void LogWarning(string data)
        {
            _log.Warning(data);
        }
    }
}
