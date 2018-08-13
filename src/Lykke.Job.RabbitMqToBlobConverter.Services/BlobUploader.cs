using Common;
using Common.Log;
using Lykke.Common.Log;
using Lykke.Job.RabbitMqToBlobConverter.Core.Domain;
using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    public class BlobUploader : IBlobUploader
    {
        private const string _tablesStructureFileName = "TableStructure.str2";
        private const string _tablesStructureBackupFileNamePattern = "TableStructure-{0}.str2";
        private const string _hourFormat = "yyyy-MM-dd-HH";
        private const string _blobContentType = "text/plain";
        private const int _maxBlockSize = 100 * 1024 * 1024; // 100Mb
        private const int _maxBlobBlocksCount = 50000;
        private const int _maxBufferCount = 500;

        private readonly Encoding _blobEncoding = Encoding.UTF8;
        private readonly BlobRequestOptions _blobRequestOptions = new BlobRequestOptions
        {
            RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(5), 5),
            MaximumExecutionTime = TimeSpan.FromMinutes(60),
            ServerTimeout = TimeSpan.FromMinutes(60)
        };
        private readonly CloudBlobContainer _blobContainer;
        private readonly ILog _log;
        private readonly Dictionary<string, BlobInfo> _blobDict = new Dictionary<string, BlobInfo>();
        private readonly Dictionary<string, List<string>> _data = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> _structureChanges = new Dictionary<string, string>();
        private readonly TimeSpan _uploadFrequency;

        private DateTime _lastMessageTime = DateTime.MinValue;
        private DateTime _lastBlobTime = DateTime.MinValue;
        private int _bufferCount;

        public BlobUploader(
            TimeSpan uploadFrequency,
            string blobConnectionString,
            string rootContainer,
            ILogFactory logFactory)
        {
            _uploadFrequency = uploadFrequency;
            var blobClient = CloudStorageAccount.Parse(blobConnectionString).CreateCloudBlobClient();
            _blobContainer = blobClient.GetContainerReference(rootContainer);
            _blobContainer.CreateIfNotExistsAsync(BlobContainerPublicAccessType.Off, null, null).GetAwaiter().GetResult();
            _log = logFactory.CreateLog(this);
        }

        public async Task CreateOrUpdateTablesStructureAsync(TablesStructure tablesStructure)
        {
            string newStructureStr = tablesStructure.ToJson();

            var blob = _blobContainer.GetBlockBlobReference(_tablesStructureFileName);
            bool exists = await blob.ExistsAsync();
            if (exists)
            {
                string structureStr = await blob.DownloadTextAsync(null, _blobRequestOptions, null);
                if (structureStr == newStructureStr)
                    return;

                await BackupStructureAsync(structureStr);

                _log.Warning("Table structure change", $"Table structure is changed from {structureStr} to {newStructureStr}");
                await blob.DeleteAsync();

                var oldStructure = structureStr.DeserializeJson<TablesStructure>();
                if (oldStructure?.Tables != null)
                {
                    foreach (var table in oldStructure.Tables)
                    {
                        var newTable = tablesStructure.Tables.FirstOrDefault(t => t.AzureBlobFolder == table.AzureBlobFolder);
                        if (newTable == null)
                            continue;

                        if (newTable.Columns.Count != table.Columns.Count)
                        {
                            await AddStructureChangeAsync(table.AzureBlobFolder);
                            continue;
                        }
                        foreach (var column in table.Columns)
                        {
                            var newColumn = newTable.Columns.FirstOrDefault(c => c.ColumnName == column.ColumnName);
                            if (newColumn != null)
                                continue;

                            await AddStructureChangeAsync(table.AzureBlobFolder);
                            break;
                        }
                    }
                }
            }
            await blob.UploadTextAsync(newStructureStr, null, _blobRequestOptions, null);
            await SetContentTypeAsync(blob);
        }

        public async Task StopAsync()
        {
            await SaveDataAsync(true);
        }

        public async Task SaveToBlobAsync(Dictionary<string, List<string>> messageData)
        {
            var now = DateTime.UtcNow;

            await CheckNewHourAsync(now);

            foreach (var pair in messageData)
            {
                string directory = _structureChanges.ContainsKey(pair.Key)
                    ? _structureChanges[pair.Key]
                    : pair.Key;

                if (_data.ContainsKey(directory))
                    _data[directory].AddRange(pair.Value);
                else
                    _data.Add(directory, pair.Value);
            }

            ++_bufferCount;

            if (now.Subtract(_lastMessageTime) >= _uploadFrequency || _bufferCount >= _maxBufferCount)
            {
                await SaveDataAsync(false);
                _lastMessageTime = now;
            }
        }

        private async Task CheckNewHourAsync(DateTime now)
        {
            if (now.Subtract(_lastBlobTime).TotalHours < 1 && now.Hour == _lastBlobTime.Hour)
                return;

            await SaveDataAsync(true);

            _lastBlobTime = now;
            _lastMessageTime = now;
        }

        private async Task SaveDataAsync(bool clearBlocks)
        {
            string blobName = _lastBlobTime.ToString(_hourFormat);

            var tasks = _data.Select(p => SaveTableDataAsync(p.Key, blobName));
            await Task.WhenAll(tasks);

            _bufferCount = 0;

            if (clearBlocks)
                _blobDict.Clear();
        }

        private async Task SaveTableDataAsync(string directory, string blobName)
        {
            var items = _data[directory];
            if (items.Count == 0)
                return;

            if (!_blobDict.ContainsKey(directory))
                await InitNextBlobAsync(directory, blobName);

            BlobInfo blobInfo = _blobDict[directory];

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    foreach (var item in items)
                    {
                        if (stream.Length + item.Length * 2 >= _maxBlockSize)
                        {
                            await UploadBlockAsync(
                                blobInfo,
                                stream,
                                directory);
                            stream.Position = 0;
                            stream.SetLength(0);
                        }

                        writer.WriteLine(item);
                        writer.Flush();
                    }

                    if (stream.Length > 0)
                        await UploadBlockAsync(
                            blobInfo,
                            stream,
                            directory);
                }
            }

            items.Clear();
        }

        private async Task UploadBlockAsync(
            BlobInfo blobInfo,
            Stream stream,
            string directory)
        {
            string blockId = Convert.ToBase64String(Encoding.Default.GetBytes(blobInfo.BlockIds.Count.ToString("d6")));
            stream.Position = 0;
            await blobInfo.Blob.PutBlockAsync(blockId, stream, null, null, _blobRequestOptions, null);
            blobInfo.BlockIds.Add(blockId);
            await blobInfo.Blob.PutBlockListAsync(blobInfo.BlockIds);

            if (blobInfo.BlockIds.Count < _maxBlobBlocksCount)
                return;

            await InitNextBlobAsync(directory, blobInfo.Blob.Name);
        }

        private async Task InitNextBlobAsync(string directory, string blobName)
        {
            string path = Path.Combine(directory.ToLower(), blobName);
            int i = 0;
            while (true)
            {
                var fileName = i == 0 ? path : $"{path}--{i:00}";
                var blob = _blobContainer.GetBlockBlobReference(fileName);
                if (await blob.ExistsAsync())
                {
                    if (!blob.Properties.AppendBlobCommittedBlockCount.HasValue)
                        await blob.FetchAttributesAsync();
                    int blobBlocksCount = blob.Properties.AppendBlobCommittedBlockCount ?? 0;
                    if (blobBlocksCount < _maxBlobBlocksCount)
                    {
                        var blobBlocks = await blob.DownloadBlockListAsync();
                        _blobDict[directory] = new BlobInfo
                        {
                            Blob = blob,
                            BlockIds = blobBlocks.Select(b => b.Name).ToList(),
                        };
                        return;
                    }
                }
                else
                {
                    await SetContentTypeAsync(blob);
                    _blobDict[directory] = new BlobInfo
                    {
                        Blob = blob,
                        BlockIds = new List<string>(),
                    };
                    return;
                }
                ++i;
            }
        }

        private async Task SetContentTypeAsync(CloudBlockBlob blob)
        {
            try
            {
                blob.Properties.ContentType = _blobContentType;
                blob.Properties.ContentEncoding = _blobEncoding.WebName;
                await blob.SetPropertiesAsync(null, _blobRequestOptions, null);
            }
            catch (StorageException)
            {
            }
        }

        private async Task BackupStructureAsync(string structure)
        {
            int i = 1;
            while (true)
            {
                var fileName = string.Format(_tablesStructureBackupFileNamePattern, i);
                var blob = _blobContainer.GetBlockBlobReference(fileName);
                if (await blob.ExistsAsync())
                    continue;

                await blob.UploadTextAsync(structure, null, _blobRequestOptions, null);
                await SetContentTypeAsync(blob);
                break;
            }
        }

        private async Task AddStructureChangeAsync(string directory)
        {
            int i = 1;
            string newDirectory;
            while (true)
            {
                var dirName = $"{directory}{i}";
                var dir = _blobContainer.GetDirectoryReference(dirName);
                var dirBlobs = await dir.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, 1, null, _blobRequestOptions, null);
                if (dirBlobs.Results.Any())
                    continue;

                newDirectory = dirName;
                break;
            }

            _structureChanges.Add(directory, newDirectory);
        }
    }
}
