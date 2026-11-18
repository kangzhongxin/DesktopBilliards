using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DesktopBilliards;

internal static class SpriteFactory
{
    public static Bitmap CreateBall(Bitmap icon, int diameter, Color accent)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var rect = new RectangleF(1.5f, 1.5f, diameter - 3f, diameter - 3f);
        using var circle = new GraphicsPath();
        circle.AddEllipse(rect);
        g.SetClip(circle, CombineMode.Replace);

        using (var baseBrush = new SolidBrush(Color.FromArgb(255, 10, 17, 30)))
            g.FillRectangle(baseBrush, 0, 0, diameter, diameter);

        var src = CenterSquare(icon.Width, icon.Height);
        float inner = diameter * 0.74f;
        var dest = new RectangleF(
            (diameter - inner) / 2f,
            (diameter - inner) / 2f,
            inner,
            inner);

        g.DrawImage(icon, dest, src, GraphicsUnit.Pixel);

        using (var gloss = new LinearGradientBrush(
                   new PointF(0, 0),
                   new PointF(diameter, diameter),
                   Color.FromArgb(86, 255, 255, 255),
                   Color.FromArgb(0, 255, 255, 255)))
            g.FillRectangle(gloss, 0, 0, diameter, diameter);

        using (var shade = new LinearGradientBrush(
                   new PointF(0, diameter * 0.42f),
                   new PointF(0, diameter),
                   Color.FromArgb(0, 0, 0, 0),
                   Color.FromArgb(135, 0, 0, 0)))
            g.FillRectangle(shade, 0, diameter * 0.42f, diameter, diameter * 0.58f);

        g.ResetClip();

        using (var rim = new Pen(Color.FromArgb(235, accent), Math.Max(2f, diameter * 0.045f)))
            g.DrawEllipse(rim, rect);

        using (var innerRim = new Pen(Color.FromArgb(74, 255, 255, 255), Math.Max(1f, diameter * 0.014f)))
            g.DrawEllipse(innerRim, rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);

        using (var specular = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(2f, diameter * 0.032f)))
        {
            specular.StartCap = LineCap.Round;
            specular.EndCap = LineCap.Round;
            g.DrawArc(
                specular,
                rect.X + diameter * 0.16f,
                rect.Y + diameter * 0.10f,
                diameter * 0.52f,
                diameter * 0.42f,
                205,
                55);
        }

        using (var glint = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            g.FillEllipse(glint, diameter * 0.245f, diameter * 0.165f, diameter * 0.06f, diameter * 0.06f);

        return bitmap;
    }

    public static Bitmap CreateCueBall(int diameter)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var rect = new RectangleF(1.5f, 1.5f, diameter - 3f, diameter - 3f);
        using var circle = new GraphicsPath();
        circle.AddEllipse(rect);

        using (var body = new LinearGradientBrush(
                   new PointF(diameter * 0.25f, diameter * 0.15f),
                   new PointF(diameter * 0.8f, diameter * 0.9f),
                   Color.FromArgb(255, 255, 255, 255),
                   Color.FromArgb(255, 184, 204, 212)))
            g.FillPath(body, circle);

        g.SetClip(circle, CombineMode.Replace);

        using (var shade = new LinearGradientBrush(
                   new PointF(0, diameter * 0.45f),
                   new PointF(0, diameter),
                   Color.FromArgb(0, 0, 0, 0),
                   Color.FromArgb(100, 0, 20, 28)))
            g.FillRectangle(shade, 0, diameter * 0.45f, diameter, diameter * 0.55f);

        using (var rim = new Pen(Color.FromArgb(220, 140, 232, 206), Math.Max(2f, diameter * 0.045f)))
            g.DrawEllipse(rim, rect);

        using (var highlight = new Pen(Color.FromArgb(245, 255, 255, 255), Math.Max(2f, diameter * 0.04f)))
        {
            highlight.StartCap = LineCap.Round;
            highlight.EndCap = LineCap.Round;
            g.DrawArc(
                highlight,
                rect.X + diameter * 0.17f,
                rect.Y + diameter * 0.10f,
                diameter * 0.48f,
                diameter * 0.4f,
                200,
                60);
        }

        using (var glint = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
            g.FillEllipse(glint, diameter * 0.26f, diameter * 0.18f, diameter * 0.075f, diameter * 0.075f);

        using (var dot = new SolidBrush(Color.FromArgb(120, 106, 178, 190)))
            g.FillEllipse(dot, diameter * 0.59f, diameter * 0.58f, diameter * 0.09f, diameter * 0.09f);

        g.ResetClip();
        return bitmap;
    }

    public static Bitmap CreateSolidBall(int diameter, Color color)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var rect = new RectangleF(1.5f, 1.5f, diameter - 3f, diameter - 3f);
        using var circle = new GraphicsPath();
        circle.AddEllipse(rect);

        Color light = Blend(color, Color.White, 0.42f);
        Color dark = Blend(color, Color.Black, 0.5f);

        using (var body = new LinearGradientBrush(
                   new PointF(diameter * 0.22f, diameter * 0.14f),
                   new PointF(diameter * 0.8f, diameter * 0.9f),
                   light,
                   dark))
            g.FillPath(body, circle);

        g.SetClip(circle, CombineMode.Replace);

        using (var shade = new LinearGradientBrush(
                   new PointF(0, diameter * 0.42f),
                   new PointF(0, diameter),
                   Color.FromArgb(0, 0, 0, 0),
                   Color.FromArgb(125, 0, 0, 0)))
            g.FillRectangle(shade, 0, diameter * 0.42f, diameter, diameter * 0.58f);

        using (var gloss = new LinearGradientBrush(
                   new PointF(0, 0),
                   new PointF(diameter, diameter),
                   Color.FromArgb(72, 255, 255, 255),
                   Color.FromArgb(0, 255, 255, 255)))
            g.FillRectangle(gloss, 0, 0, diameter, diameter);

        g.ResetClip();

        using (var rim = new Pen(Color.FromArgb(235, 244, 249, 255), Math.Max(2f, diameter * 0.04f)))
            g.DrawEllipse(rim, rect);

        using (var innerRim = new Pen(Color.FromArgb(65, 255, 255, 255), Math.Max(1f, diameter * 0.012f)))
            g.DrawEllipse(innerRim, rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);

        using (var specular = new Pen(Color.FromArgb(225, 255, 255, 255), Math.Max(2f, diameter * 0.033f)))
        {
            specular.StartCap = LineCap.Round;
            specular.EndCap = LineCap.Round;
            g.DrawArc(
                specular,
                rect.X + diameter * 0.16f,
                rect.Y + diameter * 0.1f,
                diameter * 0.52f,
                diameter * 0.42f,
                205,
                55);
        }

        using (var glint = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
            g.FillEllipse(glint, diameter * 0.25f, diameter * 0.17f, diameter * 0.06f, diameter * 0.06f);

        return bitmap;
    }

    private static Color Blend(Color a, Color b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(
            (int)(a.A + (b.A - a.A) * amount),
            (int)(a.R + (b.R - a.R) * amount),
            (int)(a.G + (b.G - a.G) * amount),
            (int)(a.B + (b.B - a.B) * amount));
    }

    public static Bitmap CreatePlainBall(int diameter, Color accent)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var rect = new RectangleF(2, 2, diameter - 4, diameter - 4);
        using var path = new GraphicsPath();
        path.AddEllipse(rect);

        using (var fill = new LinearGradientBrush(
                   new PointF(0, 0),
                   new PointF(diameter, diameter),
                   Color.FromArgb(255, 30, 42, 62),
                   Color.FromArgb(255, 8, 13, 23)))
            g.FillPath(fill, path);

        using (var rim = new Pen(Color.FromArgb(235, accent), Math.Max(2f, diameter * 0.05f)))
            g.DrawEllipse(rim, rect);

        using (var glint = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
            g.FillEllipse(glint, diameter * 0.28f, diameter * 0.2f, diameter * 0.07f, diameter * 0.07f);

        return bitmap;
    }

    public static Bitmap CreateShadow(int diameter)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const int layers = 20;
        for (int i = 0; i < layers; i++)
        {
            float inset = diameter * 0.5f * (i / (float)layers) * 0.65f;
            var rect = new RectangleF(inset, inset, diameter - inset * 2, diameter - inset * 2);
            using var brush = new SolidBrush(Color.FromArgb(5, 0, 0, 0));
            g.FillEllipse(brush, rect);
        }

        return bitmap;
    }

    public static Bitmap CreatePocket(int holeDiameter, Color accent)
    {
        int size = Math.Max(16, (int)(holeDiameter * 1.5f));
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float hole = holeDiameter;
        float cx = size / 2f;
        float cy = size / 2f;

        // 外圈柔和的暗色光晕
        for (int i = 12; i >= 0; i--)
        {
            float t = i / 12f;
            float d = hole * (1.0f + t * 0.32f);
            using var glow = new SolidBrush(Color.FromArgb(10 + (int)(12 * (1 - t)), 4, 9, 16));
            g.FillEllipse(glow, cx - d / 2f, cy - d / 2f, d, d);
        }

        // 金属袋口
        using (var rim = new Pen(Color.FromArgb(255, 120, 139, 164), Math.Max(2f, size * 0.035f)))
            g.DrawEllipse(rim, cx - hole / 2f - 3, cy - hole / 2f - 3, hole + 6, hole + 6);

        using (var accentRim = new Pen(Color.FromArgb(150, accent), Math.Max(1f, size * 0.012f)))
            g.DrawArc(accentRim, cx - hole / 2f - 5, cy - hole / 2f - 5, hole + 10, hole + 10, 200, 140);

        // 袋口内部
        using (var holeBrush = new SolidBrush(Color.FromArgb(255, 2, 4, 8)))
            g.FillEllipse(holeBrush, cx - hole / 2f, cy - hole / 2f, hole, hole);

        using (var inner = new Pen(Color.FromArgb(120, 0, 0, 0), Math.Max(2f, size * 0.02f)))
            g.DrawEllipse(inner, cx - hole / 2f + 2, cy - hole / 2f + 2, hole - 4, hole - 4);

        using (var shine = new Pen(Color.FromArgb(90, 200, 235, 255), Math.Max(1f, size * 0.01f)))
            g.DrawArc(shine, cx - hole / 2f + 5, cy - hole / 2f + 5, hole - 10, hole - 10, 205, 70);

        return bitmap;
    }

    public static Bitmap CreateGlow(int diameter, Color color)
    {
        var bitmap = new Bitmap(diameter, diameter, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        const int layers = 26;
        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)(layers - 1);
            float d = diameter * (1f - t * 0.88f);
            int alpha = 4 + (int)(10 * t);
            using var brush = new SolidBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            g.FillEllipse(brush, (diameter - d) / 2f, (diameter - d) / 2f, d, d);
        }

        return bitmap;
    }

    public static Bitmap CreateVignette(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);

        int vertical = Math.Max(60, (int)(height * 0.24f));
        int horizontal = Math.Max(80, (int)(width * 0.18f));

        using (var top = new LinearGradientBrush(
                   new Rectangle(0, 0, width, vertical),
                   Color.FromArgb(120, 0, 0, 0),
                   Color.FromArgb(0, 0, 0, 0),
                   LinearGradientMode.Vertical))
            g.FillRectangle(top, 0, 0, width, vertical);

        using (var bottom = new LinearGradientBrush(
                   new Rectangle(0, height - vertical, width, vertical),
                   Color.FromArgb(0, 0, 0, 0),
                   Color.FromArgb(130, 0, 0, 0),
                   LinearGradientMode.Vertical))
            g.FillRectangle(bottom, 0, height - vertical, width, vertical);

        using (var left = new LinearGradientBrush(
                   new Rectangle(0, 0, horizontal, height),
                   Color.FromArgb(100, 0, 0, 0),
                   Color.FromArgb(0, 0, 0, 0),
                   LinearGradientMode.Horizontal))
            g.FillRectangle(left, 0, 0, horizontal, height);

        using (var right = new LinearGradientBrush(
                   new Rectangle(width - horizontal, 0, horizontal, height),
                   Color.FromArgb(0, 0, 0, 0),
                   Color.FromArgb(110, 0, 0, 0),
                   LinearGradientMode.Horizontal))
            g.FillRectangle(right, width - horizontal, 0, horizontal, height);

        return bitmap;
    }

    private static RectangleF CenterSquare(int width, int height)
    {
        float side = Math.Min(width, height);
        return new RectangleF((width - side) / 2f, (height - side) / 2f, side, side);
    }
}
