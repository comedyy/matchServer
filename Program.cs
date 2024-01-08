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

    static void Main(string[] args)
    {
        Init();
        // NetMgr.Instance.Init();

        ThreadStart threadStart = ProcessLogic;
        _thread = new Thread(threadStart);
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
                mainThreadSleepTime = 5000;
            }
            else if(line == "help")
            {
                Console.WriteLine(@"
                exit: 退出
                reload: 加载配置
                info:打开profiler
                wait:暂停应用程序输入5秒
                ");
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
            _netProcessor.OnUpdate(msTick / 1000f);

            var logicTime = (int)(watch.ElapsedMilliseconds - msEndOfFrame);
            _watch.AddTick(logicTime, _netProcessor.GetStatus);

            while(watch.ElapsedMilliseconds < targetMs)
            {
                Thread.Sleep(1);
            }

            msTick = watch.ElapsedMilliseconds - msEndOfFrame;
            msEndOfFrame = watch.ElapsedMilliseconds;
        }
    }

    private static void Init()
    {
        _netProcessor = new NetProcessor(new GameServerSocket(1000));
    }
}
