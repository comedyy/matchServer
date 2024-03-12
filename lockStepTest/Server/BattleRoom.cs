using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using static SyncRoomOptMsg;

public struct RobertStruct
{
    public bool isRobert;
    public int robertDelay;

    public RobertStruct(bool isRobert, int robertDelay)
    {
        this.isRobert = isRobert;
        this.robertDelay = robertDelay;
    }
}

public struct RoomMemberInfo
{
    public int id;
    public byte[] joinInfo;
    public bool isOnLine;
    public bool isReady;
    public double onlineStateChangeTime;
    public byte[] showInfo;
    public bool isInNeedAiState;
    public bool isRobert; // 是否是机器人
    public int robertDelay; // 机器人延迟。
    public double readyTime;

    public RoomMemberInfo(int peer, byte[] joinMessage, byte[] showInfo, RobertStruct robertStruct) : this()
    {
        this.id = peer;
        this.joinInfo = joinMessage;
        this.isOnLine = true;
        this.showInfo = showInfo;
        this.isInNeedAiState = false;
        this.isRobert = robertStruct.isRobert;
        this.robertDelay = robertStruct.robertDelay;
    }
}

public class ServerBattleRoom
{
    Server _server;
    List<RoomMemberInfo> _netPeers = new List<RoomMemberInfo>();
    public int RoomId{get; private set;}
    public int MemberCount => _netPeers.Count;
    byte[] _startBattle;
    byte[] roomShowInfo;
    IServerGameSocket _socket;
    private int _speed = 1;

    public bool HasBattle {get; private set;}
    public int Master => _netPeers[0].id;
    public IEnumerable<int> AllPeers => _netPeers.Select(m=>m.id);
    public IEnumerable<int> AllOnLinePeers => _netPeers.Where(m=>m.isOnLine).Select(m=>m.id);

    const int MAX_USER_COUNT = 10;
    ServerSetting _setting;
    private Random _serverRandom;
    int _battleCount = 0;

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


    public ServerBattleRoom(int id, byte[] roomShowInfo, byte[] startBattle, IServerGameSocket socket, ServerSetting setting, Random serverRandom)
    {
        this.roomShowInfo = roomShowInfo;
        RoomId = id;
        _startBattle = startBattle;
        _socket = socket;
        _setting = setting;
        this._serverRandom = serverRandom;
    }

    public bool AddPeer(int peer, byte[] joinMessage, byte[] joinShowInfo, RobertStruct robertStruct)
    {
        // 服务器开始之后不应该让它加入。
        if(_server != null)
        {
            _socket.SendMessage(peer, new RoomErrorCode(){ roomError = RoomError.RoomNotExist});
            return false;
        }

        var index = _netPeers.FindIndex(m=>m.id == peer);
        var isNewUser = index < 0;
        if(isNewUser) 
        {
            if(_netPeers.Count >= MaxRoomUsers) // 房间人数FUll
            {
                Error(peer, RoomError.RoomFull);
                return false;
            }

            _netPeers.Add(new RoomMemberInfo(peer, joinMessage, joinShowInfo, robertStruct));
        }
        else
        {
            Error(peer, RoomError.JoinRoomErrorInsideRoom);
            return false;
        }

        BroadcastRoomInfo();
        _socket.SendMessage(AllOnLinePeers, new SyncRoomOptMsg(){ state = RoomOpt.Join, param = peer});

        return true;
    }

    public void UpdateInfo(int peer, byte[] joinMessage, byte[] joinShowInfo)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        if(index >= 0)
        {
            var info = _netPeers[index];
            info.joinInfo = joinMessage;
            info.showInfo = joinShowInfo;
            _netPeers[index] = info;
            BroadcastRoomInfo();
        }
        else
        {
            Error(peer, RoomError.UpdatFailedMemberNotExist);
        }
    }

    public void StartBattle(int peer, double serverTime)
    {
        if(_netPeers.Count == 0) return;
        if(peer != Master) return;
        if(_server != null) return;

        for(int i = 1; i < _netPeers.Count; i++)
        {
            if(!_netPeers[i].isReady) return;
        }

        HasBattle = true;
        _battleCount++;

        _server = new Server(_setting, _socket, AllPeers.ToList(), serverTime);

        var startMessage = new RoomStartBattleMsg
        {
            joinMessages = _netPeers.Select(m => m.joinInfo).ToList(),
            StartMsg = _startBattle
        };
        _server.StartBattle(startMessage);

        for(int i = 0; i < _netPeers.Count; i++)
        {
            SetIsReady(_netPeers[i].id, false, 0);
        }

        for(int i = 0; i < _netPeers.Count; i++)
        {
            _server.SetOnlineState(i, _netPeers[i].isOnLine);
        }
    }

    public void OnReceiveMsg(int peer, NetDataReader reader)
    {
        if(_server == null) return;

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_BattleRoom.OnReceiveMsg");
        #endif

        _server.AddMessage(peer, reader);

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.EndSample();
        #endif
    }

    public void Update(float deltaTime, double roomTime)
    {
        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.BeginSample("NETBATTLE_BattleRoom.Update");
        #endif

        for(int i = 0; i < _speed; i++)
        {
            _server?.Update(deltaTime, roomTime);
        }   

        #if UNITY_EDITOR
        UnityEngine.Profiling.Profiler.EndSample();
        #endif

        if(_server != null && _server.IsBattleEnd)
        {
            SwitchToRoomMode();
        }

        UpdateRobertBehavior(roomTime);
    }

    private void UpdateRobertBehavior(double roomTime)
    {
        if(_server != null) return;
        if(_netPeers.Count <= 1) return;

        for(int i = 0; i < _netPeers.Count; i++)
        {
            var peer = _netPeers[i];
            if(!peer.isRobert) continue;

            var isMaster = i == 0;
            if(isMaster)
            {
                // start game
                var hasUser = _netPeers.Any(m=>!m.isRobert);
                if(!hasUser) continue;

                var battleStartDelay = _netPeers[0].robertDelay;

                // all ready check
                var allReadyAndTimeout = true;
                for(int j = 1; j < _netPeers.Count; j++)
                {
                    allReadyAndTimeout &= _netPeers[j].isReady;
                    allReadyAndTimeout &= (roomTime - _netPeers[j].readyTime > battleStartDelay);
                }
                if(!allReadyAndTimeout) continue;

                StartBattle(peer.id, roomTime);
            }
            else // ready
            {
                if(peer.isReady) continue;
                if(roomTime - peer.onlineStateChangeTime < peer.robertDelay) continue;
                
                SetIsReady(peer.id, true, roomTime);
            }
        }
    }

    private void SwitchToRoomMode()
    {
        Console.WriteLine($"battleEnd {RoomId}");
        _server?.Destroy();
        _server = null;
        HasBattle = false;
    }

    // public bool IsBattleEnd => _server != null && _server.IsBattleEnd;

    internal bool RemovePeer(int peer, RoomOpt opt)
    {
        if(_server != null)
        {
            Error(peer, RoomError.LeaveErrorInBattle);
            return false;
        }

        _socket.SendMessage(AllOnLinePeers, new SyncRoomOptMsg(){ state = opt, param = peer});
        _netPeers.RemoveAll(m=> m.id == peer);

        BroadcastRoomInfo();
        return true;
    }

    void BroadcastRoomInfo()
    {
        if(_netPeers.Count == 0 ) return;

        _socket.SendMessage(AllOnLinePeers, RoomInfo);
    }

    UpdateRoomMemberList RoomInfo => new UpdateRoomMemberList(){
        roomId = RoomId,
        conditions = _setting.Conditions,
        roomShowInfo = roomShowInfo,
        AIHelperIndex = (byte)_netPeers.FindIndex(m=>!m.isInNeedAiState),
        userList = _netPeers.Select(m=>new RoomUser(){userInfo = m.showInfo,
             isOnLine = m.isOnLine, isReady = m.isReady, userId = (uint)m.id, needAiHelp = m.isInNeedAiState, isRobert = m.isRobert }).ToArray()
    };

    internal void SetRoomSpeed(int peer, int speed)
    {
        _speed = Math.Max(Math.Min(speed, 5), 1);
    }

    internal void ForceClose(RoomOpt reason)
    {
        if(_netPeers.Count == 0) return;

        _socket.SendMessage(AllOnLinePeers, new SyncRoomOptMsg(){ state = reason, param = _netPeers[0].id});
        _socket.SendMessage(AllOnLinePeers, new UpdateRoomMemberList());

        _netPeers.Clear();
    }

    internal void SetUserOnLineState(int peer, bool v, double _serverTime)
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

        var need = !v;
        OnUpdateAiHelper(peer, need);

        if(_server != null)
        {
            _server.SetOnlineState(index, v);
        }

        // sync room list
        BroadcastRoomInfo();
    }

    internal void SetIsReady(int peer, bool v, double readyTime)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        var x = _netPeers[index];
        x.isReady = v;
        x.readyTime = readyTime;
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

    public RoomEndReason NeedDestroy(double serverTime)
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

        // 玩家暂停太久，100秒。
        // if(_server != null && _server._pauseFrame != int.MaxValue && (serverTime - _server.UserPauseTime) > 100)
        // {
        //     return RoomEndReason.UserPauseTooLongException;
        // }

        return RoomEndReason.None;
    }

    internal void SendReconnectBattleMsg(int peer)
    {
        if(_server == null)
        {
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

    public void Error(int peer, RoomError error)
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

    internal void UserReloadServerOKMsgProcess(int peer)
    {
        if(_server == null)
        {
            return;
        }

        OnUpdateAiHelper(peer, false);
        BroadcastRoomInfo();
    }

    void OnUpdateAiHelper(int peer, bool need)
    {
        var index = _netPeers.FindIndex(m=>m.id == peer);
        if(index < 0) return;  // not found

        var x = _netPeers[index];
        if(x.isInNeedAiState == need) return; // 状态未改变

        x.isInNeedAiState = need;
        _netPeers[index] = x;
    }
}