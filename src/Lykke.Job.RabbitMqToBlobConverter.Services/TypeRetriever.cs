using Lykke.Common.Log;
using Lykke.Job.RabbitMqToBlobConverter.Core.Services;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    public class TypeRetriever : ITypeRetriever
    {
        private const string _libsDir = "libs";
        private const string _dllExtension = ".dll";
        private const string _betaSuffix = "-beta";
        private const string _packageSource = "https://api.nuget.org/v3/index.json";

        private readonly string _typeName;
        private readonly string _nugetPackageName;
        private readonly PackageMetadataResource _packageMetadataResource;
        private readonly DownloadResource _downloadResource;
        private readonly PackageDownloadContext _packageDownloadContext;
        private readonly NugetLogger _nugetLogger;
        private readonly string _downloadDirectory;

        private Type _type;

        public TypeRetriever(
            string typeName,
            string nugetPackageName,
            ILogFactory logFactory)
        {
            _typeName = typeName;
            _nugetPackageName = nugetPackageName;

            List<Lazy<INuGetResourceProvider>> providers = new List<Lazy<INuGetResourceProvider>>();
            providers.AddRange(Repository.Provider.GetCoreV3());  // Add v3 API support
            var packageSource = new PackageSource(_packageSource);
            SourceRepository sourceRepository = new SourceRepository(packageSource, providers);
            _packageMetadataResource = sourceRepository.GetResource<PackageMetadataResource>();
            _downloadResource = sourceRepository.GetResource<DownloadResource>();

            string workingDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _downloadDirectory = Path.Combine(workingDir, _libsDir);
            _packageDownloadContext = new PackageDownloadContext(
                new SourceCacheContext
                {
                    NoCache = true,
                    DirectDownload = true,
                },
                _downloadDirectory,
                true);
            _nugetLogger = new NugetLogger(logFactory);
        }

        public async Task<Type> RetrieveTypeAsync()
        {
            if (_type != null)
                return _type;

            bool isBetaPackage = _nugetPackageName.EndsWith(_betaSuffix);
            (string searchName, bool isSpecificVersion) = GetSearchablePackageName(_nugetPackageName, isBetaPackage);

            IEnumerable<IPackageSearchMetadata> searchMetadata = await _packageMetadataResource.GetMetadataAsync(
                searchName,
                isBetaPackage,
                false,
                _nugetLogger,
                CancellationToken.None);
            if (!searchMetadata.Any())
                throw new InvalidOperationException($"Package {_nugetPackageName} not found");

            var packageInfo = searchMetadata
                .Cast<PackageSearchMetadata>()
                .OrderByDescending(p => p.Version)
                .First(i => !isSpecificVersion || i.Identity.ToString() == _nugetPackageName);

            var downloadResult = await _downloadResource.GetDownloadResourceResultAsync(
                packageInfo.Identity,
                _packageDownloadContext,
                null,
                _nugetLogger,
                CancellationToken.None);
            if (downloadResult.Status != DownloadResourceResultStatus.Available)
                throw new InvalidOperationException($"Nuget package {_nugetPackageName} of version {packageInfo.Version} is not available for download");

            var pathResolver = new PackagePathResolver(_downloadDirectory);
            var extractContext = new PackageExtractionContext(_nugetLogger);
            var extractedFiles = PackageExtractor.ExtractPackage(downloadResult.PackageStream, pathResolver, extractContext, CancellationToken.None);

            var dllFiles = extractedFiles.Where(f => Path.GetExtension(f).ToLower() == _dllExtension);
            if (!dllFiles.Any())
                throw new InvalidOperationException($"Dll files not found in {_packageDownloadContext.DirectDownloadDirectory}");

            var assembly = Assembly.LoadFile(dllFiles.First());

            var typeName = _typeName;
            int dotIndex = typeName.IndexOf('.');
            if (dotIndex == -1)
                typeName = $"{searchName}.{_typeName}";
            var type = assembly.GetType(typeName);
            if (type == null)
                throw new InvalidOperationException($"Type {typeName} not found among {assembly.ExportedTypes.Select(t => t.FullName).ToList().ToJson()}");

            _type = type;

            return type;
        }

        private (string, bool) GetSearchablePackageName(string fullPackageName, bool isBetaPackage)
        {
            string packageName = isBetaPackage
                ? fullPackageName.Substring(0, fullPackageName.Length - _betaSuffix.Length)
                : fullPackageName;
            var parts = packageName.Split('.');
            for (int i = parts.Length - 1; i > 0; --i)
            {
                if (!int.TryParse(parts[i], out _))
                    return (string.Join('.', parts.Take(i + 1)), isBetaPackage || i < parts.Length - 1);
            }

            return (packageName, isBetaPackage);
        }
    }
}
