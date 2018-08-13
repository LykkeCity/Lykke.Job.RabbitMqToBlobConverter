using System;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Services
{
    public interface ITypeRetriever
    {
        Task<Type> RetrieveTypeAsync();
    }
}
