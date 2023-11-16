using System.Diagnostics;

public class ProfilerTick
{
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

    public void EndTick()
    {
        AddTick((int)_watch.ElapsedMilliseconds - _beginTime);
    }

    public void AddTick(int logicMs, Func<string> value = null)
    {
        _logicTime += logicMs;
        if(_watch.ElapsedMilliseconds > 5000) // 5秒tick一次
        {
            Console.WriteLine($"【PROFILER】[{_profilerName}] average time = {1.0f * _logicTime / _watch.ElapsedMilliseconds:0.000} info:【{value?.Invoke()}】");
            _watch.Restart();
            _logicTime = 0;
        }
    }
}