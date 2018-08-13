using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    public class Orchestrator : IOrchestrator
    {
        private readonly ITypeRetriever _typeRetriever;
        private readonly IStructureBuilder _structureBuilder;
        private readonly IBlobUploader _blobUploader;
        private readonly IRabbitMqSubscriber _rabbitMqSubscriber;

        public Orchestrator(
            ITypeRetriever typeRetriever,
            IStructureBuilder structureBuilder,
            IBlobUploader blobUploader,
            IRabbitMqSubscriber rabbitMqSubscriber)
        {
            _typeRetriever = typeRetriever;
            _structureBuilder = structureBuilder;
            _blobUploader = blobUploader;
            _rabbitMqSubscriber = rabbitMqSubscriber;
        }

        public async Task StartAsync()
        {
            var type = await _typeRetriever.RetrieveTypeAsync();
            var tablesStructure = _structureBuilder.GetTablesStructure(type);
            await _blobUploader.CreateOrUpdateTablesStructureAsync(tablesStructure);

            _rabbitMqSubscriber.Start(type);
        }

        public void Stop()
        {
            _rabbitMqSubscriber.StopAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
