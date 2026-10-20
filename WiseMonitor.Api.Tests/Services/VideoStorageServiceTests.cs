using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

/// <summary>
/// URLs assinadas do bucket S3 compatível (Cloudflare R2). A assinatura é calculada
/// localmente, sem chamar o provedor.
/// </summary>
public class VideoStorageServiceTests
{
    private static readonly string[] Vars =
        ["VIDEO_BUCKET", "VIDEO_STORAGE_ENDPOINT", "VIDEO_STORAGE_ACCESS_KEY", "VIDEO_STORAGE_SECRET_KEY"];

    private static VideoStorageService WithEnv(params (string Name, string? Value)[] values)
    {
        var previous = Vars.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var v in Vars) Environment.SetEnvironmentVariable(v, null);
            foreach (var (name, value) in values) Environment.SetEnvironmentVariable(name, value);
            return new VideoStorageService();
        }
        finally
        {
            foreach (var (name, value) in previous) Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static VideoStorageService R2() => WithEnv(
        ("VIDEO_BUCKET", "wisemonitor-videos"),
        ("VIDEO_STORAGE_ENDPOINT", "https://abc123.r2.cloudflarestorage.com"),
        ("VIDEO_STORAGE_ACCESS_KEY", "AKIDEXAMPLE"),
        ("VIDEO_STORAGE_SECRET_KEY", "secret-example"));

    [Fact]
    public void SemConfiguracaoCompleta_FicaDesligado()
    {
        Assert.False(WithEnv().IsEnabled);
        Assert.False(WithEnv(("VIDEO_BUCKET", "wisemonitor-videos")).IsEnabled);
    }

    [Fact]
    public async Task UrlDeEnvio_ApontaParaOBucketComPutAssinadoEContentType()
    {
        var storage = R2();
        Assert.True(storage.IsEnabled);

        var org = Guid.NewGuid();
        var segment = Guid.NewGuid();
        var name = storage.BuildObjectName(org, segment);
        var url = new Uri(await storage.CreateUploadUrlAsync(name));

        Assert.Equal("https", url.Scheme);
        Assert.Equal("abc123.r2.cloudflarestorage.com", url.Host);
        Assert.Equal($"/wisemonitor-videos/videos/{org:N}/{segment:N}.mp4", url.AbsolutePath);
        Assert.Contains("X-Amz-Signature=", url.Query);
        Assert.Contains("X-Amz-Credential=AKIDEXAMPLE%2F", url.Query);
        Assert.Contains("%2Fauto%2Fs3%2F", url.Query);           // região do R2
        Assert.Contains("content-type", url.Query);              // Content-Type entra na assinatura
    }

    [Fact]
    public async Task UrlDeLeitura_EAssinadaEDiferenteDaDeEnvio()
    {
        var storage = R2();
        var name = storage.BuildObjectName(Guid.NewGuid(), Guid.NewGuid());

        var read = await storage.CreateReadUrlAsync(name);
        var upload = await storage.CreateUploadUrlAsync(name);

        Assert.Contains("X-Amz-Signature=", read);
        Assert.DoesNotContain("content-type", read);
        Assert.NotEqual(read, upload);
    }
}
