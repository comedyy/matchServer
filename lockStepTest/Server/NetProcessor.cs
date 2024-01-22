
using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

public enum RoomEndReason
{
    None,
    RoomMasterLeave,
    BattleEnd,
    AllPeerLeave,
    AllPeerOffLine,
}

public class NetProcessor 
{
    Dictionary<int, ServerBattleRoom> _allUserRooms = new Dictionary<int, ServerBattleRoom>();
    Dictionary<int, ServerBattleRoom> _allRooms = new Dictionary<int, ServerBattleRoom>();
    static int _roomId = 0;
    float _serverTime;
    IServerGameSocket _serverSocket;
    public NetProcessor(IServerGameSocket socket)
    {
        _serverSocket = socket;
        _serverSocket.Start();
        _serverSocket.OnReceiveMsg = OnReceiveMsg;
        _serverSocket.OnPeerDisconnect = OnDisconnect;
        _serverSocket.OnPeerReconnected = OnReconnect;
        _serverSocket.GetAllRoomList = GetRoomListMsg;
        _serverSocket.GetUserState = GetUserState;
        _serverSocket.GetRoomState = GetRoomState;
    }

    private void OnReceiveMsg(int peer, NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        switch(msgType)
        {
            case MsgType1.CreateRoom: CreateRoom(peer, reader.Get<CreateRoomMsg>()); break;
            case MsgType1.JoinRoom: JoinRoom(peer, reader.Get<JoinRoomMsg>()); break;
            case MsgType1.KickUser: KickUser(peer, reader.Get<KickUserMsg>()); break;
            case MsgType1.LeaveUser: LeaveUser(peer); break;
            case MsgType1.RoomReady: SetIsReady(peer, reader.Get<RoomReadyMsg>()); break;
            case MsgType1.StartRequest : StartBattle(peer, reader.Get<StartBattleRequest>()); break;
            case MsgType1.SetSpeed: SetRoomSpeed(peer, reader.Get<SetServerSpeedMsg>()); break;
            case MsgType1.RoomChangeUserPos: ChangeUserPos(peer, reader.Get<RoomChangeUserPosMsg>()); break;
            case MsgType1.RoomSyncLoadingProcess: SyncLoadingProcess(peer, reader.Get<RoomSyncLoadingProcessMsg>()); break;
            case MsgType1.GetUserInfo: GetUserJoinInfoResponse(peer, reader.Get<GetUserJoinInfoMsg>().userId); break;
            case MsgType1.GetRoomState: 
            case MsgType1.GetAllRoomList:
                break;
            default:
                if(_allUserRooms.TryGetValue(peer, out var room))
                {
                    room.OnReceiveMsg(peer, reader);
                }
                break;
        }
    }

    private void GetUserJoinInfoResponse(int peer, int userId)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.GetUserJoinInfoResponse(peer, userId);
        }
    }

    private void SyncLoadingProcess(int peer, RoomSyncLoadingProcessMsg roomSyncLoadingProcessMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.UpdateLoadingProcess(peer, roomSyncLoadingProcessMsg);
        }
    }

    private void ChangeUserPos(int peer, RoomChangeUserPosMsg setServerSpeedMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.ChangeUserPos(peer, setServerSpeedMsg.fromIndex, setServerSpeedMsg.toIndex);
        }
    }

    private void KickUser(int peer, KickUserMsg kickUserMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            if(room.KickUser(peer, kickUserMsg.userId))
            {
                _allUserRooms.Remove(kickUserMsg.userId);
            }
        }
    }

    private void SetRoomSpeed(int peer, SetServerSpeedMsg setServerSpeedMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetRoomSpeed(peer, setServerSpeedMsg.speed);
        }
    }

    private RoomListMsg GetRoomListMsg()
    {
        var roomList = _allRooms.Values.Where(m=>!m.IsBattleStart).Select(m=>m.GetRoomInfoMsg());

        return new RoomListMsg(){
            roomList = roomList.ToArray()
        };
    }

    private GetUserStateMsg GetUserState(int peerId)
    {
        GetUserStateMsg.UserState state = GetUserStateMsg.UserState.None;
        if(_allUserRooms.TryGetValue(peerId, out var room))
        {
            state = room.IsBattleStart ? GetUserStateMsg.UserState.HasBattle : GetUserStateMsg.UserState.HasRoom;
        }

        return new GetUserStateMsg(){ userId = peerId, state = state};
    }

    
    private GetRoomStateResponse GetRoomState(int roomId)
    {
        if(_allRooms.TryGetValue(roomId, out var room))
        {
            return new GetRoomStateResponse(){
                roomId = roomId, infoMsg = room.GetRoomInfoMsg()
            };
        }

        return new GetRoomStateResponse(){
            roomId = roomId, infoMsg = default
        };;
    }

    private void StartBattle(int peer, StartBattleRequest startBattleRequest)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.StartBattle(peer);
        }
    }

    
    private void OnReconnect(int peer, bool reconnectBattle)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, true, _serverTime);

            if(reconnectBattle)
            {
                room.SendReconnectBattleMsg(peer);
            }
        }
        else
        {
            _serverSocket.SendMessage(peer, new UpdateRoomMemberList());
        }
    }


    void OnDisconnect(int peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, false, _serverTime);
        }
    }

    void SetIsReady(int peer, RoomReadyMsg ready)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetIsReady(peer, ready.isReady);
        }
    }

    void LeaveUser(int peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, false, _serverTime);
            var master = room.Master;
            if(master == peer && room.CheckMasterLeaveShouldDestroyRoom())
            {
                RemoveRoom(room, RoomEndReason.RoomMasterLeave);
            }
            else
            {
                room.RemovePeer(peer, SyncRoomOptMsg.RoomOpt.Leave);
                _allUserRooms.Remove(peer);
            }
        }
    }

    private void JoinRoom(int peer, JoinRoomMsg joinRoomMsg)
    {
        if(_allRooms.TryGetValue(joinRoomMsg.roomId, out var room))
        {
            if(_allUserRooms.TryGetValue(peer, out var room1))  // 已经有房间
            {
                if(room1 != room)
                {
                    // send error
                    _serverSocket.SendMessage(peer, new RoomErrorCode(){
                        roomError = RoomError.JoinRoomErrorHasRoom
                    });
                    return;
                }
            }

            if(room.AddPeer(peer, joinRoomMsg.joinMessage, joinRoomMsg.name, joinRoomMsg.heroId, joinRoomMsg.heroLevel, joinRoomMsg.heroStar))
            {
                _allUserRooms[peer] = room;
            }
            else
            {
                // send error
                _serverSocket.SendMessage(peer, new RoomErrorCode(){
                    roomError = RoomError.RoomFull
                });
            }
        }
    }

    void CreateRoom(int peer, CreateRoomMsg msg)
    {
        if(_allUserRooms.ContainsKey(peer))
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode(){
                roomError = RoomError.CreateRoomErrorHasRoom
            });
            return;
        }

        var roomId = ++_roomId;
        var room = new ServerBattleRoom(roomId, msg.roomType,  msg.roomLevel, msg.version, msg.startBattleMsg,  _serverSocket, msg.setting);
        _allRooms.Add(roomId, room);

        JoinRoom(peer, new JoinRoomMsg(){
            roomId = roomId, joinMessage = msg.join, heroId = msg.heroId, name = msg.name, heroStar = msg.heroStar, heroLevel = msg.heroLevel
        });

        Console.WriteLine($"CreateRoom:{roomId}");
    }

    List<(ServerBattleRoom, RoomEndReason)> _removeRooms = new List<(ServerBattleRoom, RoomEndReason)>();
    public void OnUpdate(float deltaTime)
    {
        _serverTime += deltaTime;
        _serverSocket.Update();

        foreach(var x in _allRooms.Values)
        {
            x.Update(deltaTime, _serverTime);
        }

        CheckClearRoom();
    }
    float _lastClearRoomTime = 0;
    private void CheckClearRoom()
    {
        if(_serverTime - _lastClearRoomTime < 1)
        {
            return;
        }

        _lastClearRoomTime = _serverTime;

        _removeRooms.Clear();
        foreach(var x in _allRooms.Values)
        {
            var removeRoomReason = x.NeedDestroy(_serverTime);
            if(removeRoomReason != RoomEndReason.None)
            {
                _removeRooms.Add((x, removeRoomReason));
            }
        }

        foreach(var room in _removeRooms)
        {
            RemoveRoom(room.Item1, room.Item2);
        }
    }

    void RemoveRoom(ServerBattleRoom room, RoomEndReason roomEndReason)
    {
        var allPeers = room.AllPeers;
        room.ForceClose();
        _allRooms.Remove(room.RoomId);

        foreach(var x in allPeers)
        {
            _allUserRooms.Remove(x);
        }

        Console.WriteLine($"RemoveRoom:{room.RoomId} {roomEndReason}");
    }
    
    internal string GetStatus()
    {
        return $"房间{_allRooms.Count}个, user{_allUserRooms.Count}个";
    }

    public void Destroy()
    {
        _serverSocket.OnDestroy();
    }
}