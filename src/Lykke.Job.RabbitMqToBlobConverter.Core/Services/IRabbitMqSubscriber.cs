using System;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IRabbitMqSubscriber
    {
        void Start(Type type);

        Task StopAsync();
    }
}
