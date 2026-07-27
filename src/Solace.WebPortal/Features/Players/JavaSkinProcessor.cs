using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Solace.WebPortal.Common.Features.Players;

namespace Solace.WebPortal.Features.Players;

public static partial class JavaSkinProcessor
{
    public static async Task<ProcessResult> Process(Stream imageData, SkinType skinType, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageData, cancellationToken);

        // detect old skin format
        if (image.Size == new Size(64, 32))
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions()
            {
                Size = new Size(64, 64),
                Mode = ResizeMode.BoxPad,
                Position = AnchorPositionMode.Top,
                PadColor = Color.Transparent,
            }));
        }
        else if (image.Size != new Size(64, 64))
        {
            return $"Invalid image size ({image.Size.Width}x{image.Size.Height}), only 64x64 and 64x32 are supported";
        }

        using var pngStream = new MemoryStream();
        await image.SaveAsPngAsync(pngStream, cancellationToken);

        var isSkinSlim = skinType switch
        {
            SkinType.Auto => image[54, 20].A == 0,
            SkinType.Slim => true,
            _ => false
        };

        return (pngStream.ToArray(), isSkinSlim);
    }

    public readonly union ProcessResult(string, (byte[] ImageData, bool IsSkinSlim));
}