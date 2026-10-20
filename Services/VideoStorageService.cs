using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Guarda os segmentos de vídeo num bucket compatível com S3 (Cloudflare R2, Backblaze
    /// B2, MinIO...) em vez do Postgres. O agent envia o MP4 direto ao bucket por uma URL
    /// assinada (o arquivo não passa pela API) e o painel toca o vídeo do bucket por outra
    /// URL assinada. Com os bytes na coluna VideoData, dezenas de máquinas mandando um MP4
    /// a cada 10s enchem o banco em poucos dias.
    ///
    /// Desligado enquanto VIDEO_BUCKET, VIDEO_STORAGE_ENDPOINT, VIDEO_STORAGE_ACCESS_KEY e
    /// VIDEO_STORAGE_SECRET_KEY não estiverem todos definidos: o agent usa o upload antigo.
    /// A retenção dos arquivos é uma regra de ciclo de vida do bucket.
    /// </summary>
    public class VideoStorageService : IVideoStorageService
    {
        public const string ContentType = "video/mp4";

        private static readonly TimeSpan UploadUrlValidity = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ReadUrlValidity = TimeSpan.FromHours(1);

        private readonly string? _bucket;
        private readonly AmazonS3Client? _client;

        public VideoStorageService()
        {
            var bucket = Env("VIDEO_BUCKET");
            // R2: https://<account-id>.r2.cloudflarestorage.com
            var endpoint = Env("VIDEO_STORAGE_ENDPOINT");
            var accessKey = Env("VIDEO_STORAGE_ACCESS_KEY");
            var secretKey = Env("VIDEO_STORAGE_SECRET_KEY");

            if (bucket == null || endpoint == null || accessKey == null || secretKey == null)
                return;

            _bucket = bucket;
            _client = new AmazonS3Client(
                new BasicAWSCredentials(accessKey, secretKey),
                new AmazonS3Config
                {
                    ServiceURL = endpoint,
                    // R2 usa a região "auto"; B2/MinIO aceitam a que vier aqui.
                    AuthenticationRegion = Env("VIDEO_STORAGE_REGION") ?? "auto",
                    ForcePathStyle = true,
                    // Checksums automáticos do SDK v4 não são aceitos por todo provedor S3
                    // compatível (o R2 recusava uploads com eles): só quando obrigatórios.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                });
        }

        public bool IsEnabled => _client != null;

        /// <summary>
        /// O nome do objeto sai só da organização do token e de um id gerado pelo
        /// servidor: na confirmação ele é recalculado, então um agent não consegue
        /// registrar como seu um arquivo de outra organização.
        /// </summary>
        public string BuildObjectName(Guid organizationId, Guid segmentId) =>
            $"videos/{organizationId:N}/{segmentId:N}.mp4";

        public Task<string> CreateUploadUrlAsync(string objectName) =>
            RequireClient().GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = objectName,
                Verb = HttpVerb.PUT,
                // O agent manda exatamente este Content-Type; ele entra na assinatura.
                ContentType = ContentType,
                Protocol = Protocol.HTTPS,
                Expires = DateTime.UtcNow.Add(UploadUrlValidity),
            });

        public Task<string> CreateReadUrlAsync(string objectName) =>
            RequireClient().GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = _bucket,
                Key = objectName,
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS,
                Expires = DateTime.UtcNow.Add(ReadUrlValidity),
            });

        public async Task DownloadToFileAsync(string objectName, string path)
        {
            using var response = await RequireClient().GetObjectAsync(_bucket, objectName);
            await using var file = File.Create(path);
            await response.ResponseStream.CopyToAsync(file);
        }

        private AmazonS3Client RequireClient() =>
            _client ?? throw new InvalidOperationException("Armazenamento de vídeo (VIDEO_BUCKET/VIDEO_STORAGE_*) não configurado.");

        private static string? Env(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
