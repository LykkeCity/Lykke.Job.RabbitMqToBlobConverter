using Microsoft.WindowsAzure.Storage.Blob;
using System.Collections.Generic;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    internal class BlobInfo
    {
        internal CloudBlockBlob Blob { get; set; }
        internal List<string> BlockIds { get; set; }
    }
}
