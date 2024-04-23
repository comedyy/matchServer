using System.Diagnostics;

public class ProfilerTick
{
    public static bool EnableProfiler{get;set;}
    Stopwatch _watch;
    int _logicTime = 0;
    string _profilerName;

    int _beginTime = 0;

    public ProfilerTick(string name)
    {
        _profilerName = name;
        _watch = new Stopwatch();
        _watch.Start();
    }

    public void BeginTick()
    {
        _beginTime = (int)_watch.ElapsedMilliseconds;
    }

    public void EndTick(Func<string> value = null)
    {
        AddTick((int)_watch.ElapsedMilliseconds - _beginTime, value);
    }

    public void AddTick(int logicMs, Func<string> value = null)
    {
        if(!EnableProfiler) return;

        _logicTime += logicMs;
        if(_watch.ElapsedMilliseconds > 5000) // 5秒tick一次
        {
            Console.WriteLine($"【PROFILER】[{_profilerName}] average timepercent = {1.0f * _logicTime / _watch.ElapsedMilliseconds:0.0%} info:【{value?.Invoke()}】");
            _watch.Restart();
            _logicTime = 0;
        }
    }
}