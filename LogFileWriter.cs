
public class LogFileWriter
{
    private static readonly object DebugLogLock = new object();
    static string _logPath;
    public static void Init()
    {
        if(!Directory.Exists("log"))
        {
            Directory.CreateDirectory("log");
        }

        _logPath = $"log/{DateTime.Now.ToString().Replace("/", "_").Replace(" ", "_").Replace(":", "_")}.log";
        File.WriteAllText(_logPath, "init");

        Console.WriteLine($"log path:[{_logPath}]");
    }

    internal static void WriteLog(string str, params object[] args)
    {
        lock(DebugLogLock)
        {
            var content = $"\n{DateTime.Now} {str} {string.Join(",", args)}";
            File.AppendAllText(_logPath, content);
            Console.WriteLine(content);
        }
    }
}