using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace WiseMonitor.Api.Utils
{
    public static class ThumbnailHelper
    {
        // Só formatos que a API aceita em upload. Deixa TIFF/BMP/etc. de fora:
        // ImageSharp 3.x tem falhas conhecidas no decoder/encoder TIFF (corrigidas só na 4.x, licenciada).
        private static readonly DecoderOptions SafeDecoderOptions = new()
        {
            Configuration = new Configuration(
                new PngConfigurationModule(),
                new JpegConfigurationModule(),
                new WebpConfigurationModule(),
                new GifConfigurationModule())
        };

        public static async Task GenerateThumbnailAsync(string sourcePath, string destPath, int width)
        {
            // Carrega a imagem do caminho fornecido
            using var image = await Image.LoadAsync(SafeDecoderOptions, sourcePath);

            // Calcula altura proporcional
            var ratio = (double)width / image.Width;
            var height = (int)(image.Height * ratio);

            // Redimensiona a imagem
            image.Mutate(x => x.Resize(width, height));

            // Salva a thumbnail como JPEG
            await image.SaveAsync(destPath, new JpegEncoder());
        }
    }
}
