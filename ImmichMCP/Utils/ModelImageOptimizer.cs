using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ImmichMCP.Utils;

internal sealed record OptimizedModelImage(
    byte[] Bytes,
    string MimeType,
    int Width,
    int Height,
    int Quality);

internal static class ModelImageOptimizer
{
    internal const int MaxLongEdge = 1024;
    internal const int JpegQuality = 80;

    internal static OptimizedModelImage Create(byte[] previewBytes)
    {
        using var image = Image.Load(previewBytes);
        image.Mutate(context => context.AutoOrient());

        if (image.Width > MaxLongEdge || image.Height > MaxLongEdge)
        {
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxLongEdge, MaxLongEdge)
            }));
        }

        using var output = new MemoryStream();
        image.SaveAsJpeg(output, new JpegEncoder { Quality = JpegQuality });

        return new OptimizedModelImage(
            output.ToArray(),
            "image/jpeg",
            image.Width,
            image.Height,
            JpegQuality);
    }
}
