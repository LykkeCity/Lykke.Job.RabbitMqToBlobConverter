using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    public class StartupManager : IStartupManager
    {
        private readonly IOrchestrator _orchestrator;

        public StartupManager(IOrchestrator orchestrator)
        {
            _orchestrator = orchestrator;
        }

        public async Task StartAsync()
        {
            await _orchestrator.StartAsync();
        }
    }
}
