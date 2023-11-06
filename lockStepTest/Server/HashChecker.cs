using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

struct NotSameStruct
{
    public string name;
    public int index;
}

public class HashChecker
{
    Dictionary<int, FrameHash>[] _listHash;
    List<int> notSameIndexs = null;

    int notSamePosIndex{get; set;} = -1;
    int notSameHpIndex{get; set;} = -1;
    int notSameFindTargetIndex{get; set;} = -1;
    int notSamePreRvoIndex{get; set;} = -1;
    public bool NotSame => notSameIndexs != null && notSameIndexs.Any(m=>m != -1);

    public HashChecker(int maxCount)
    {
        _listHash = new Dictionary<int, FrameHash>[maxCount];
        for(int i = 0; i < _listHash.Length; i++)
        {
            _listHash[i] = new Dictionary<int, FrameHash>();
        }
    }

    public void AddHash(FrameHash hash)
    {
        if(NotSame) return;

        var id = hash.id;
        var list = _listHash[id];
        list.Add(hash.frame, hash);

        CheckHash(hash.frame);
    }

    private void CheckHash(int frame)
    {
        int count = _listHash.Count(m=>m.ContainsKey(frame));
        if(count == _listHash.Length)
        {
            CheckIndex(frame);
        }
    }

    private void CheckIndex(int v)
    {
        List<FrameHash> list = new List<FrameHash>();
        foreach(var x in _listHash)
        {
            list.Add(x[v]);
            FrameHash.Pool.Enqueue(x[v].allHashItems); // 回收
            x.Remove(v);

//            Debug.LogError(string.Join(",", list[0].allHashItems.Select(m=>m.hash)));
        }

        var first = list[0];
        if(first.allHashItems == null) return;

        int hashCategoryCount = first.allHashItems.Length;
        if(notSameIndexs == null)
        {
            notSameIndexs = new List<int>();
            for(int i = 0; i < hashCategoryCount; i++) notSameIndexs.Add(-1);
        }

        for(int i = 0; i < hashCategoryCount; i++)
        {
            var isSame = list.All(m=>m.allHashItems[i] == first.allHashItems[i]);
            if(!isSame && notSameIndexs[i] < 0)
            {
                notSameIndexs[i] = v;
            }
        }

        if(NotSame)
        {
            var str = string.Join(",", notSameIndexs.Select((m, i)=>{
                return ((CheckSumType)first.allHashItems[i].hashType, m);
            }));
            Console.WriteLine($"发现不一致的问题, {str}");

            // for(int i = 0; i < hashCategoryCount; i++)
            // {
            //     if(notSameIndexs[i] == -1) continue;

            //     var catergoryType = (CheckSumType)first.allHashItems[i].hashType;
            //     if(catergoryType == CheckSumType.pos || catergoryType == CheckSumType.dropPos || catergoryType == CheckSumType.preMovePosition || catergoryType == CheckSumType.preRvoPosition
            //         || catergoryType == CheckSumType.createMonster)
            //     {
            //         EchoPosIndex(list.Select(m=>m.allHashItems[i]), catergoryType);
            //     }
            //     else
            //     {
            //         EchoIndex(list.Select(m=>m.allHashItems[i]), catergoryType);
            //     }
            // }

            if(list.Count > 1)
            {
                for(int i = 0; i < list.Count; i++)
                {
                    WriteUnsyncToFile(list[i].allHashItems, 0, $"hashChecker_{i}.log", null);
                }
            }

            // if(notSamePosIndex >= 0) EchoPosIndex(list.Select(m=>m.hashPos), "Pos");
            // if(notSameHpIndex >= 0) EchoIndex(list.Select(m=>m.hashHp), "Hp");
            // if(notSameFindTargetIndex >= 0) EchoIndex(list.Select(m=>m.hashFindtarget), "FindTarget");
            // if(notSamePreRvoIndex >= 0) EchoIndex(list.Select(m=>m.preRvo), "preRvo");
        }
    }

    public static void WriteUnsyncToFile(FrameHashItem[] allHashDetails, float escaped, string logFile, List<string> symbol)
    {
        if(allHashDetails != null)
        {
            var list = new List<string>();
            var hashTypeCount = allHashDetails.Length;
            for(var hashTypeIndex = 0; hashTypeIndex < hashTypeCount; hashTypeIndex ++)
            {
                var hashItem = allHashDetails[hashTypeIndex];
                list.Add($"时间{escaped}【{hashItem.GetString(symbol)}】");
            }

            File.WriteAllText("d://" + logFile, string.Join("\n", list) + "\n");
        }
    }
}