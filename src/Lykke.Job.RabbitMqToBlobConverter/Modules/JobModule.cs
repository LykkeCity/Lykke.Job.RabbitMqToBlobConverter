using Autofac;
using Common;
using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using Lykke.Job.RabbitMqToBlobConverter.Services;
using Lykke.Job.RabbitMqToBlobConverter.Settings.JobSettings;
using Lykke.Job.RabbitMqToBlobConverter.RabbitSubscribers;

namespace Lykke.Job.RabbitMqToBlobConverter.Modules
{
    public class JobModule : Module
    {
        private readonly RabbitMqToBlobConverterSettings _settings;

        public JobModule(RabbitMqToBlobConverterSettings settings)
        {
            _settings = settings;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<HealthService>()
                .As<IHealthService>()
                .SingleInstance();

            builder.RegisterType<StartupManager>()
                .As<IStartupManager>();

            builder.RegisterType<ShutdownManager>()
                .As<IShutdownManager>();

            builder.RegisterType<TypeRetriever>()
                .As<ITypeRetriever>()
                .SingleInstance()
                .WithParameter("typeName", _settings.DeserializationType)
                .WithParameter("nugetPackageName", _settings.NugetPackage);

            builder.RegisterType<BlobUploader>()
                .As<IBlobUploader>()
                .SingleInstance()
                .WithParameter("uploadFrequency", _settings.UploadFrequency)
                .WithParameter("blobConnectionString", _settings.Db.BlobConnString)
                .WithParameter("rootContainer", _settings.BlobContainer);

            builder.RegisterType<MessageConverter>()
                .As<IMessageConverter>()
                .SingleInstance();

            builder.RegisterType<StructureBuilder>()
                .As<IStructureBuilder>()
                .As<ITypeInfo>()
                .SingleInstance()
                .WithParameter("instanceTag", _settings.InstanceTag)
                .WithParameter("excludedPropertiesMap", _settings.ExcludedPropertiesMap)
                .WithParameter("idPropertiesMap", _settings.IdPropertiesMap)
                .WithParameter("relationPropertiesMap", _settings.RelationPropertiesMap);

            builder.RegisterType<RabbitSubscriber>()
                .As<IRabbitMqSubscriber>()
                .SingleInstance()
                .WithParameter("connectionString", _settings.Rabbit.ConnectionString)
                .WithParameter("exchangeName", _settings.Rabbit.ExchangeName)
                .WithParameter("routingKey", _settings.Rabbit.RoutingKey);

            builder.RegisterType<Orchestrator>()
                .As<IOrchestrator>()
                .As<IStopable>()
                .SingleInstance();
        }
    }
}
