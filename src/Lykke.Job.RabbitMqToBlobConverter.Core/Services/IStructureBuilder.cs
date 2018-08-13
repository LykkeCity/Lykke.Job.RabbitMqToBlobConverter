using Lykke.Job.RabbitMqToBlobConverter.Core.Domain;
using System;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface IStructureBuilder
    {
        TablesStructure GetTablesStructure(Type type);
    }
}
