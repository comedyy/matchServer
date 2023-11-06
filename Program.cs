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

        float perTime = 0;
        Stopwatch watch = new Stopwatch();
        watch.Start();
        while (true)
        {
            var currentTime = watch.ElapsedMilliseconds / 1000f;
            var delta = currentTime - perTime;
            perTime = currentTime;

            _netProcessor.OnUpdate(delta);

            Thread.Sleep(10);
        }

        Console.WriteLine(" ---------------server end------------------ ");
        Console.ReadLine();
    }

    private static void Init()
    {
        _netProcessor = new NetProcessor();
    }
}
