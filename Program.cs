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
    static void Main(string[] args)
    {
        Init();
        // NetMgr.Instance.Init();

        Stopwatch watch = new Stopwatch();
        watch.Start();

        var msEndOfFrame = watch.ElapsedMilliseconds;
        var msTick = 0L;
        int frame = 0;

        ProfilerTick _watch = new ProfilerTick("Program");

        while (true) // 最低50毫秒一个循环
        {
            frame ++;
            var targetMs = frame * 50;
            _netProcessor.OnUpdate(msTick / 1000f);

            var logicTime = (int)(watch.ElapsedMilliseconds - msEndOfFrame);
            _watch.AddTick(logicTime);

            while(watch.ElapsedMilliseconds < targetMs)
            {
                Thread.Sleep(1);
            }

            msTick = watch.ElapsedMilliseconds - msEndOfFrame;
            msEndOfFrame = watch.ElapsedMilliseconds;
        }

        Console.WriteLine(" ---------------server end------------------ ");
        Console.ReadLine();
    }

    private static void Init()
    {
        _netProcessor = new NetProcessor(new GameServerSocket(1000));
    }
}
