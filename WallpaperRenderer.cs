using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Win32;

namespace DesktopBilliards;

/// <summary>
/// 直接读取 Windows 当前壁纸并渲染成工作区大小。
/// 这样即使桌面上有终端/浏览器窗口，游戏背景也永远是干净壁纸。
/// 如果壁纸读取失败，调用方会退回“隐藏图标后截屏”的方案。
/// </summary>
internal static class WallpaperRenderer
{
    public static Bitmap? TryCreate(Rectangle workArea)
    {
        try
        {
            using var desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            string? path = desktopKey?.GetValue("WallPaper") as string;
            string style = desktopKey?.GetValue("WallpaperStyle") as string ?? "10";
            string tile = desktopKey?.GetValue("TileWallpaper") as string ?? "0";
            Color desktopColor = ReadDesktopColor();

            Log.Write($"Wallpaper: path='{path}' style={style} tile={tile} work={workArea}");

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                var buffer = new System.Text.StringBuilder(1024);
                if (NativeMethods.SystemParametersInfo(
                        0x0073, // SPI_GETDESKWALLPAPER
                        (uint)buffer.Capacity,
                        buffer,
                        0))
                {
                    path = buffer.ToString();
                }
            }

            // Windows Spotlight 的原始文件可能被系统移走/删除，
            // 但 Themes\TranscodedWallpaper 始终是当前实际壁纸的一份可读副本。
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                string transcoded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft",
                    "Windows",
                    "Themes",
                    "TranscodedWallpaper");

                if (File.Exists(transcoded))
                {
                    path = transcoded;
                    Log.Write("Wallpaper: using TranscodedWallpaper");
                }
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Log.Write("Wallpaper: path missing");
                return null;
            }

            using var stream = File.OpenRead(path);
            using var image = Image.FromStream(stream, false, false);
            Log.Write($"Wallpaper: loaded {image.Width}x{image.Height} format={image.RawFormat}");

            if (image.Width < 2 || image.Height < 2)
                return null;

            var bitmap = new Bitmap(
                Math.Max(1, workArea.Width),
                Math.Max(1, workArea.Height),
                PixelFormat.Format32bppPArgb);

            using var g = Graphics.FromImage(bitmap);
            g.Clear(desktopColor);
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            bool tiled = tile == "1" || style == "1";

            if (tiled)
            {
                using var brush = new TextureBrush(image, WrapMode.Tile);
                g.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
                return bitmap;
            }

            switch (style)
            {
                case "2": // Stretch
                    g.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
                    break;

                case "6": // Fit
                {
                    float scale = Math.Min(
                        bitmap.Width / (float)image.Width,
                        bitmap.Height / (float)image.Height);
                    float width = image.Width * scale;
                    float height = image.Height * scale;
                    g.DrawImage(
                        image,
                        (bitmap.Width - width) / 2f,
                        (bitmap.Height - height) / 2f,
                        width,
                        height);
                    break;
                }

                case "0": // Center
                    g.DrawImage(
                        image,
                        (bitmap.Width - image.Width) / 2f,
                        (bitmap.Height - image.Height) / 2f,
                        image.Width,
                        image.Height);
                    break;

                case "22": // Span
                {
                    var virtualScreen = SystemInformation.VirtualScreen;
                    float scaleX = virtualScreen.Width / (float)image.Width;
                    float scaleY = virtualScreen.Height / (float)image.Height;
                    float dx = -(workArea.Left - virtualScreen.Left) * scaleX;
                    float dy = -(workArea.Top - virtualScreen.Top) * scaleY;

                    g.DrawImage(
                        image,
                        dx,
                        dy,
                        image.Width * scaleX,
                        image.Height * scaleY);
                    break;
                }

                default: // Fill (10) 以及未知值
                {
                    float scale = Math.Max(
                        bitmap.Width / (float)image.Width,
                        bitmap.Height / (float)image.Height);
                    float width = image.Width * scale;
                    float height = image.Height * scale;
                    g.DrawImage(
                        image,
                        (bitmap.Width - width) / 2f,
                        (bitmap.Height - height) / 2f,
                        width,
                        height);
                    break;
                }
            }

            Log.Write($"Wallpaper: rendered {bitmap.Width}x{bitmap.Height}");
            return bitmap;
        }
        catch (Exception ex)
        {
            Log.Write("Wallpaper: failed " + ex);
            return null;
        }
    }

    private static Color ReadDesktopColor()
    {
        try
        {
            using var colorsKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
            string? value = colorsKey?.GetValue("Background") as string;
            if (string.IsNullOrWhiteSpace(value))
                return Color.Black;

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 ||
                !int.TryParse(parts[0], out int r) ||
                !int.TryParse(parts[1], out int g) ||
                !int.TryParse(parts[2], out int b))
            {
                return Color.Black;
            }

            return Color.FromArgb(255, r, g, b);
        }
        catch
        {
            return Color.Black;
        }
    }
}
