using System.Collections.Generic;

namespace Lykke.Job.RabbitMqToBlobConverter.Core.Domain
{
    public class TableStructure
    {
        public string TableName { get; set; }

        public string AzureBlobFolder { get; set; }

        public List<ColumnInfo> Columns { get; set; }
    }
}
