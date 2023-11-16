using Game;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

public class BattleRoom
{
    Server _server;
    List<(NetPeer, JoinMessage)> _netPeers = new List<(NetPeer, JoinMessage)>();
    public int RoomId{get; private set;}
    public int MemberCount => _netPeers.Count;
    BattleStartMessage _startBattle;
    IServerGameSocket _socket;
    private int _speed = 1;

    public bool IsStart {get; private set;}
    public NetPeer Master => _netPeers[0].Item1;
    public List<NetPeer> AllPeers => _netPeers.Select(m=>m.Item1).ToList();

    internal string roomName
    {
        get
        {
            var battleType = _startBattle.battleType == (int)LevelType.Challenge ? "挑战" : "主线";
            return $"{battleType}_{_startBattle.levelId}";
        }
    }

    public BattleRoom(int id, BattleStartMessage startBattle, IServerGameSocket socket)
    {
        RoomId = id;
        _startBattle = startBattle;
        _socket = socket;
    }

    public void AddPeer(NetPeer peer, JoinMessage joinMessage)
    {
        var index = _netPeers.FindIndex(m=>m.Item1 == peer);
        if(index < 0) 
        {
            _netPeers.Add((peer, joinMessage));
        }
        else
        {
            _netPeers[index] = (peer, joinMessage);
        }

        BroadcastRoomInfo();
    }

    public void StartBattle(NetPeer peer)
    {
        if(_netPeers.Count == 0) return;
        if(peer != _netPeers[0].Item1) return;

        IsStart = true;

        _server = new Server(0.05f, _socket, _netPeers.Select(m=>m.Item1).ToList());
        _startBattle.joins = _netPeers.Select(m=>m.Item2).ToArray();
        _server.StartBattle(_startBattle);
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
            userList = _netPeers.Select(m=>new RoomUser(){name = m.Item2.name, HeroId = m.Item2.HeroId, userId = m.Item2.userId}).ToArray()
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