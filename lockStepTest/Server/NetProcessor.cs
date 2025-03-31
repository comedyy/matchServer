
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;

public enum RoomEndReason
{
    None,
    RoomMasterLeave,
    BattleEnd,
    AllPeerLeave,
    AllPeerOffLine,
    UserPauseTooLongException,
    ClientBattleReconnect
}

public class NetProcessor
{
    Dictionary<int, ServerBattleRoom> _allUserRooms = new Dictionary<int, ServerBattleRoom>();
    Dictionary<int, ServerBattleRoom> _allRooms = new Dictionary<int, ServerBattleRoom>();
    public int RoomId { get; private set; }
    double _serverTime;
    IServerGameSocket _serverSocket;
    UniqueIdGenerator _generator;
    Random _serverRandom;

    public NetProcessor(IServerGameSocket socket, int initRoomId, KeyValuePair<int, int> IdRange)
    {
        RoomId = initRoomId;
        _serverSocket = socket;
        _serverSocket.Start();
        _serverSocket.OnReceiveMsg = OnReceiveMsg;
        _serverSocket.OnPeerDisconnect = OnDisconnect;
        _serverSocket.OnPeerReconnected = OnReconnect;
        _serverSocket.OnUnConnectReceiveMsg = OnUnconnectMsg;
        _serverSocket.GetUserState = GetUserState;

        _generator = new UniqueIdGenerator(IdRange.Key, IdRange.Value);
        _serverRandom = new Random(999);
    }

    private void OnUnconnectMsg(IPEndPoint point, NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        if (msgType == MsgType1.GetAllRoomList)
        {
            var msg = GetRoomListMsg();
            _serverSocket.SendUnconnectedMessage(point, msg);
        }
        else if (msgType == MsgType1.GetRoomState)
        {
            var msg = GetRoomState(reader.Get<GetRoomStateMsg>().idRoom);
            _serverSocket.SendUnconnectedMessage(point, msg);
        }
        else if (msgType == MsgType1.GetUserState)
        {
            var userId = reader.Get<GetUserStateMsg>().userId;
            RemoveRoomIfPossible(userId);
            var state = GetUserState(userId);
            var msg = new GetUserStateMsg() { userId = userId, state = state };
            _serverSocket.SendUnconnectedMessage(point, msg);
        }
        else if (msgType == MsgType1.GetUniqueIdInServer)
        {
            var count = reader.Get<GetServerUniqueIdMsg>().count;
            _serverSocket.SendUnconnectedMessage(point, new GetServerUniqueIdMsg()
            {
                id = _generator.GeneratorUniqueId(count),
                count = count
            });
        }
    }

    private void OnReceiveMsg(int peer, NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        switch (msgType)
        {
            case MsgType1.CreateRoom: CreateRoom(peer, reader.Get<CreateRoomMsg>()); break;
            case MsgType1.CreateAutoCreateRoomRobert: CreateRobertRoom(peer, reader.Get<CreateAutoCreateRoomRobertMsg>()); break;
            case MsgType1.JoinRoom: JoinRoom(peer, reader.Get<JoinRoomMsg>()); break;
            case MsgType1.CreateAutoJoinRobert: JoinRobert(reader.Get<CreateAutoJoinRobertMsg>()); break;
            case MsgType1.UpdateMemberInfo: UpdateMemberInfo(peer, reader.Get<UpdateMemberInfoMsg>()); break;
            case MsgType1.KickUser: KickUser(peer, reader.Get<KickUserMsg>()); break;
            case MsgType1.LeaveUser: LeaveUser(peer); break;
            case MsgType1.RoomReady: SetIsReady(peer, reader.Get<RoomReadyMsg>()); break;
            case MsgType1.StartRequest: StartBattle(peer, reader.Get<StartBattleRequest>()); break;
            case MsgType1.SetSpeed: SetRoomSpeed(peer, reader.Get<SetServerSpeedMsg>()); break;
            case MsgType1.RoomChangeUserPos: ChangeUserPos(peer, reader.Get<RoomChangeUserPosMsg>()); break;
            case MsgType1.UserReloadServerOK: UserReloadServerOKMsgProcess(peer); break;
            case MsgType1.BroadCastMsg: BroadcastMsg(peer, reader.Get<BroadCastMsg>()); break;
            case MsgType1.ChangeRoomInfo: ChangeRoomInfo(peer, reader.Get<ChangeRoomInfoMsg>()); break;
            case MsgType1.RobertQuitRoom: RobertQuitRoom(peer, reader.Get<RobertQuitRoomMsg>()); break;
            case MsgType1.GetRoomState:
            case MsgType1.GetAllRoomList:
                break;
            default:
                if (_allUserRooms.TryGetValue(peer, out var room))
                {
                    room.OnReceiveMsg(peer, reader);
                }
                break;
        }
    }

    private void RobertQuitRoom(int peer, RobertQuitRoomMsg robertQuitRoomMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))  // 已经有房间
        {
            room.RobertQuitRoom(peer, robertQuitRoomMsg.robertId);
            _allUserRooms.Remove(robertQuitRoomMsg.robertId);
        }
    }

    private void ChangeRoomInfo(int peer, ChangeRoomInfoMsg changeRoomInfoMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))  // 已经有房间
        {
            room.ChangeRoomInfo(peer, changeRoomInfoMsg, _serverTime);
        }
    }

    private void BroadcastMsg(int peer, BroadCastMsg msg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))  // 已经有房间
        {
            _serverSocket.SendMessage(room.AllOnLinePeers, msg);
        }
    }

    private void UpdateMemberInfo(int peer, UpdateMemberInfoMsg updateMemberInfoMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room1))  // 已经有房间
        {
            room1.UpdateInfo(peer, updateMemberInfoMsg.joinMessage, updateMemberInfoMsg.joinShowInfo);
        }
        else
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode()
            {
                roomError = RoomError.RoomNotExist
            });
        }
    }

    private void UserReloadServerOKMsgProcess(int peer)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.UserReloadServerOKMsgProcess(peer);
        }
    }

    private void ChangeUserPos(int peer, RoomChangeUserPosMsg setServerSpeedMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.ChangeUserPos(peer, setServerSpeedMsg.fromIndex, setServerSpeedMsg.toIndex);
        }
    }

    private void KickUser(int peer, KickUserMsg kickUserMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            if (room.KickUser(peer, kickUserMsg.userId))
            {
                _allUserRooms.Remove(kickUserMsg.userId);
            }
        }
    }

    private void SetRoomSpeed(int peer, SetServerSpeedMsg setServerSpeedMsg)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetRoomSpeed(peer, setServerSpeedMsg.speed);
        }
    }

    private RoomListMsg GetRoomListMsg()
    {
        var roomList = _allRooms.Values.Where(m => !m.HasBattle).Select(m => m.GetRoomInfoMsg());

        return new RoomListMsg()
        {
            roomList = roomList.ToArray()
        };
    }

    private GetUserStateMsg.UserState GetUserState(int peerId)
    {
        GetUserStateMsg.UserState state = GetUserStateMsg.UserState.None;
        if (_allUserRooms.TryGetValue(peerId, out var room))
        {
            state = room.HasBattle ? GetUserStateMsg.UserState.HasBattle : GetUserStateMsg.UserState.HasRoom;
        }

        return state;
    }


    private GetRoomStateResponse GetRoomState(int roomId)
    {
        if (_allRooms.TryGetValue(roomId, out var room) && !room.HasBattle)
        {
            return new GetRoomStateResponse()
            {
                roomId = roomId,
                infoMsg = room.GetRoomInfoMsg()
            };
        }

        return new GetRoomStateResponse()
        {
            roomId = roomId,
            infoMsg = default
        }; ;
    }

    private void StartBattle(int peer, StartBattleRequest startBattleRequest)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.StartBattle(peer, _serverTime);
        }
    }


    private void OnReconnect(int peer, TeamConnectParam teamParam)
    {
        if (teamParam == TeamConnectParam.None) return;

        if(teamParam == TeamConnectParam.SyncInfoWhenClientOutsideRoom)
        {
            RemoveRoomIfPossible(peer);
        }
        SyncRoomInfo(peer);
    }

    void RemoveRoomIfPossible(int peer)
    {
        if (!_allUserRooms.TryGetValue(peer, out var room))
        {
            return;
        }

        // 如果是客户端战斗（通过机器人判断），服务器已经在战斗了，客户端掉线了。
        if(room.IsInClientBattleRobert)
        {
            RemoveRoom(room, RoomEndReason.AllPeerLeave);
        }
    }

    void SyncRoomInfo(int peer)
    {
        if (!_allUserRooms.TryGetValue(peer, out var room))
        {
            _serverSocket.SendMessage(peer, new UpdateRoomMemberList());
            return;
        }

        room.SetUserOnLineState(peer, true, _serverTime);
        
        if(room.ContainUser(peer))
        {
            _serverSocket.SendMessage(peer, room.RoomInfo);
        }
        else    // 正常不会走这里。
        {
            _serverSocket.SendMessage(peer, new UpdateRoomMemberList());
            // Exception, user not in room;
            _allUserRooms.Remove(peer);
            ServerLog.WriteLog($"found user not in room {peer}");
            return;
        }
        
        room.SendReconnectBattleMsg(peer);
    }


    void OnDisconnect(int peer)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetUserOnLineState(peer, false, _serverTime);
        }
    }

    void SetIsReady(int peer, RoomReadyMsg ready)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetIsReady(peer, ready.isReady, _serverTime, true);
        }
    }

    void LeaveUser(int peer)
    {
        if (_allUserRooms.TryGetValue(peer, out var room))
        {
            var master = room.Master;
            if (master == peer && room.CheckMasterLeaveShouldDestroyRoom())
            {
                RemoveRoom(room, RoomEndReason.RoomMasterLeave);
            }
            else
            {
                if (room.RemovePeer(peer, SyncRoomOptMsg.RoomOpt.Leave))
                {
                    _allUserRooms.Remove(peer);
                }
            }
        }
    }

    private void JoinRobert(CreateAutoJoinRobertMsg createAutoJoinRobertMsg)
    {
        int idRobert = createAutoJoinRobertMsg.idRobert;
        if (idRobert == 0) return;
        if (_allUserRooms.ContainsKey(idRobert))  // 已经有房间
        {
            return;
        }

        if (_allRooms.TryGetValue(createAutoJoinRobertMsg.joinRoomMsg.roomId, out var room))
        {
            var excludePlayerId = createAutoJoinRobertMsg.PlayerIdWhichCannotStayTogether;    // 检测是否有特殊玩家在，如果在机器人无法加入。
            if(excludePlayerId != 0)
            {
                foreach(var x in room._netPeers)
                {
                    if(x.id == excludePlayerId)
                    {
                        room.Error(room.Master, RoomError.RobertPlayerIdWhichCannotStayTogether);
                        return;
                    }
                }
            }

            if (room.AddPeer(idRobert, createAutoJoinRobertMsg.joinRoomMsg.joinMessage, createAutoJoinRobertMsg.joinRoomMsg.joinShowInfo,
                new RobertStruct(true, createAutoJoinRobertMsg.readyDelay, createAutoJoinRobertMsg.autoLeaveWhenBattleEnd, createAutoJoinRobertMsg.PlayerIdWhichCannotStayTogether), createAutoJoinRobertMsg.joinRoomMsg.gameId))
            {
                _allUserRooms[idRobert] = room;
                room.SetUserOnLineState(idRobert, false, _serverTime);

                Console.WriteLine($"Join robert {idRobert}");
            }
        }
    }

    private void JoinRoom(int peer, JoinRoomMsg joinRoomMsg)
    {
        RemoveRoomIfPossible(peer);

        if (_allRooms.TryGetValue(joinRoomMsg.roomId, out var room))
        {
            if (_allUserRooms.TryGetValue(peer, out var room1))  // 已经有房间
            {
                if (room1 != room)
                {
                    room.Error(peer, RoomError.JoinRoomErrorHasRoom);
                    SyncRoomInfo(peer); // 客户端逻辑错乱，重发房间信息。
                    return;
                }
                else
                {
                    room.Error(peer, RoomError.JoinRoomErrorInsideRoom);
                    SyncRoomInfo(peer); // 客户端逻辑错乱，重发房间信息。
                    return;
                }
            }

            for (int i = room._netPeers.Count - 1; i >= 0 ; i--)       // 判断是否有特殊机器人在，如果在，把它踢掉。
            {
                var x = room._netPeers[i];
                if(x.playerIdWhichCannotStayTogether == peer)
                {
                    KickUser(room.Master, new KickUserMsg(){ userId = x.id});
                    room.Error(room.Master, RoomError.RobertPlayerIdWhichCannotStayTogetherBeKick);
                }
            }

            if (room.AddPeer(peer, joinRoomMsg.joinMessage, joinRoomMsg.joinShowInfo, new RobertStruct(false, 0, false, 0), joinRoomMsg.gameId))
            {
                _allUserRooms[peer] = room;
            }
        }
        else
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode()
            {
                roomError = RoomError.RoomNotExist
            });
        }
    }

    private void CreateRobertRoom(int peer, CreateAutoCreateRoomRobertMsg createAutoCreateRoomRobertMsg)
    {
        RemoveRoomIfPossible(peer);
        
        var robertId = createAutoCreateRoomRobertMsg.idRobert;
        if (_allUserRooms.ContainsKey(robertId))
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode()
            {
                roomError = RoomError.CreateRoomErrorHasRoom
            });
            SyncRoomInfo(peer); // 客户端逻辑错乱，重发房间信息。
            return;
        }

        var msg = createAutoCreateRoomRobertMsg.createRoomMsg;
        var roomId = ++RoomId;
        var room = new ServerBattleRoom(roomId, msg.roomShowInfo, msg.startBattleMsg, _serverSocket, msg.setting, _serverRandom);
        _allRooms.Add(roomId, room);

        JoinRobert(new CreateAutoJoinRobertMsg()
        {
            joinRoomMsg = new JoinRoomMsg()
            {
                roomId = roomId,
                joinMessage = msg.join,
                joinShowInfo = msg.joinShowInfo
            },
            idRobert = createAutoCreateRoomRobertMsg.idRobert,
            readyDelay = createAutoCreateRoomRobertMsg.delayStart
        }
        );

        JoinRoom(peer, new JoinRoomMsg()
        {
            roomId = roomId,
            joinMessage = createAutoCreateRoomRobertMsg.joinUser,
            joinShowInfo = createAutoCreateRoomRobertMsg.joinShowInfoUser
        });

        Console.WriteLine($"CreateRobertRoom:{roomId}");
    }


    void CreateRoom(int peer, CreateRoomMsg msg)
    {
        RemoveRoomIfPossible(peer);

        if (_allUserRooms.ContainsKey(peer))
        {
            _serverSocket.SendMessage(peer, new RoomErrorCode()
            {
                roomError = RoomError.CreateRoomErrorHasRoom
            });
            
            SyncRoomInfo(peer); // 客户端逻辑错乱，重发房间信息。
            return;
        }

        var roomId = 0;
        if (msg.setting.needJoinId)
        {
            roomId = ++RoomId;
        }
        else
        {
            for (int i = 0; i < 10; i++)
            {
                roomId = (int)_serverRandom.Next(10000, 99999);
                if (!_allRooms.ContainsKey(roomId))
                {
                    break;
                }
            }

            if (roomId == 0)
            {
                _serverSocket.SendMessage(peer, new RoomErrorCode() { roomError = RoomError.RandomRoomIdGetError });
                return;
            }
        }

        var room = new ServerBattleRoom(roomId, msg.roomShowInfo, msg.startBattleMsg, _serverSocket, msg.setting, _serverRandom);
        _allRooms.Add(roomId, room);

        JoinRoom(peer, new JoinRoomMsg()
        {
            roomId = roomId,
            joinMessage = msg.join,
            joinShowInfo = msg.joinShowInfo
        });

        Console.WriteLine($"CreateRoom:{roomId}");
    }

    List<(ServerBattleRoom, RoomEndReason)> _removeRooms = new List<(ServerBattleRoom, RoomEndReason)>();
    public void OnUpdate(float deltaTime)
    {
        _serverTime += deltaTime;
        _serverSocket.Update(deltaTime);

        foreach (var x in _allRooms.Values)
        {
            x.Update(deltaTime, _serverTime);
        }

        CheckClearRoom();
    }
    double _lastClearRoomTime = 0;

    private void CheckClearRoom()
    {
        if (_serverTime - _lastClearRoomTime < 1)
        {
            return;
        }

        _lastClearRoomTime = _serverTime;

        _removeRooms.Clear();
        foreach (var x in _allRooms.Values)
        {
            var removeRoomReason = x.NeedDestroy(_serverTime);
            if (removeRoomReason != RoomEndReason.None)
            {
                _removeRooms.Add((x, removeRoomReason));
            }
        }

        foreach (var room in _removeRooms)
        {
            RemoveRoom(room.Item1, room.Item2);
        }
    }

    void RemoveRoom(ServerBattleRoom room, RoomEndReason roomEndReason)
    {
        var allPeers = room.AllPeers.ToArray();
        room.ForceClose(roomEndReason == RoomEndReason.RoomMasterLeave ? SyncRoomOptMsg.RoomOpt.MasterLeaveRoomEnd : SyncRoomOptMsg.RoomOpt.RoomEnd);
        _allRooms.Remove(room.RoomId);

        foreach (var x in allPeers)
        {
            _allUserRooms.Remove(x);
        }

        Console.WriteLine($"RemoveRoom:{room.RoomId} {roomEndReason}");
    }

    internal string GetStatus()
    {
        return $"房间{_allRooms.Count}个, user{_allUserRooms.Count}个 Total内存:{GC.GetTotalMemory(false) / (1024f * 1024):0.00}M {_serverSocket.GetInfo()}";
    }

    public void Destroy()
    {
        _serverSocket.OnDestroy();
    }
}