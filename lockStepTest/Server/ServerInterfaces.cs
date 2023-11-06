
using System;
using LiteNetLib;
using LiteNetLib.Utils;

public enum ConnectResult
{
    Connecting,
    Refuse,
    Connnected,
}

public interface IServerGameSocket : IGameSocket
{
    // void BroadCastBattleStart(BattleStartMessage msg);
    int Count{get;}
    Action<NetPeer> OnPeerDisconnect { get; set; }
}

public interface IClientGameSocket : IGameSocket
{
    Action<BattleStartMessage> OnStartBattle{get;set;}
    ConnectResult connectResult{get;}
    int RoundTripTime{get;}
}


public interface IGameSocket : IMessageSendReceive, ILifeCircle
{
    
}

public interface IMessageSendReceive
{
    void SendMessage<T>(List<NetPeer> peers, T t) where T : INetSerializable;
    Action<NetPeer, NetDataReader> OnReceiveMsg{get;set;}
}

public interface ILifeCircle
{
    void Start();
    void Update();
    void OnDestroy();
}


struct SendItem
{
    public float addTime;
    public byte[] bytes;
}

struct ReceiveItem
{
    public float addTime;
    public byte[] bytes;
}

interface IConnectionCount
{
    int Count{get;}
}
