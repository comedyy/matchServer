using LiteNetLib;
using LiteNetLib.Utils;
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
    public bool IsStart {get; private set;}
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

        _socket.SendMessage(_netPeers.Select(m=>m.Item1).ToList(), new UpdateRoomMemberList(){
            userList = _netPeers.Select(m=>new RoomUser(){name = m.Item2.name, HeroId = m.Item2.HeroId}).ToArray()
        });
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
        _server?.Update(deltaTime);
    }

    internal void RemovePeer(NetPeer peer)
    {
        _netPeers.RemoveAll(m=> m.Item1 == peer);
        
        if(_netPeers.Count > 0)
        {
            _socket.SendMessage(_netPeers.Select(m=>m.Item1).ToList(), new UpdateRoomMemberList(){
                userList = _netPeers.Select(m=>new RoomUser(){name = m.Item2.name, HeroId = m.Item2.HeroId}).ToArray()
            });
        }
    }
}