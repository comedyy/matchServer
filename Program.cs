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

        while (true) // 最低50毫秒一个循环
        {
            _netProcessor.OnUpdate(msTick / 1000f);

            // frame
            var logicMs = watch.ElapsedMilliseconds - msEndOfFrame;
            var sleepMs = logicMs > 50 ? 0 : 50 - logicMs;  // 如果逻辑处理超过50ms，不sleep了，确保20帧每秒
            Thread.Sleep((int)sleepMs);

            msTick = watch.ElapsedMilliseconds - msEndOfFrame;
            msEndOfFrame = watch.ElapsedMilliseconds;
        }

        Console.WriteLine(" ---------------server end------------------ ");
        Console.ReadLine();
    }

    private static void Init()
    {
        _netProcessor = new NetProcessor();
    }
}
