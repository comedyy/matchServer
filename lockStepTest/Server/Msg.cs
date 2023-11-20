using System;
using System.Collections.Generic;
using System.Linq;
using Game;
using LiteNetLib.Utils;

public enum MsgType1 : byte
{
    StartBattle = 1,
    FrameMsg = 2,
    ServerFrameMsg = 3,
    HashMsg = 4,
    ReadyForNextStage = 5,
    ServerReadyForNextStage = 6,
    PauseGame = 7,
    FinishCurrentStage = 9,         // 完成当前的stage小关
    ServerEnterLoading = 10,  // 完成当前的stage小关, 服务器回包
    CreateRoom = 101,
    JoinRoom = 102,
    StartRequest = 103,
    SyncRoomMemberList = 104,
    GetAllRoomList = 105,
    Unsync = 106,
    SetSpeed = 107,
}

[Serializable]
public struct FrameHashItem
{
    public byte hashType;
    public int hash;

    public List<int> listValue;
    public List<short> lstParamIndex;
    public List<string> lstParam;
    public List<int> listEntity;

    public void Write(NetDataWriter writer)
    {
        writer.Put(hashType);
        writer.Put(hash);

        if (listValue == null)
        {
            writer.Put((short)-1);
            return;
        }

        writer.Put((short)listValue.Count);
        for (int i = 0; i < listValue.Count; i++)
        {
            writer.Put(listValue[i]);
        }

        writer.Put((short)listEntity.Count);
        for (int i = 0; i < listEntity.Count; i++)
        {
            writer.Put(listEntity[i]);
        }

        if(lstParamIndex == null)
        {
            writer.Put((short)-1);
            writer.Put((short)lstParam.Count);
            for (int i = 0; i < lstParam.Count; i++)
            {
                writer.Put(lstParam[i]);
            }
        }
        else
        {
            writer.Put((short)lstParamIndex.Count);
            for (int i = 0; i < lstParamIndex.Count; i++)
            {
                writer.Put(lstParamIndex[i]);
            }
        }
    }

    public void Read(NetDataReader reader)
    {
        hashType = reader.GetByte();
        hash = reader.GetInt();

        var listCount = reader.GetShort();
        if (listCount == -1) return;

        listValue = new List<int>();
        for (int i = 0; i < listCount; i++)
        {
            listValue.Add(reader.GetInt());
        }

        var listCount1 = reader.GetShort();
        listEntity = new List<int>();
        for (int i = 0; i < listCount1; i++)
        {
            listEntity.Add(reader.GetInt());
        }

        var listCount2 = reader.GetShort();
        if(listCount2 == -1)
        {
            listCount2 = reader.GetShort();
            lstParam = new List<string>();
            for (int i = 0; i < listCount2; i++)
            {
                lstParam.Add(reader.GetString());
            }
        }
        else
        {
            lstParamIndex = new List<short>();
            for (int i = 0; i < listCount2; i++)
            {
                lstParamIndex.Add(reader.GetShort());
            }
        }
    }
    public static bool operator ==(FrameHashItem item1, FrameHashItem item2)
    {
        if (item1.hash != item2.hash) return false;

        if (item1.listEntity != null && item2.listEntity != null)
        {
            if (item1.listEntity.Count != item2.listEntity.Count) return false;
            for (int i = 0; i < item1.listEntity.Count; i++)
            {
                if (item1.listEntity[i] != item1.listEntity[i]) return false;
            }
        }

        if (item1.listValue != null && item2.listValue != null)
        {
            if (item1.listValue.Count != item2.listValue.Count) return false;
            for (int i = 0; i < item1.listValue.Count; i++)
            {
                if (item1.listValue[i] != item1.listValue[i]) return false;
            }
        }

        return true;
    }

    public static bool operator !=(FrameHashItem item1, FrameHashItem item2)
    {
        return !(item1 == item2);
    }

    public string GetString(List<string> symbol)
    {
        if (listValue != null)
        {
            if(listEntity != null && lstParamIndex != null && lstParamIndex.Count > 0 && symbol != null)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))} \n{string.Join("\n", lstParamIndex.Select(m=>symbol[m]))}";
            }
            else if(listEntity != null && lstParam != null && lstParam.Count > 0)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))} \n{string.Join("\n", lstParam)}";
            }
            else if(listEntity != null)
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n{string.Join("!", listEntity.Select(m=>(m>>16, m & 0xffff)))}";
            }
            else
            {
                return $"{(CheckSumType)hashType} Hash:{hash} \n listValue：{string.Join("!", listValue)} \n";
            }
        }
        else
        {
            return $"{(CheckSumType)hashType} Hash:{hash}";
        }
    }
}

[Serializable]
public struct FrameHash : INetSerializable
{
    public static Queue<FrameHashItem[]> Pool = new Queue<FrameHashItem[]>();
    public int frame;
    public int id;
    public int hash;
    public FrameHashItem[] allHashItems;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.HashMsg);
        writer.Put(frame);
        writer.Put(id);
        writer.Put(hash);

        int count = allHashItems == null ? 0 : allHashItems.Length;
        writer.Put(count);
        for (int i = 0; i < count; i++)
        {
            allHashItems[i].Write(writer);
        }
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frame = reader.GetInt();
        id = reader.GetInt();
        hash = reader.GetInt();

        var count = reader.GetInt();
        if (count > 0)
        {
            allHashItems = Pool.Count > 0 ? Pool.Dequeue() : new FrameHashItem[count];
            for (int i = 0; i < count; i++)
            {
                allHashItems[i] = default;
                allHashItems[i].Read(reader);
            }
        }
    }
}


public struct FinishRoomMsg : INetSerializable
{
    public int stageIndex;
    internal int id;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.FinishCurrentStage);
        writer.Put(stageIndex);
        writer.Put(id);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageIndex = reader.GetInt();
        id = reader.GetInt();
    }
}


public struct ReadyStageMsg : INetSerializable
{
    public int stageIndex;
    internal int id;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ReadyForNextStage);
        writer.Put(stageIndex);
        writer.Put(id);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageIndex = reader.GetInt();
        id = reader.GetInt();
    }
}


public struct PauseGameMsg : INetSerializable
{
    public bool pause;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.PauseGame);
        writer.Put(pause);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        pause = reader.GetBool();
    }
}

public struct ServerReadyForNextStage : INetSerializable
{
    public int stageIndex;
    public int frameIndex;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerReadyForNextStage);
        writer.Put(stageIndex);
        writer.Put(frameIndex);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        stageIndex = reader.GetInt();
        frameIndex = reader.GetInt();
    }
}


public struct ServerEnterLoading : INetSerializable
{
    public int frameIndex;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerEnterLoading);
        writer.Put(frameIndex);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frameIndex = reader.GetInt();
    }
}


public struct PackageItem : INetSerializable
{
    public MessageItem messageItem;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.FrameMsg);

        MessageItem.ToWriter(writer, messageItem);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        messageItem = MessageItem.FromReader(reader);
    }
}


public struct Pair2 : INetSerializable
{
    public uint Item1;
    public uint Item2;

    public void Deserialize(NetDataReader reader)
    {
        Item1 = reader.GetUInt();
        Item2 = reader.GetUInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Item1);
        writer.Put(Item2);
    }
}

public struct Pair3 : INetSerializable
{
    public uint Item1;
    public uint Item2;
    public uint Item3;

    public void Deserialize(NetDataReader reader)
    {
        Item1 = reader.GetUInt();
        Item2 = reader.GetUInt();
        Item3 = reader.GetUInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(Item1);
        writer.Put(Item2);
        writer.Put(Item3);
    }
}


public struct JoinMessage : INetSerializable
{
    public string name;
    public uint userId;
    public uint InstanceHeroId; // 这个是用来战斗恢复用的。
    public uint HeroId;
    public uint heroLevel;
    public uint HeroStar;
    public Pair2[] Talents;
    public Pair2[] ActiveSkills;
    public Pair3[] PassiveSkills;

    public Pair2[] AllActiveSkillsToSelect;
    public Pair2[] AllPassiveSkillsToSelect;
    public Pair2[] AllAttribute;
    
    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put(name);
        writer.Put(userId);
        writer.Put(InstanceHeroId);
        writer.Put(HeroId);
        writer.Put(heroLevel);

        SeriaizePairArray(writer, Talents);
        SeriaizePairArray(writer, ActiveSkills);
        SeriaizePairArray(writer, PassiveSkills);
        SeriaizePairArray(writer, AllActiveSkillsToSelect);
        SeriaizePairArray(writer, AllPassiveSkillsToSelect);
        SeriaizePairArray(writer, AllAttribute);
    }

    static void SeriaizePairArray(NetDataWriter writer, Pair2[] array)
    {
        if(array == null) 
        {
            writer.Put(0);
            return;
        }

        writer.Put(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].Serialize(writer);
        }
    }

    static void SeriaizePairArray(NetDataWriter writer, Pair3[] array)
    {
        if(array == null) 
        {
            writer.Put(0);
            return;
        }

        writer.Put(array.Length);
        for (int i = 0; i < array.Length; i++)
        {
            array[i].Serialize(writer);
        }
    }

    static void DeserializePairArray(NetDataReader reader, ref Pair2[] array)
    {
        var count = reader.GetInt();
        array = new Pair2[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = new Pair2()
            {
                Item1 = reader.GetUInt(),
                Item2 = reader.GetUInt()
            };
        }
    }

    static void DeserializePairArray(NetDataReader reader, ref Pair3[] array)
    {
        var count = reader.GetInt();
        array = new Pair3[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = new Pair3()
            {
                Item1 = reader.GetUInt(),
                Item2 = reader.GetUInt()
            };
        }
    }


    void INetSerializable.Deserialize(NetDataReader reader)
    {
        name = reader.GetString();
        userId = reader.GetUInt();
        InstanceHeroId = reader.GetUInt();
        HeroId = reader.GetUInt();
        heroLevel = reader.GetUInt();

        DeserializePairArray(reader, ref Talents);
        DeserializePairArray(reader, ref ActiveSkills);
        DeserializePairArray(reader, ref PassiveSkills);
        DeserializePairArray(reader, ref AllActiveSkillsToSelect);
        DeserializePairArray(reader, ref AllPassiveSkillsToSelect);
        DeserializePairArray(reader, ref AllAttribute);
    }
}

public struct BattleStartMessage : INetSerializable
{
    public uint levelId;
    public JoinMessage[] joins;
    public int battleType;
    public uint seed;
    //public uint roomSeed;       // 随机关卡房间使用的seed。
    public float cameraAspect;
    public bool isInsideGame;
    public string guid;
    public bool autoPopupSkillWin;
    public float tick;
    public int[] ChallengeSlotIds; // 挑战关卡的slots。

    public uint[] WheelEnhanceIds;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.StartBattle);
        writer.Put(levelId);

        var joinLength = joins == null ? 0 : joins.Length;
        writer.Put((byte)joinLength);
        for (int i = 0; i < joinLength; i++)
        {
            writer.Put(joins[i]);
        }
        writer.Put(battleType);
        writer.Put(seed);
        writer.Put(cameraAspect);
        writer.Put(isInsideGame);
        writer.Put(guid);
        writer.Put(autoPopupSkillWin);
        writer.Put(tick);

        int slotCount = ChallengeSlotIds == null ? 0 : ChallengeSlotIds.Length;
        writer.Put(slotCount);
        for(int i = 0; i < slotCount; i++)
        {
            writer.Put(ChallengeSlotIds[i]);
        }

        writer.PutArray(WheelEnhanceIds);
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        levelId = reader.GetUInt();

        joins = new JoinMessage[reader.GetByte()];
        for (int i = 0; i < joins.Length; i++)
        {
            joins[i] = reader.Get<JoinMessage>();
        }

        battleType = reader.GetInt();
        seed = reader.GetUInt();
        //roomSeed = reader.GetUInt();
        cameraAspect = reader.GetFloat();
        isInsideGame = reader.GetBool();
        guid = reader.GetString();
        autoPopupSkillWin = reader.GetBool();
        tick = reader.GetFloat();

        int slotCount = reader.GetInt();
        ChallengeSlotIds = new int[slotCount];
        for(int i = 0; i < slotCount; i++)
        {
            ChallengeSlotIds[i] = reader.GetInt();
        }

        WheelEnhanceIds = reader.GetUIntArray();
    }
}

public struct ServerPackageItem : INetSerializable
{
    public ushort frame;
    public List<MessageItem> list;

    // server write 
    public FrameMsgBuffer clientFrameMsgList;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ServerFrameMsg);
        writer.Put(frame);
        var count = clientFrameMsgList.Count;
        writer.Put((byte)count);
        clientFrameMsgList.WriterToWriter(writer);

        // if (list != null)
        // {
        //     for (int i = 0; i < list.Count; i++)
        //     {
        //         var messageItem = list[i];
        //         PackageItem.ToWriter(writer, messageItem);
        //     }
        // }
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        var msgType = reader.GetByte();
        frame = reader.GetUShort();
        var count = reader.GetByte();
        if (count > 0)
        {
            list = ListPool<MessageItem>.Get();
            for (int i = 0; i < count; i++)
            {
                list.Add(MessageItem.FromReader(reader));
            }
        }
    }
}

public enum PlaybackBit : byte
{
    Package = 1 << 0,
    Hash = 1 << 1,
    ChangeState = 1 << 2,
    GameEnd = 1 << 3
}

public struct PlaybackMessageItem : INetSerializable
{
    public PlaybackBit playbackBit;
    public ushort frame;
    public List<MessageItem> list;
    public MessageHash hash;
    public byte currentState;

    void INetSerializable.Serialize(NetDataWriter writer)
    {
        writer.Put((byte)playbackBit);
        writer.Put(frame);

        if((playbackBit & PlaybackBit.Package) > 0)
        {
            writer.Put((byte)list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                MessageItem.ToWriter(writer, list[i]);
            }
        }
        if((playbackBit & PlaybackBit.ChangeState) > 0)
        {
            writer.Put(currentState);
        }
        if((playbackBit & PlaybackBit.Hash) > 0)
        {
            writer.Put(hash.hash);
            
            var appendDetail = hash.appendDetail;
            writer.Put(appendDetail);

            if(appendDetail)
            {
                writer.Put(hash.escaped);
                writer.Put((byte)hash.allHashDetails.Length);
                for(int i = 0; i < hash.allHashDetails.Length; i++)
                {
                    hash.allHashDetails[i].Write(writer);
                }
            }
        }
    }

    void INetSerializable.Deserialize(NetDataReader reader)
    {
        playbackBit = (PlaybackBit)reader.GetByte();
        frame = reader.GetUShort();

        if((playbackBit & PlaybackBit.Package) > 0)
        {
            var count = reader.GetByte();
            list = ListPool<MessageItem>.Get();
            for (int i = 0; i < count; i++)
            {
                list.Add(MessageItem.FromReader(reader));
            }
        }

        if((playbackBit & PlaybackBit.ChangeState) > 0)
        {
            currentState = reader.GetByte();
        }

        if((playbackBit & PlaybackBit.Hash) > 0)
        {
            hash = new MessageHash()
            {
                hash = reader.GetInt(),
            };

            hash.appendDetail = reader.GetBool();
            if(hash.appendDetail)
            {
                hash.escaped = reader.GetFloat();
                var hashCount = reader.GetByte();
                hash.allHashDetails = new FrameHashItem[hashCount];
                for(int i = 0; i < hashCount; i++)
                {
                    hash.allHashDetails[i].Read(reader);
                }
            }
        }
    }
}


public struct CreateRoomMsg : INetSerializable
{
    public BattleStartMessage startBattle;
    public JoinMessage join;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        startBattle = reader.Get<BattleStartMessage>();
        join = reader.Get<JoinMessage>();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.CreateRoom);
        writer.Put(startBattle);
        writer.Put(join);
    }
}


public struct JoinRoomMsg : INetSerializable
{
    public int roomId;
    public JoinMessage joinMessage;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomId = reader.GetInt();
        joinMessage = reader.Get<JoinMessage>();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.JoinRoom);
        writer.Put(roomId);
        writer.Put(joinMessage);
    }
}

public struct StartBattleRequest : INetSerializable
{
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.StartRequest);
    }
}

public struct RoomUser : INetSerializable
{
    public string name;
    public uint HeroId;
    public uint userId;
    public void Deserialize(NetDataReader reader)
    {
        name = reader.GetString();
        HeroId = reader.GetUInt();
        userId = reader.GetUInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(name);
        writer.Put(HeroId);
        writer.Put(userId);
    }
}


public struct UpdateRoomMemberList : INetSerializable
{
    public RoomUser[] userList;
    public int roomId;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomId = reader.GetInt();
        var count = reader.GetInt();
        userList = new RoomUser[count];
        for(int i = 0; i < count; i++)
        {
            userList[i] = reader.Get<RoomUser>();
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.SyncRoomMemberList);

        writer.Put(roomId);
        var size = userList == null ? 0 : userList.Length;
        writer.Put(size);
        for(int i = 0; i < size; i++)
        {
            writer.Put(userList[i]);
        }
    }
}


public struct RoomInfoMsg : INetSerializable
{
    public int roomId;
    public string name;
    public int count;
    
    public void Deserialize(NetDataReader reader)
    {
        roomId = reader.GetInt();
        name = reader.GetString();
        count = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(roomId);
        writer.Put(name);
        writer.Put(count);
    }
}


public struct RoomListMsgRequest : INetSerializable
{
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetAllRoomList);
    }
}


public struct RoomListMsg : INetSerializable
{
    public RoomInfoMsg[] roomList;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        var count = reader.GetInt();
        roomList = new RoomInfoMsg[count];
        for(int i = 0; i < count; i++)
        {
            roomList[i] = reader.Get<RoomInfoMsg>();
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.GetAllRoomList);

        writer.Put(roomList.Length);
        for(int i = 0; i < roomList.Length; i++)
        {
            writer.Put(roomList[i]);
        }
    }
}

public struct UnSyncMsg : INetSerializable
{
    public string[] unSyncInfos;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();

        var count = reader.GetInt();
        unSyncInfos = new string[count];
        for(int i = 0; i < count; i++)
        {
            var subCount = reader.GetInt();
            List<string> lst = new List<string>();
            for(int j = 0; j < subCount; j++)
            {
                lst.Add(reader.GetString());
            }

            unSyncInfos[i] = string.Join("", lst);
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.Unsync);

        writer.Put(unSyncInfos.Length);
        for(int i = 0; i < unSyncInfos.Length; i++)
        {
            var str = unSyncInfos[i];
            var strLength = str.Length;
            var maxSize = NetDataWriter.StringBufferMaxLength - 10;

            if(strLength >= maxSize)
            {
                // 拆分。
                var count = strLength / maxSize;
                if(maxSize * count != strLength)
                {
                    count ++;
                }

                for(int j = 0; j < count; j++)
                {
                    var isLastOne = i == count - 1;
                    var lastOneSize = strLength - maxSize * (count - 1);

                    writer.Put(str.Substring(j * count, isLastOne ? lastOneSize : maxSize));
                }
            }
            else
            {
                writer.Put(1);
                writer.Put(unSyncInfos[i]);
            }
        }
    }
}
public struct SetServerSpeedMsg : INetSerializable
{
    public int speed;
    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        speed = reader.GetInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.SetSpeed);
        writer.Put(speed);
    }
}