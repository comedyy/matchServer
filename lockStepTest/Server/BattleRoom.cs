using Game;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static SyncRoomOptMsg;


public struct RoomMemberInfo
{
    public int id;
    public byte[] joinInfo;
    public string name;
    public uint heroId;
    public bool isOnLine;
    public bool isReady;
    public float onlineStateChangeTime;
    public uint heroLevel;
    public uint heroStar;

    public RoomMemberInfo(int peer, byte[] joinMessage, string name,  uint heroId, uint heroLevel, uint heroStar) : this()
    {
        this.id = peer;
        this.joinInfo = joinMessage;
        this.name = name;
        this.heroId = heroId;
        this.isOnLine = true;
        this.heroLevel = heroLevel;
        this.heroStar = heroStar;
    }
}

public class ServerBattleRoom
{
    Server _server;
    List<RoomMemberInfo> _netPeers = new List<RoomMemberInfo>();
    public int RoomId{get; private set;}
    public int MemberCount => _netPeers.Count;
    byte[] _startBattle;
    IServerGameSocket _socket;
    private int _speed = 1;

    public bool IsBattleStart {get; private set;}
    public int Master => _netPeers[0].id;
    public List<int> AllPeers => _netPeers.Select(m=>m.id).ToList();

    const int MAX_USER_COUNT = 10;
    ServerSetting _setting; 
    int _battleCount = 0;
    private int _roomType;
    private int _roomLevel;
    private string _version;
    private int _activityId;

    int MaxRoomUsers
    {
        get{
            var maxCount = _setting.maxCount;
            if(maxCount == 0)
            {
                maxCount = MAX_USER_COUNT;
            }

            return Math.Min((int)maxCount, MAX_USER_COUNT);
        }
    }

    public bool CheckMasterLeaveShouldDestroyRoom()
    {
        return _setting.masterLeaveOpt == RoomMasterLeaveOpt.RemoveRoomAndBattle 
            || (_setting.masterLeaveOpt == RoomMasterLeaveOpt.OnlyRemoveRoomBeforeBattle && _server == null);
    }


    public ServerBattleRoom(int id, int roomType, int roomLevel, string version, byte[] startBattle, IServerGameSocket socket, ServerSetting setting, int activityId)
    {
        this._roomType = roomType;
        this._roomLevel = roomLevel;
        this._version = version;
        RoomId = id;
        _startBattle = startBattle;
        _socket = socket;
        _setting = setting;
        _activityId = activityId;
    }

    public bool AddPeer(int peer, byte[] joinMessage, string name, uint heroId, uint heroLevel, uint heroStar)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        var isNewUser = index < 0;
        if(isNewUser) 
        {
            if(_netPeers.Count >= MaxRoomUsers) // 房间人数FUll
            {
                return false;
            }

            _netPeers.Add(new RoomMemberInfo(peer, joinMessage, name, heroId, heroLevel, heroStar));
        }
        else    // 替换信息
        {
            var info = _netPeers[index];
            info.heroId = heroId;
            info.joinInfo = joinMessage;
            info.heroLevel = heroLevel;
            info.heroStar = heroStar;
            _netPeers[index] = info;
        }

        BroadcastRoomInfo();

        if(isNewUser)
        {
            _socket.SendMessage(_netPeers.Select(m=>m.id), new SyncRoomOptMsg(){ state = RoomOpt.Join, param = peer});
        }

        return true;
    }

    public void StartBattle(int peer)
    {
        if(_netPeers.Count == 0) return;
        if(peer != Master) return;

        for(int i = 1; i < _netPeers.Count; i++)
        {
            if(!_netPeers[i].isReady) return;
        }

        IsBattleStart = true;
        _battleCount++;

        _server = new Server(_setting, _socket, _netPeers.Select(m=>m.id).ToList());

        var startMessage = new RoomStartBattleMsg
        {
            joinMessages = _netPeers.Select(m => m.joinInfo).ToList(),
            StartMsg = _startBattle
        };
        _server.StartBattle(startMessage);

        for(int i = 0; i < _netPeers.Count; i++)
        {
            SetIsReady(_netPeers[i].id, false);
        }
    }

    public void OnReceiveMsg(int peer, NetDataReader reader)
    {
        if(_server == null) return;

        _server.AddMessage(peer, reader);
    }

    public void Update(float deltaTime, float roomTime)
    {
        for(int i = 0; i < _speed; i++)
        {
            _server?.Update(deltaTime, roomTime);
        }

        if(_server != null && _server.IsBattleEnd)
        {
            SwitchToRoomMode();
        }
    }

    private void SwitchToRoomMode()
    {
        Console.WriteLine($"battleEnd {RoomId}");
        _server?.Destroy();
        _server = null;
        IsBattleStart = false;
    }

    // public bool IsBattleEnd => _server != null && _server.IsBattleEnd;

    internal void RemovePeer(int peer, RoomOpt opt)
    {
        _socket.SendMessage(_netPeers.Select(m=>m.id), new SyncRoomOptMsg(){ state = opt, param = peer});
        _netPeers.RemoveAll(m=> m.id == peer);

        BroadcastRoomInfo();
    }

    void BroadcastRoomInfo()
    {
        if(_netPeers.Count == 0 ) return;

        _socket.SendMessage(_netPeers.Select(m=>m.id), RoomInfo);
    }

    UpdateRoomMemberList RoomInfo => new UpdateRoomMemberList(){
        roomId = RoomId,
        roomLevel = _roomLevel,
        roomType = _roomType,
        conditions = _setting.Conditions,
        version = _version,
        activityId = _activityId,
        userList = _netPeers.Select(m=>new RoomUser(){name = m.name, HeroId = m.heroId, heroLevel = m.heroLevel, heroStar = m.heroStar,
             isOnLine = m.isOnLine, isReady = m.isReady, userId = (uint)m.id, }).ToArray()
    };

    internal void SetRoomSpeed(int peer, int speed)
    {
        _speed = speed;
    }

    internal void ForceClose(RoomOpt reason)
    {
        _socket.SendMessage(_netPeers.Select(m=>m.id), new SyncRoomOptMsg(){ state = reason, param = _netPeers[0].id});
        _socket.SendMessage(_netPeers.Select(m=>m.id), new UpdateRoomMemberList());

        _netPeers.Clear();
    }

    internal void SetUserOnLineState(int peer, bool v, float _serverTime)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        if(index < 0)
        {
            return;
        }
        
        var x = _netPeers[index];
                
        if(x.isOnLine == v) return;

        x.isOnLine = v;
        x.onlineStateChangeTime = _serverTime;
        _netPeers[index] = x;

        // sync room list
        BroadcastRoomInfo();
    }

    internal void SetIsReady(int peer, bool v)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        var x = _netPeers[index];
        x.isReady = v;
        _netPeers[index] = x;

        // sync room list
        BroadcastRoomInfo();
    }

    internal bool KickUser(int peer, int userId)
    {
        if(peer != Master) return false;

        var index = _netPeers.FindIndex(m=>m.id == userId);
        if(index <= 0) return false;

        RemovePeer(userId, SyncRoomOptMsg.RoomOpt.Kick);

        return true;
    }

    public RoomEndReason NeedDestroy(float serverTime)
    {
        // 1. 人走光了
        if(_netPeers.Count == 0) 
        {
            return RoomEndReason.AllPeerLeave;
        }

        // 掉线光了。
        var time = _server != null ? 30 : 30;
        var isAllOffLine = _netPeers.All(m=>!m.isOnLine && serverTime > m.onlineStateChangeTime + time);

        if(isAllOffLine) 
        {
            return RoomEndReason.AllPeerOffLine;
        }

        // 战斗结束了。
        if(_server == null && _battleCount > 0 && !_setting.keepRoomAfterBattle)
        {
            return RoomEndReason.BattleEnd;
        }

        return RoomEndReason.None;
    }

    internal void SendReconnectBattleMsg(int peer)
    {
        if(_server == null)
        {
            Error(peer, RoomError.BattleNotExit);
            return;
        }

        var message = _server._startMessage;
        message.isReconnect = true;

        _socket.SendMessage(peer, message);
    }

    internal void ChangeUserPos(int peer, byte fromIndex, byte toIndex)
    {
        if(peer != Master)
        {
            Error(peer, RoomError.AuthError);
            return;
        }

        if(fromIndex <= 0 || fromIndex >= _netPeers.Count) 
        {
            Error(peer, RoomError.ChangeErrorOutOfIndex);
            return;
        }
        if(toIndex <= 0 || toIndex >= _netPeers.Count) 
        {
            Error(peer, RoomError.ChangeErrorOutOfIndex);
            return;
        }

        var fromItem = _netPeers[fromIndex];
        var toItem = _netPeers[toIndex];

        _netPeers[fromIndex] = toItem;
        _netPeers[toIndex] = fromItem;

        BroadcastRoomInfo();
    }

    void Error(int peer, RoomError error)
    {
        _socket.SendMessage(peer, new RoomErrorCode(){
            roomError = error
        });
    }

    internal RoomInfoMsg GetRoomInfoMsg()
    {
        return new RoomInfoMsg()
        {
            updateRoomMemberList = RoomInfo,
        };
    }

    internal void UpdateLoadingProcess(int peer, RoomSyncLoadingProcessMsg roomSyncLoadingProcessMsg)
    {
        roomSyncLoadingProcessMsg.id = peer;
        _socket.SendMessage(AllPeers, roomSyncLoadingProcessMsg);
    }

    internal void GetUserJoinInfoResponse(int peer, int userId)
    {
        foreach(var x in _netPeers)
        {
            if(x.id == userId)
            {
                _socket.SendMessage(peer, new GetUserJoinInfoResponse(){
                    join = x.joinInfo
                });
                return;
            }
        }
    }

    internal void UserReloadServerOKMsgProcess(int peer)
    {
        _socket.SendMessage(AllPeers, new UserReloadServerOKMsg(){userId = peer});
    }
}