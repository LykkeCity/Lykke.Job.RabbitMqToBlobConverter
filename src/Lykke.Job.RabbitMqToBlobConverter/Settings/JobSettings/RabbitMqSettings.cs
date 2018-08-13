using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RabbitMqToBlobConverter.Settings.JobSettings
{
    public class RabbitMqSettings
    {
        [AmqpCheck]
        public string ConnectionString { get; set; }

        public string ExchangeName { get; set; }

        [Optional]
        public string RoutingKey { get; set; }
    }
}
