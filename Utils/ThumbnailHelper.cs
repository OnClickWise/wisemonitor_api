using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace WiseMonitor.Api.Utils
{
    public static class ThumbnailHelper
    {
        public static async Task GenerateThumbnailAsync(string sourcePath, string destPath, int width)
        {
            // Carrega a imagem do caminho fornecido
            using var image = await Image.LoadAsync(sourcePath);

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
