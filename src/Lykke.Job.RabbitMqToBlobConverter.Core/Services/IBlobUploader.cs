using Lykke.Job.RabbitMqToBlobConverter.Core.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IBlobUploader
    {
        Task CreateOrUpdateTablesStructureAsync(TablesStructure tablesStructure);

        Task SaveToBlobAsync(Dictionary<string, List<string>> messageData);

        Task StopAsync();
    }
}
