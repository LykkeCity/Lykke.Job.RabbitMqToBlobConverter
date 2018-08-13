using System.Collections.Generic;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IMessageConverter
    {
        Dictionary<string, List<string>> Convert(object message);
    }
}
