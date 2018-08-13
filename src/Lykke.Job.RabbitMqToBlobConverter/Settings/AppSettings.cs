using Lykke.Job.RabbitMqToBlobConverter.Settings.JobSettings;
using Lykke.Job.RabbitMqToBlobConverter.Settings.SlackNotifications;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RabbitMqToBlobConverter.Settings
{
    public class AppSettings
    {
        public RabbitMqToBlobConverterSettings RabbitMqToBlobConverterJob { get; set; }

        public SlackNotificationsSettings SlackNotifications { get; set; }

        [Optional]
        public MonitoringServiceClientSettings MonitoringServiceClient { get; set; }
    }
}
