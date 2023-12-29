using System.Collections.Generic;
using LiteNetLib.Utils;


public enum ServerSyncType
{
    SyncMsgEventFrame,
    SyncMsgOnlyHasMsg,
}

public enum RoomMasterLeaveOpt
{
    RemoveRoomAndBattle,
    OnlyRemoveRoomBeforeBattle,
    ChangeRoomMater,
}


public struct ServerSetting : INetSerializable
{
    public ServerSyncType syncType;
    public float tick;
    public ushort maxFrame;
    public RoomMasterLeaveOpt masterLeaveOpt;
    public byte maxCount;

    public void Deserialize(NetDataReader reader)
    {
        syncType = (ServerSyncType)reader.GetByte();
        tick = reader.GetFloat();
        maxFrame = reader.GetUShort();
        masterLeaveOpt = (RoomMasterLeaveOpt)reader.GetByte();
        maxCount = reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)syncType);
        writer.Put(tick);
        writer.Put(maxFrame);
        writer.Put((byte)masterLeaveOpt);
        writer.Put(maxCount);
    }
}


public struct CreateRoomMsg : INetSerializable
{
    public byte[] startBattleMsg;
    public byte[] join;
    public string name;
    public uint userId;
    public uint heroId;
    public string roomName;
    public ServerSetting setting;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte(); // msgHeader

        startBattleMsg = reader.GetBytesWithLength();
        join = reader.GetBytesWithLength();
        
        name = reader.GetString();
        userId = reader.GetUInt();
        heroId = reader.GetUInt();
        roomName = reader.GetString();
        setting = reader.Get<ServerSetting>();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.CreateRoom);
        writer.PutBytesWithLength(startBattleMsg);
        writer.PutBytesWithLength(join);

        writer.Put(name);
        writer.Put(userId);
        writer.Put(heroId);
        writer.Put(roomName);
        writer.Put(setting);
    }
}


public struct JoinRoomMsg : INetSerializable
{
    public int roomId;
    public byte[] joinMessage;
    public string name;
    public uint userId;
    public uint heroId;


    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomId = reader.GetInt();
        joinMessage = reader.GetBytesWithLength();
        name = reader.GetString();
        userId = reader.GetUInt();
        heroId = reader.GetUInt();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.JoinRoom);
        writer.Put(roomId);
        writer.PutBytesWithLength(joinMessage);
        writer.Put(name);
        writer.Put(userId);
        writer.Put(heroId);
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

public struct RoomStartBattleMsg : INetSerializable
{
    public byte[] StartMsg;
    public List<byte[]> joinMessages;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();

        StartMsg = reader.GetBytesWithLength();
        var MemberCount = reader.GetByte();
        joinMessages = new List<byte[]>();
        for(int i = 0; i < MemberCount; i++)
        {
            joinMessages.Add(reader.GetBytesWithLength());
        }
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.RoomStartBattle);

        writer.PutBytesWithLength(StartMsg);
        writer.Put((byte)joinMessages.Count);
        for(int i = 0; i < joinMessages.Count; i++)
        {
            writer.PutBytesWithLength(joinMessages[i]);
        }
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

public enum RoomError : byte
{
    RoomFull = 1,
}

public struct RoomErrorCode : INetSerializable
{
    public RoomError roomError;

    public void Deserialize(NetDataReader reader)
    {
        reader.GetByte();
        roomError = (RoomError)reader.GetByte();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put((byte)MsgType1.ErrorCode);
        writer.Put((byte)roomError);
    }
}