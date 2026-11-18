using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DesktopBilliards;

internal sealed class IconShot
{
    public IconShot(Rectangle screenRect, Bitmap image)
    {
        ScreenRect = screenRect;
        Image = image;
    }

    public Rectangle ScreenRect { get; }
    public Bitmap Image { get; }
}

internal sealed class DesktopSnapshot : IDisposable
{
    public DesktopSnapshot(Rectangle workArea, Bitmap background, List<IconShot> icons)
    {
        WorkArea = workArea;
        Background = background;
        Icons = icons;
    }

    public Rectangle WorkArea { get; }
    public Bitmap Background { get; }
    public IReadOnlyList<IconShot> Icons { get; }

    public void Dispose()
    {
        Background.Dispose();
        foreach (var icon in Icons)
            icon.Image.Dispose();
    }
}

/// <summary>
/// 通过 PrintWindow 直接渲染桌面图标列表，因此即使图标被终端/浏览器挡住也能拿到图标图片。
/// 背景优先从 Windows 壁纸文件渲染，避免把其他窗口截进游戏里。
/// </summary>
internal static class DesktopCapture
{
    public static DesktopSnapshot Capture()
    {
        Log.Write("Capture: begin");
        var result = CaptureCore();
        Log.Write("Capture: complete");
        return result;
    }

    private static DesktopSnapshot CaptureCore()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var work = screen.WorkingArea;
        Log.Write($"CaptureCore: screen={screen.DeviceName} work={work}");

        if (work.Width < 320 || work.Height < 240)
            throw new InvalidOperationException("当前显示器工作区太小，无法开始游戏。");

        var icons = new List<IconShot>();
        var listView = FindDesktopListView();
        Log.Write($"CaptureCore: listView=0x{listView.ToInt64():X}");

        if (listView != IntPtr.Zero && NativeMethods.IsWindowVisible(listView))
        {
            int count = GetItemCount(listView);
            Log.Write($"CaptureCore: itemCount={count}");

            if (count > 0)
            {
                try
                {
                    // 直接渲染桌面图标列表：不受其他窗口遮挡影响。
                    using var iconCanvas = RenderListView(listView);
                    Log.Write($"CaptureCore: iconCanvas={iconCanvas.Width}x{iconCanvas.Height}");

                    var origin = GetClientOrigin(listView);
                    var canvasBounds = new Rectangle(0, 0, iconCanvas.Width, iconCanvas.Height);

                    using var reader = new RemoteIconReader(listView);

                    for (int i = 0; i < count; i++)
                    {
                        if (!reader.TryGetIconRect(i, out var rect))
                            continue;

                        var screenRect = Rectangle.FromLTRB(
                            origin.X + rect.Left,
                            origin.Y + rect.Top,
                            origin.X + rect.Right,
                            origin.Y + rect.Bottom);

                        if (!screenRect.IntersectsWith(work))
                            continue;

                        var iconRect = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                        var cropRect = Rectangle.Intersect(iconRect, canvasBounds);

                        if (cropRect.Width < 8 || cropRect.Height < 8)
                            continue;

                        icons.Add(new IconShot(
                            screenRect,
                            iconCanvas.Clone(cropRect, PixelFormat.Format32bppArgb)));
                    }
                }
                catch (Exception ex)
                {
                    // 图标读取失败时仍然可以开局：不足 15 个的部分用生成球补足。
                    Log.Write("CaptureCore: icon read failed: " + ex);
                }
            }
        }
        else
        {
            Log.Write("CaptureCore: desktop listview unavailable; will generate rack balls");
        }

        Log.Write($"CaptureCore: icons read={icons.Count}");

        // 背景优先用壁纸文件渲染；失败时才退回截屏。
        Bitmap? background = WallpaperRenderer.TryCreate(work);

        if (background != null)
        {
            Log.Write("CaptureCore: rendered clean wallpaper background");
        }
        else if (listView != IntPtr.Zero && NativeMethods.IsWindowVisible(listView))
        {
            try
            {
                Log.Write("CaptureCore: wallpaper unavailable, hiding icons");
                NativeMethods.ShowWindow(listView, NativeMethods.SW_HIDE);
                Thread.Sleep(320);
                NativeMethods.DwmFlush();
                Log.Write("CaptureCore: capturing background");

                background = new Bitmap(work.Width, work.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(background))
                    g.CopyFromScreen(work.Left, work.Top, 0, 0, work.Size, CopyPixelOperation.SourceCopy);
            }
            finally
            {
                NativeMethods.ShowWindow(listView, NativeMethods.SW_SHOW);
                NativeMethods.DwmFlush();
                Log.Write("CaptureCore: icons shown");
            }
        }
        else
        {
            background = new Bitmap(work.Width, work.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(background);
            g.CopyFromScreen(work.Left, work.Top, 0, 0, work.Size, CopyPixelOperation.SourceCopy);
        }

        return new DesktopSnapshot(work, background, icons);
    }

    private static Bitmap RenderListView(IntPtr listView)
    {
        if (!NativeMethods.GetClientRect(listView, out var client))
            throw new InvalidOperationException("无法读取桌面图标区域的大小。");

        int width = Math.Max(1, client.Right - client.Left);
        int height = Math.Max(1, client.Bottom - client.Top);

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        try
        {
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.Black);

            var hdc = g.GetHdc();
            bool printed;

            try
            {
                printed = NativeMethods.PrintWindow(listView, hdc, 2); // PW_RENDERFULLCONTENT
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }

            if (!printed)
                throw new InvalidOperationException("无法渲染桌面图标。请重启 Windows Explorer 后再试。");

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 只读诊断：不截图、不隐藏图标，把 listview 和各图标矩形写入 probe.txt。
    /// 用于确认当前 Windows 版本的桌面图标接口是否可用。
    /// </summary>
    public static void WriteProbe()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "probe.txt");
        var lines = new List<string>();

        try
        {
            var listView = FindDesktopListView();
            lines.Add($"listview=0x{listView.ToInt64():X}");

            if (listView == IntPtr.Zero)
            {
                lines.Add("ERROR: desktop listview not found");
            }
            else
            {
                lines.Add($"visible={NativeMethods.IsWindowVisible(listView)}");
                lines.Add($"count={GetItemCount(listView)}");

                var origin = GetClientOrigin(listView);
                lines.Add($"clientOrigin={origin.X},{origin.Y}");

                using var reader = new RemoteIconReader(listView);
                int count = GetItemCount(listView);
                int ok = 0;
                int minX = int.MaxValue, minY = int.MaxValue;
                int maxX = int.MinValue, maxY = int.MinValue;

                for (int i = 0; i < count && i < 200; i++)
                {
                    if (reader.TryGetIconRect(i, out var rect))
                    {
                        ok++;
                        minX = Math.Min(minX, rect.Left);
                        minY = Math.Min(minY, rect.Top);
                        maxX = Math.Max(maxX, rect.Right);
                        maxY = Math.Max(maxY, rect.Bottom);

                        lines.Add(
                            $"[{i}] {rect.Left},{rect.Top},{rect.Right},{rect.Bottom} " +
                            $"size={rect.Right - rect.Left}x{rect.Bottom - rect.Top}");
                    }
                    else
                    {
                        lines.Add($"[{i}] FAIL");
                    }
                }

                lines.Add($"ok={ok}");

                if (ok > 0 && NativeMethods.GetClientRect(listView, out var client))
                {
                    int width = Math.Max(1, client.Right - client.Left);
                    int height = Math.Max(1, client.Bottom - client.Top);
                    lines.Add($"clientRect={client.Left},{client.Top},{client.Right},{client.Bottom}");

                    using var rendered = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(rendered))
                    {
                        g.Clear(Color.Black);
                        var hdc = g.GetHdc();
                        bool printed = NativeMethods.PrintWindow(listView, hdc, 2);
                        g.ReleaseHdc(hdc);
                        lines.Add($"printWindow={printed}");
                    }

                    int padding = 40;
                    int cropX = Math.Max(0, minX - padding);
                    int cropY = Math.Max(0, minY - padding);
                    int cropW = Math.Min(width - cropX, maxX - minX + padding * 2);
                    int cropH = Math.Min(height - cropY, maxY - minY + padding * 2);

                    if (cropW > 0 && cropH > 0)
                    {
                        using var crop = rendered.Clone(
                            new Rectangle(cropX, cropY, cropW, cropH),
                            PixelFormat.Format32bppArgb);
                        crop.Save(
                            Path.Combine(AppContext.BaseDirectory, "listview.png"),
                            ImageFormat.Png);
                        lines.Add($"saved=listview.png crop={cropX},{cropY},{cropW},{cropH}");
                    }
                }
            }

            var screen = Screen.FromPoint(Cursor.Position);
            lines.Add($"screen={screen.DeviceName} work={screen.WorkingArea}");

            using var wallpaper = WallpaperRenderer.TryCreate(screen.WorkingArea);
            lines.Add(wallpaper == null
                ? "wallpaper=FAILED"
                : $"wallpaper={wallpaper.Width}x{wallpaper.Height}");
        }
        catch (Exception ex)
        {
            lines.Add("ERROR: " + ex);
        }

        File.WriteAllLines(path, lines);
    }

    private static int GetItemCount(IntPtr listView)
    {
        NativeMethods.SendMessageTimeout(
            listView,
            NativeMethods.LVM_GETITEMCOUNT,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG,
            500,
            out var result);

        return (int)result;
    }

    private static Point GetClientOrigin(IntPtr listView)
    {
        var pt = new NativeMethods.POINT();
        NativeMethods.ClientToScreen(listView, ref pt);
        return new Point(pt.X, pt.Y);
    }

    private static IntPtr FindDesktopListView()
    {
        IntPtr defView = IntPtr.Zero;
        var progman = NativeMethods.FindWindow("Progman", null);

        if (progman != IntPtr.Zero)
            defView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (defView == IntPtr.Zero)
        {
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                var child = NativeMethods.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (child == IntPtr.Zero)
                    return true;

                defView = child;
                return false;
            }, IntPtr.Zero);
        }

        return defView == IntPtr.Zero
            ? IntPtr.Zero
            : NativeMethods.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    /// <summary>
    /// 用 LVM_GETITEMRECT 读取图标区域。LVM_* 消息不会跨进程封送指针，
    /// 所以 RECT 必须分配在 Explorer 进程里。
    /// </summary>
    private sealed class RemoteIconReader : IDisposable
    {
        private readonly IntPtr _listView;
        private readonly IntPtr _process;
        private readonly IntPtr _remoteBuffer;
        private bool _disposed;

        public RemoteIconReader(IntPtr listView)
        {
            _listView = listView;
            NativeMethods.GetWindowThreadProcessId(listView, out var processId);

            _process = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_VM_OPERATION |
                NativeMethods.PROCESS_VM_READ |
                NativeMethods.PROCESS_VM_WRITE |
                NativeMethods.PROCESS_QUERY_INFORMATION,
                false,
                processId);

            if (_process == IntPtr.Zero)
                throw new InvalidOperationException(
                    "无法读取 Windows Explorer 进程。请用普通用户权限运行，不要以管理员身份启动。");

            _remoteBuffer = NativeMethods.VirtualAllocEx(
                _process,
                IntPtr.Zero,
                64,
                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                NativeMethods.PAGE_READWRITE);

            if (_remoteBuffer == IntPtr.Zero)
            {
                NativeMethods.CloseHandle(_process);
                throw new InvalidOperationException("无法在 Explorer 进程中分配临时内存。");
            }
        }

        public bool TryGetIconRect(int index, out NativeMethods.RECT rect)
        {
            rect = default;
            if (_disposed)
                return false;

            var input = new NativeMethods.RECT { Left = NativeMethods.LVIR_ICON };
            if (!NativeMethods.WriteProcessMemory(
                    _process,
                    _remoteBuffer,
                    ref input,
                    (nuint)Marshal.SizeOf<NativeMethods.RECT>(),
                    out _))
                return false;

            var sent = NativeMethods.SendMessageTimeout(
                _listView,
                NativeMethods.LVM_GETITEMRECT,
                (IntPtr)index,
                _remoteBuffer,
                NativeMethods.SMTO_ABORTIFHUNG,
                300,
                out var result);

            if (sent == IntPtr.Zero || result == IntPtr.Zero)
                return false;

            if (!NativeMethods.ReadProcessMemory(
                    _process,
                    _remoteBuffer,
                    out rect,
                    (nuint)Marshal.SizeOf<NativeMethods.RECT>(),
                    out _))
                return false;

            return rect.Right > rect.Left && rect.Bottom > rect.Top;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_remoteBuffer != IntPtr.Zero)
                NativeMethods.VirtualFreeEx(_process, _remoteBuffer, 0, NativeMethods.MEM_RELEASE);

            if (_process != IntPtr.Zero)
                NativeMethods.CloseHandle(_process);
        }
    }
}
