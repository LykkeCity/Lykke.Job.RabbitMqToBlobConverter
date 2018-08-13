using Common;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IOrchestrator : IStopable
    {
        Task StartAsync();
    }
}
