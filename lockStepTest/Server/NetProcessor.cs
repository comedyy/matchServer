
using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

enum RoomEndReason
{
    RoomMasterLeave,
    BattleEnd,
}

public class NetProcessor 
{
    Dictionary<NetPeer, BattleRoom> _allUserRooms = new Dictionary<NetPeer, BattleRoom>();
    Dictionary<int, BattleRoom> _allRooms = new Dictionary<int, BattleRoom>();
    static int _roomId = 0;
    IServerGameSocket _serverSocket;
    public NetProcessor(IServerGameSocket socket)
    {
        _serverSocket = socket;
        _serverSocket.Start();
        _serverSocket.OnReceiveMsg = OnReceiveMsg;
        _serverSocket.OnPeerDisconnect = OnLeave;
    }

    private void OnReceiveMsg(NetPeer peer, NetDataReader reader)
    {
        var msgType = (MsgType1)reader.PeekByte();
        switch(msgType)
        {
            case MsgType1.CreateRoom: CreateRoom(peer, reader.Get<CreateRoomMsg>()); break;
            case MsgType1.JoinRoom: JoinRoom(peer, reader.Get<JoinRoomMsg>()); break;
            case MsgType1.StartRequest : StartBattle(peer, reader.Get<StartBattleRequest>()); break;
            case MsgType1.SetSpeed: SetRoomSpeed(peer, reader.Get<SetServerSpeedMsg>()); break;
            case MsgType1.GetAllRoomList:
                _serverSocket.SendMessage(new List<NetPeer>(){peer}, GetRoomListMsg());
                break;
            default:
                if(_allUserRooms.TryGetValue(peer, out var room))
                {
                    room.OnReceiveMsg(reader);
                }
                break;
        }
    }

    private void SetRoomSpeed(NetPeer peer, SetServerSpeedMsg setServerSpeedMsg)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.SetRoomSpeed(peer, setServerSpeedMsg.speed);
        }
    }

    private RoomListMsg GetRoomListMsg()
    {
        var roomList = _allRooms.Values.Where(m=>!m.IsStart).Select(m=>new RoomInfoMsg(){
            name = m.roomName, count = m.MemberCount, roomId = m.RoomId
        });

        return new RoomListMsg(){
            roomList = roomList.ToArray()
        };
    }

    private void StartBattle(NetPeer peer, StartBattleRequest startBattleRequest)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            room.StartBattle(peer);
        }
    }

    void OnLeave(NetPeer peer)
    {
        if(_allUserRooms.TryGetValue(peer, out var room))
        {
            var master = room.Master;
            if(master == peer)
            {
                RemoveRoom(room, RoomEndReason.RoomMasterLeave);
            }
            else
            {
                room.RemovePeer(peer);
                _allUserRooms.Remove(peer);
            }
        }
    }

    private void JoinRoom(NetPeer peer, JoinRoomMsg joinRoomMsg)
    {
        if(_allRooms.TryGetValue(joinRoomMsg.roomId, out var room))
        {
            room.AddPeer(peer, joinRoomMsg.joinMessage);
            _allUserRooms.Add(peer, room);
        }
    }

    void CreateRoom(NetPeer peer, CreateRoomMsg msg)
    {
        var roomId = ++_roomId;
        var room = new BattleRoom(roomId, msg.startBattle, _serverSocket);
        _allRooms.Add(roomId, room);

        JoinRoom(peer, new JoinRoomMsg(){
            roomId = roomId, joinMessage = msg.join
        });

        Console.WriteLine($"CreateRoom:{roomId}");
    }

    List<BattleRoom> _removeRooms = new List<BattleRoom>();
    public void OnUpdate(float deltaTime)
    {
        _serverSocket.Update();

        _removeRooms.Clear();
        foreach(var x in _allRooms.Values)
        {
            x.Update(deltaTime);
            if(x.IsBattleEnd)
            {
                _removeRooms.Add(x);
            }
        }

        foreach(var room in _removeRooms)
        {
            RemoveRoom(room, RoomEndReason.BattleEnd);
        }
    }

    void RemoveRoom(BattleRoom room, RoomEndReason roomEndReason)
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
}