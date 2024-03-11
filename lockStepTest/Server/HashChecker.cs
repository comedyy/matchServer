using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

class HashCompareItem
{
    public int hash;
    public int hashIndex;
    public int frame;
    public List<int> whos = new List<int>();
}

public class HashChecker
{
    const int MAX_HASH_COUNT = 32;
    List<HashCompareItem> _allHashCompare;


    public HashChecker(int maxCount)
    {
        _allHashCompare = new List<HashCompareItem>();
        for(int i = 0; i < MAX_HASH_COUNT; i++)
        {
            _allHashCompare.Add(new HashCompareItem());
        }
    }

    public string[] AddHash(FrameHash hash)
    {
        return CheckHashNew(hash);
    }

    private string[] CheckHashNew(FrameHash hash)
    {
        GuaranteeSize(hash);

        var currentHashIndex = hash.hashIndex;
        var lastIndex = _allHashCompare.Last().hashIndex;
        if(currentHashIndex > lastIndex)
        {
            Console.WriteLine("inner error");
            return null;
        }

        if(currentHashIndex < lastIndex) return null;  // 过期
        else if(lastIndex >= currentHashIndex)         // 在cache中
        {
            var diff = lastIndex - currentHashIndex;
            var compareIndex = _allHashCompare.Count - 1 - diff;
            var compare = _allHashCompare[compareIndex];

            if(compare.hash == 0)
            {
                compare.frame = hash.frame;
                compare.hash = hash.hash;
            }
            else
            {
                if(compare.hashIndex != hash.hashIndex)
                {
                    // error
                    Console.WriteLine("error here compare.hashIndex != hash.hashIndex");
                    return null;
                }

                if(compare.hash != hash.hash)
                {
                    return new string[]{$"unsync in frame {hash.frame}"};
                }
            }
        }

        return null;
    }

    private void GuaranteeSize(FrameHash hash)
    {
        var currentHashIndex = hash.hashIndex;
        var lastIndex = _allHashCompare.Last().hashIndex;
        if(currentHashIndex <= lastIndex) return;

        var diff = currentHashIndex - lastIndex;
        for(int i = 0; i < diff; i++)
        {
            var x = _allHashCompare[0];
            _allHashCompare.RemoveAt(0);
            _allHashCompare.Add(x);
            x.hashIndex = i + 1 + lastIndex;
            x.frame = 0;
            x.hash = 0;
        }
    }
}