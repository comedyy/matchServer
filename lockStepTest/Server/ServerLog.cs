
using System;
using System.IO;

public class ServerLog
{
    private static readonly object DebugLogLock = new object();
    static string _logPath;
    public static void Init()
    {
        if(!Directory.Exists("log"))
        {
            Directory.CreateDirectory("log");
        }

        _logPath = $"log/error.log";
        File.WriteAllText(_logPath, "init");

        Console.WriteLine($"log path:[{_logPath}]");
    }

    internal static void WriteLog(string str, params object[] args)
    {
#if UNITY_EDITOR
        var content = $"\n{DateTime.Now} {str} {string.Join(",", args)}";
        UnityEngine.Debug.LogError(content);
#else
        lock(DebugLogLock)
        {
            var content = $"\n{DateTime.Now} {str} {string.Join(",", args)}";
            File.AppendAllText(_logPath, content);
            Console.WriteLine(content);
        }
#endif
    }
}