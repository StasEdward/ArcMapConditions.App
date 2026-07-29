using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace ArcMapConditions.App.Services;

/// <summary>
/// Loads condition icons by slug from the Assets/icons folder next to the exe,
/// caching each image and falling back to generic.png for unknown slugs.
/// </summary>
public static class IconProvider
{
    private static readonly string IconsDir =
        Path.Combine(AppContext.BaseDirectory, "Assets", "icons");

    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public static BitmapImage Get(string slug)
    {
        if (Cache.TryGetValue(slug, out BitmapImage? cached))
            return cached;

        BitmapImage image = Load(slug) ?? Load("generic") ?? Empty();
        Cache[slug] = image;
        return image;
    }

    private static BitmapImage? Load(string slug)
    {
        string path = Path.Combine(IconsDir, slug + ".png");
        if (!File.Exists(path))
            return null;

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;      // read fully, don't lock the file
        bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bmp.UriSource = new Uri(path, UriKind.Absolute);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private static BitmapImage Empty()
    {
        // 1x1 transparent fallback so binding never yields null.
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = new MemoryStream(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC"));
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}
