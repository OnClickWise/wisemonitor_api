using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Guarda os segmentos de vídeo no Google Cloud Storage em vez do Postgres.
    /// O agent envia o MP4 direto ao bucket por uma URL assinada (o arquivo não passa
    /// pelo Cloud Run) e o painel toca o vídeo do bucket por outra URL assinada.
    /// Quando o bucket guardava os bytes na coluna VideoData, 83 máquinas mandando um
    /// MP4 a cada 10s encheram o banco (545 GB) e derrubaram a API.
    ///
    /// Desligado enquanto VIDEO_BUCKET não estiver definido: o agent recebe 204 e
    /// descarta o segmento. A retenção (90 dias) é uma regra de ciclo de vida do bucket.
    /// </summary>
    public class VideoStorageService : IVideoStorageService
    {
        public const string ContentType = "video/mp4";

        private static readonly TimeSpan UploadUrlValidity = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ReadUrlValidity = TimeSpan.FromHours(1);

        private readonly string? _bucket;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private UrlSigner? _signer;
        private StorageClient? _client;

        public VideoStorageService()
        {
            var bucket = Environment.GetEnvironmentVariable("VIDEO_BUCKET");
            _bucket = string.IsNullOrWhiteSpace(bucket) ? null : bucket.Trim();
        }

        public bool IsEnabled => _bucket != null;

        /// <summary>
        /// O nome do objeto sai só da organização do token e de um id gerado pelo
        /// servidor: na confirmação ele é recalculado, então um agent não consegue
        /// registrar como seu um arquivo de outra organização.
        /// </summary>
        public string BuildObjectName(Guid organizationId, Guid segmentId) =>
            $"videos/{organizationId:N}/{segmentId:N}.mp4";

        public async Task<string> CreateUploadUrlAsync(string objectName)
        {
            var signer = await GetSignerAsync();
            var template = UrlSigner.RequestTemplate
                .FromBucket(RequireBucket())
                .WithObjectName(objectName)
                .WithHttpMethod(HttpMethod.Put)
                .WithContentHeaders(new Dictionary<string, IEnumerable<string>>
                {
                    ["Content-Type"] = new[] { ContentType }
                });

            return await signer.SignAsync(template, UrlSigner.Options.FromDuration(UploadUrlValidity));
        }

        public async Task<string> CreateReadUrlAsync(string objectName)
        {
            var signer = await GetSignerAsync();
            var template = UrlSigner.RequestTemplate
                .FromBucket(RequireBucket())
                .WithObjectName(objectName)
                .WithHttpMethod(HttpMethod.Get);

            return await signer.SignAsync(template, UrlSigner.Options.FromDuration(ReadUrlValidity));
        }

        public async Task DownloadToFileAsync(string objectName, string path)
        {
            await EnsureInitializedAsync();
            await using var file = File.Create(path);
            await _client!.DownloadObjectAsync(RequireBucket(), objectName, file);
        }

        private string RequireBucket() =>
            _bucket ?? throw new InvalidOperationException("VIDEO_BUCKET não configurado.");

        private async Task<UrlSigner> GetSignerAsync()
        {
            await EnsureInitializedAsync();
            return _signer!;
        }

        private async Task EnsureInitializedAsync()
        {
            if (_signer != null && _client != null)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_signer != null && _client != null)
                    return;

                // No Cloud Run é a conta de serviço da instância: a assinatura vai à API
                // do IAM (signBlob), sem chave privada guardada em lugar nenhum.
                var credential = await GoogleCredential.GetApplicationDefaultAsync();
                _signer = UrlSigner.FromCredential(credential);
                _client = await StorageClient.CreateAsync(credential);
            }
            finally
            {
                _initLock.Release();
            }
        }
    }

    public interface IVideoStorageService
    {
        bool IsEnabled { get; }
        string BuildObjectName(Guid organizationId, Guid segmentId);
        Task<string> CreateUploadUrlAsync(string objectName);
        Task<string> CreateReadUrlAsync(string objectName);
        Task DownloadToFileAsync(string objectName, string path);
    }
}
