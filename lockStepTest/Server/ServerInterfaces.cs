
using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

public enum ConnectResult
{
    Connecting,
    Refuse,
    Connnected,
}

public interface IServerGameSocket : ILifeCircle
{
    // void BroadCastBattleStart(BattleStartMessage msg);
    int Count{get;}
    Action<NetPeer> OnPeerDisconnect { get; set; }
    void SendMessage<T>(List<NetPeer> peers, T t) where T : INetSerializable;
    Action<NetPeer, NetDataReader> OnReceiveMsg{get;set;}
}

public interface IClientGameSocket : ILifeCircle
{
    Action<BattleStartMessage> OnStartBattle{get;set;}
    ConnectResult connectResult{get;}
    int RoundTripTime{get;}
    void SendMessage<T>( T t) where T : INetSerializable;
    void SendMessageNotReliable<T>( T t) where T : INetSerializable;
    Action<NetDataReader> OnReceiveMsg{get;set;}
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
