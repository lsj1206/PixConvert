using System;
using System.IO;
using NetVips;
using SkiaSharp;

namespace PixConvert.Tests;

internal static class TestImageFactory
{
    public static void CreateTransparentPng(string path, int width = 100, int height = 100)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(10, 10, width - 20, height - 20, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    public static string CreateAnimatedGif(string path, int frameCount, int width = 100, int height = 100)
    {
        var frames = new Image[frameCount];

        try
        {
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = (i % 2 == 0)
                    ? Image.Black(width, height)
                    : Image.Black(width, height) + 255;
            }

            using var combined = Image.Arrayjoin(frames, across: 1);
            combined.WriteToFile(path);
            return path;
        }
        finally
        {
            foreach (var frame in frames)
            {
                frame?.Dispose();
            }
        }
    }
}
