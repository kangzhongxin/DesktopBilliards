using System.IO;

namespace DesktopBilliards;

internal static class Log
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(Path.GetTempPath(), "DesktopBilliards.log");

    public static void Reset()
    {
        try
        {
            File.WriteAllText(FilePath, "");
        }
        catch
        {
            // 日志失败不影响游戏。
        }
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志失败不影响游戏。
        }
    }
}
