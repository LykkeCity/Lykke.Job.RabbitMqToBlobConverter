using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RabbitMqToBlobConverter.Settings.JobSettings
{
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }

        [AzureBlobCheck]
        public string BlobConnString { get; set; }
    }
}
