using System;
using System.Collections.Generic;
using Lykke.SettingsReader.Attributes;

namespace Lykke.Job.RabbitMqToBlobConverter.Settings.JobSettings
{
    public class RabbitMqToBlobConverterSettings
    {
        public DbSettings Db { get; set; }

        public RabbitMqSettings Rabbit { get; set; }

        public string NugetPackage { get; set; }

        public string DeserializationType { get; set; }

        public string BlobContainer { get; set; }

        public TimeSpan UploadFrequency { get; set; }

        [Optional]
        public string InstanceTag { get; set; }

        [Optional]
        public Dictionary<string, List<string>> ExcludedPropertiesMap { get; set; }

        [Optional]
        public Dictionary<string, string> IdPropertiesMap { get; set; }

        [Optional]
        public Dictionary<string, string> RelationPropertiesMap { get; set; }
    }
}
