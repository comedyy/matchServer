using Game;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

public class BattleRoom
{
    Server _server;
    List<(NetPeer, byte[], string, uint, uint)> _netPeers = new List<(NetPeer, byte[], string, uint, uint)>();
    public int RoomId{get; private set;}
    public int MemberCount => _netPeers.Count;
    byte[] _startBattle;
    IServerGameSocket _socket;
    private int _speed = 1;

    public bool IsStart {get; private set;}
    public NetPeer Master => _netPeers[0].Item1;
    public List<NetPeer> AllPeers => _netPeers.Select(m=>m.Item1).ToList();
    ServerSetting _setting; 

    public bool CheckMasterLeaveShouldDestroyRoom()
    {
        return _setting.masterLeaveOpt == RoomMasterLeaveOpt.RemoveRoomAndBattle 
            || (_setting.masterLeaveOpt == RoomMasterLeaveOpt.OnlyRemoveRoomBeforeBattle && _server == null);
    }

    internal string roomName{get; private set;}

    public BattleRoom(int id, string battleName, byte[] startBattle, IServerGameSocket socket, ServerSetting setting)
    {
        roomName = battleName;
        RoomId = id;
        _startBattle = startBattle;
        _socket = socket;
        _setting = setting;
    }

    public void AddPeer(NetPeer peer, byte[] joinMessage, string name, uint userId, uint heroId)
    {
        var index = _netPeers.FindIndex(m=>m.Item1 == peer);
        if(index < 0) 
        {
            _netPeers.Add((peer, joinMessage, name, userId, heroId));
        }
        else
        {
            _netPeers[index] = (peer, joinMessage, name, userId, heroId);
        }

        BroadcastRoomInfo();
    }

    public void StartBattle(NetPeer peer)
    {
        if(_netPeers.Count == 0) return;
        if(peer != _netPeers[0].Item1) return;

        IsStart = true;

        _server = new Server(_setting, _socket, _netPeers.Select(m=>m.Item1).ToList());

        var startMessage = new RoomStartBattleMsg
        {
            joinMessages = _netPeers.Select(m => m.Item2).ToList(),
            StartMsg = _startBattle
        };
        _server.StartBattle(startMessage);
    }

    public void OnReceiveMsg(NetDataReader reader)
    {
        _server.AddMessage(reader);
    }

    public void Update(float deltaTime)
    {
        for(int i = 0; i < _speed; i++)
        {
            _server?.Update(deltaTime);
        }
    }

    public bool IsBattleEnd => _server != null && _server.IsBattleEnd;

    internal void RemovePeer(NetPeer peer)
    {
        _netPeers.RemoveAll(m=> m.Item1 == peer);

        BroadcastRoomInfo();
    }

    void BroadcastRoomInfo()
    {
        if(_netPeers.Count == 0) return;

        _socket.SendMessage(_netPeers.Select(m=>m.Item1).ToList(), new UpdateRoomMemberList(){
            roomId = RoomId,
            userList = _netPeers.Select(m=>new RoomUser(){name = m.Item3, HeroId = m.Item5, userId = m.Item4}).ToArray()
        });
    }

    internal void SetRoomSpeed(NetPeer peer, int speed)
    {
        _speed = speed;
    }

    internal void ForceClose()
    {
        _socket.SendMessage(_netPeers.Select(m=>m.Item1).ToList(), new UpdateRoomMemberList());
        _netPeers.Clear();
    }
}