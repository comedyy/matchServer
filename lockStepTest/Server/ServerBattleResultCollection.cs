
using System;
using System.Collections.Generic;

struct BattleResultInfo
{
    public double addTime;
    public int battleId;
    public int result;
}

public class ServerBattleResultCollection
{
    Queue<BattleResultInfo> _queue = new Queue<BattleResultInfo>();
    Dictionary<int, int> _query = new Dictionary<int, int>();
    public void AddBattleResult(int battleId, int result, double time)
    {
        _queue.Enqueue(new BattleResultInfo(){
            battleId = battleId, addTime = time, result = result
        });

        _query.Add(battleId, result);
    }

    public bool GetResult(int battleId, out int result)
    {
        return _query.TryGetValue(battleId, out result);
    }

    double _lastUpdateTime = 0;
    public void Update(double timeNow)
    {
        if(_queue.Count == 0) return;
        if(timeNow - _lastUpdateTime < 5) return;

        _lastUpdateTime = timeNow;

        var checkCount = Math.Min(100, _queue.Count);
        var removeCount = 0;
        for(int i = 0; i < checkCount; i++)
        {
            var x = _queue.Peek();
            if(timeNow > x.addTime + 60)
            {
                break;
            }

            _queue.Dequeue();
            _query.Remove(x.battleId);
            removeCount++;
        }

#if UNITY_EDITOR || UNITY_IOS || UNITY_ANDROID
#else
        if(removeCount > 0 && ProfilerTick.EnableProfiler)
        {
            Console.WriteLine($"clear battle result count: {removeCount}");
        }
#endif
    }
}