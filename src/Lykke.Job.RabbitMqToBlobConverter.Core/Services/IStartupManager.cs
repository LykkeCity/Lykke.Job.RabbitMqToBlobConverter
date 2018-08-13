using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IStartupManager
    {
        Task StartAsync();
    }
}