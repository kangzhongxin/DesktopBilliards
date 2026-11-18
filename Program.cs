namespace DesktopBilliards;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Write("ThreadException: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Write("UnhandledException: " + e.ExceptionObject);

        Log.Reset();
        Log.Write("Program.Main start");

        if (args.Any(a => string.Equals(a, "--probe", StringComparison.OrdinalIgnoreCase)))
        {
            DesktopCapture.WriteProbe();
            return;
        }

        try
        {
            using var snapshot = DesktopCapture.Capture();
            Log.Write($"Capture complete: work={snapshot.WorkArea} icons={snapshot.Icons.Count}");
#if DEBUG
            try
            {
                snapshot.Background.Save(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopBilliards-bg.png"),
                    System.Drawing.Imaging.ImageFormat.Png);
            }
            catch
            {
                // 调试截图失败不影响游戏。
            }
#endif

            bool demo = args.Any(a => string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase));
            bool demoFill = args.Any(a => string.Equals(a, "--fill", StringComparison.OrdinalIgnoreCase));
            var form = new GameForm(snapshot, demo, demoFill);
            Log.Write($"GameForm constructed demo={demo} fill={demoFill}");

            Application.Run(form);
            Log.Write("Application.Run returned");
        }
        catch (Exception ex)
        {
            Log.Write("FATAL: " + ex);

            MessageBox.Show(
                "桌面台球启动失败：\n\n" +
                ex.Message +
                "\n\n提示：请用普通用户权限运行，并确保桌面图标可见。",
                "Desktop Billiards",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
