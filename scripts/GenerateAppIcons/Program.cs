using System.Runtime.InteropServices;
using SkiaSharp;
using Svg.Skia;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var svgPath = Path.Combine(repoRoot, "src", "Paperdown.App", "Assets", "paperdown.svg");
var assetsDir = Path.GetDirectoryName(svgPath)!;

if (!File.Exists(svgPath))
{
    Console.Error.WriteLine($"SVG not found: {svgPath}");
    return 1;
}

using var svg = new SKSvg();
if (svg.Load(svgPath) is null)
{
    Console.Error.WriteLine("Failed to load SVG.");
    return 1;
}

var picture = svg.Picture!;
var bounds = picture.CullRect;

void RenderPng(string fileName, int width, int height, SKColor? background = null)
{
    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(background ?? SKColors.Transparent);

    var scale = Math.Min(width / bounds.Width, height / bounds.Height) * 0.92f;
    var dx = (width - bounds.Width * scale) / 2f - bounds.Left * scale;
    var dy = (height - bounds.Height * scale) / 2f - bounds.Top * scale;

    canvas.Save();
    canvas.Translate(dx, dy);
    canvas.Scale(scale);
    canvas.DrawPicture(picture);
    canvas.Restore();

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    var path = Path.Combine(assetsDir, fileName);
    var tempPath = path + ".tmp";
    File.WriteAllBytes(tempPath, data.ToArray());
    if (File.Exists(path))
    {
        File.Delete(path);
    }

    File.Move(tempPath, path);
    Console.WriteLine($"Wrote {path} ({data.Size} bytes)");
}

RenderPng("Square150x150Logo.scale-200.png", 300, 300);
RenderPng("Square44x44Logo.scale-200.png", 88, 88);
RenderPng("Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24);
RenderPng("Square44x44Logo.targetsize-48_altform-lightunplated.png", 48, 48);
RenderPng("StoreLogo.png", 100, 100);
RenderPng("Wide310x150Logo.scale-200.png", 620, 300);
RenderPng("SplashScreen.scale-200.png", 620, 300, SKColor.Parse("#F8FAFC"));
RenderPng("LockScreenLogo.scale-200.png", 96, 96);

RenderPng("AppIcon-256.png", 256, 256);
WriteIco(Path.Combine(assetsDir, "AppIcon.ico"), [
    (16, RenderBitmap(16, 16)),
    (24, RenderBitmap(24, 24)),
    (32, RenderBitmap(32, 32)),
    (48, RenderBitmap(48, 48)),
    (64, RenderBitmap(64, 64)),
    (128, RenderBitmap(128, 128)),
    (256, RenderBitmap(256, 256)),
]);

Console.WriteLine("Done.");
return 0;

SKBitmap RenderBitmap(int width, int height)
{
    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    var scale = Math.Min(width / bounds.Width, height / bounds.Height) * 0.9f;
    var dx = (width - bounds.Width * scale) / 2f - bounds.Left * scale;
    var dy = (height - bounds.Height * scale) / 2f - bounds.Top * scale;
    canvas.Save();
    canvas.Translate(dx, dy);
    canvas.Scale(scale);
    canvas.DrawPicture(picture);
    canvas.Restore();
    using var image = surface.Snapshot();
    return SKBitmap.FromImage(image);
}

static void WriteIco(string path, (int size, SKBitmap bitmap)[] entries)
{
    using var fs = File.Create(path);
    using var writer = new BinaryWriter(fs);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)entries.Length);

    var offset = 6 + 16 * entries.Length;
    var imageData = new List<byte[]>();

    foreach (var (size, bitmap) in entries)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = png.ToArray();
        imageData.Add(bytes);

        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(bytes.Length);
        writer.Write(offset);
        offset += bytes.Length;
    }

    foreach (var bytes in imageData)
    {
        writer.Write(bytes);
    }
}
