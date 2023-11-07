using System;

public enum UserOptType
{
    Accelerate,
    SettingAutoDropSkill,
}

public enum MessageBit : ushort
{
    Pos = 1 << 0,
    UserOpt = 1 << 1,
    ChooseSkill = 1 << 2,
    GM = 1 << 3, 
    PauseGame = 1 << 4,
    DropSkill = 1 << 5,
    CullSkill = 1 << 6,
    RechooseSkill = 1 << 7,
    Reborn = 1 << 8,
    ExitGame = 1 << 9,
    Rotation = 1 << 10,
    Ping = 1 << 11,
}

[Serializable]
public struct MessageItem
{
    public uint id;
    public MessageBit messageBit;
    public MessagePosItem posItem;
    public MessageRotationItem rotationItem;
    public MessageOpt optItem;
    public MessageChooseSkillItem chooseSkillItem;
    public MessageGMItem gmItem;
    public MessagePauseGameItem pauseItem;
    public MessageCullSkill cullSkill;
    public MessageRechoose rechooseSkill;
    public MessagePing ping;
}

[Serializable]
public struct MessagePosItem
{
    public int posX;
    public int posY;
    public bool endMoving;
}


[Serializable]
public struct MessageRotationItem
{
    public short angle;

    internal int GetCheckSum()
    {
        return angle;
    }
}

[Serializable]
public struct MessageChooseSkillItem
{
    public int skillId;
    public int type;
    public bool isSuperSkill;
    public int conditionId;
    public uint skillGroupId;
    public int multiple;

    internal int GetCheckSum()
    {
        var checkSum = skillId;
        
        return checkSum;
    }
}


[Serializable]
public struct MessageOpt
{
    public UserOptType type;
    public bool enable;

    internal int GetCheckSum()
    {
        return 0;
    }
}

[Serializable]
public struct MessageGMItem
{
    public string op;
    public int value;
    public int value1;
}

[Serializable]
public struct MessagePauseGameItem
{
    public bool pause;

    internal int GetCheckSum()
    {
        return pause.GetHashCode();
    }
}

[Serializable]
public struct MessageCullSkill
{
    public uint skillId;
    public bool active;

    internal int GetCheckSum()
    {
        return 0;
    }
}

[Serializable]
public struct MessageRechoose
{
    public uint useDropId;

    internal int GetCheckSum()
    {
        return (int)useDropId;
    }
}


[Serializable]
public struct MessagePing
{
    public int msTime;
}


[Serializable]
public struct MessageHash
{
    public int hash;
    
    public bool appendDetail;
    public FrameHashItem[] allHashDetails;
    public float escaped;
}


