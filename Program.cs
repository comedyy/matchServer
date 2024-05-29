using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Program
{
    static NetProcessor _netProcessor;
    static Thread _thread;
    static bool NeedStop;
    static int mainThreadSleepTime;
    const string appConfigFolder = "__appconfig";

    static void Main(string[] args)
    {
        CreaetAppConfigFolder();
        Init();

        ThreadStart threadStart = ProcessLogic;
        _thread = new Thread(threadStart)
        {
            Name = "____ROOM_PROCESS"
        };
        _thread.Start();

        Console.WriteLine(" ---------------server start------------------ ");

        while(!NeedStop)
        {
            var line =  Console.ReadLine();
            if(line != null)
            {
                ProcessGM(line);
            }

            Thread.Sleep(mainThreadSleepTime);
            mainThreadSleepTime = 100;
        }

        Console.WriteLine(" ---------------server end------------------ ");
    }

    private static void CreaetAppConfigFolder()
    {
        if(!Directory.Exists(appConfigFolder))
        {
            Directory.CreateDirectory(appConfigFolder);
        }
    }

    private static void ProcessGM(string line)
    {
        Console.WriteLine($"input command 【{line}】");
        try
        {
            if(line == "exit")
            {
                NeedStop = true;
            }
            else if(line == "reload")
            {

            }
            else if(line == "info")
            {
                ProfilerTick.EnableProfiler = !ProfilerTick.EnableProfiler;
            }
            else if(line == "wait")
            {
                mainThreadSleepTime = 10000;
            }
            else if(line == "gc")
            {
                GC.Collect();
            }
            else if(line == "help")
            {
                Console.WriteLine(@"
                exit: 退出
                reload: 加载配置
                info:打开profiler
                wait:暂停应用程序输入10秒,这个时候可以用于输入ssh输入。
                gc: 内存回收
                ");
            }
            else 
            {
                Console.WriteLine("invalid input【{line}】");
            }
        }
        catch(Exception e)
        {
            Console.WriteLine("==== ERRROR ====" + e.Message + " " + e.StackTrace);   
        }
    }

    private static void ProcessLogic()
    {
        Stopwatch watch = new Stopwatch();
        watch.Start();

        var msEndOfFrame = watch.ElapsedMilliseconds;
        var msTick = 0L;
        int frame = 0;

        ProfilerTick _watch = new ProfilerTick("Program");

        while (!NeedStop) // 最低50毫秒一个循环
        {
            frame ++;
            var targetMs = frame * 50;

            try
            {
                _netProcessor.OnUpdate(msTick / 1000f);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message + " " + e.StackTrace); 
            }

            var logicTime = (int)(watch.ElapsedMilliseconds - msEndOfFrame);
            _watch.AddTick(logicTime, _netProcessor.GetStatus);

            while(watch.ElapsedMilliseconds < targetMs)
            {
                Thread.Sleep(1);
            }

            msTick = watch.ElapsedMilliseconds - msEndOfFrame;
            msEndOfFrame = watch.ElapsedMilliseconds;

            if(frame % 1000 == 0 && _netProcessor != null)
            {
                try
                {
                    File.WriteAllText(roomTxt, _netProcessor.RoomId.ToString());
                }
                catch(Exception e)
                {
                    // nothing
                    Console.WriteLine("write room id Exception " + e.Message);
                }
            }
        }
    }

    static Dictionary<string, int> _dicConfigs;
    private static void Init()
    { 
        LogFileWriter.Init();

        var port = GetConfigId("port", 5000);
        int uniqueIdBegin = GetConfigId("uniqueIdBegin", 40000);
        int uniqueIdEnd = GetConfigId("uniqueIdEnd", 50000);

        var initRoomId = GetInitRoomId();
        _netProcessor = new NetProcessor(new GameServerSocket(1000, port, RoomMsgVersion.version), initRoomId, new KeyValuePair<int, int>(uniqueIdBegin, uniqueIdEnd));
    }
    
    static int GetConfigId(string name, int defaultValue)
    {
        if(_dicConfigs == null)
        {
            _dicConfigs = new Dictionary<string, int>();
            var configPath = $"{appConfigFolder}/appConfig.txt";
            if(File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach(var x in lines)
                {
                    var args = x.Split(",", StringSplitOptions.RemoveEmptyEntries);
                    if(args.Length < 2) continue;

                    _dicConfigs[args[0]] = int.Parse(args[1]);

                    Console.WriteLine($"Load {x}");
                }
            }
            else
            {
                Console.WriteLine("Not found appConfig.txt");
                File.WriteAllText(configPath, "port,5000");
            }
        }

        int value;
        if (_dicConfigs.TryGetValue(name, out value))
        {
            Console.WriteLine($"LoadedConfig {name} - {value}");
        } 
        else
        {
            value = defaultValue;
            Console.WriteLine($"LoadedConfig {name} NotFound, default： - {value}");
        }

        return value;
    }

    static string roomTxt = $"{appConfigFolder}/roomId.txt";
    private static int GetInitRoomId()
    {
        try
        {
            if(!File.Exists(roomTxt))
            {
                Console.WriteLine("Not Found RoomIdConfig, Init:0");
                return 0;
            }

            var id = int.Parse(File.ReadAllText(roomTxt));
            var targetId = id + 100000;
            if(targetId > int.MaxValue)
            {
                targetId = 100000;
            }

            Console.WriteLine($"get room id from config {id}, Set room init id = {targetId}");
            return targetId;
        }
        catch(Exception e)
        {
            Console.WriteLine("Get Room Id Exception "+ e.Message);
            return 0;
        }
    }
}
