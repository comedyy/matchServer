using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

public class GameServerSocket : IServerGameSocket, INetEventListener
{
    private NetManager _netServer;
    private List<NetPeer> _ourPeers = new List<NetPeer>();
    private NetDataWriter _dataWriter;
    private int countUser;

    public GameServerSocket(int countUser)
    {
        this.countUser = countUser;
    }

    #region ILifeCircle
    public void Start()
    {
        _dataWriter = new NetDataWriter();
        _netServer = new NetManager(this);
        _netServer.Start(5000);
        _netServer.BroadcastReceiveEnabled = true;
        _netServer.UpdateTime = 15;
    }

    public void Update()
    {
        _netServer.PollEvents();
    }

    public void OnDestroy()
    {
        if (_netServer != null)
            _netServer.Stop();
    }
#endregion
    
#region IMessageSendReceive
    public Action<NetPeer, NetDataReader> OnReceiveMsg{get;set;}

    public void SendMessage<T>(List<NetPeer> list, T t) where T : INetSerializable
    {
        if(list.Count == 0) return;

        _dataWriter.Reset();
        t.Serialize(_dataWriter);

        foreach (var peer in list)
        {
            peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
        }
    }
#endregion

#region INetEventListener
    public void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine("[SERVER] We have new peer " + peer.EndPoint);
        _ourPeers.Add(peer);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketErrorCode)
    {
         Console.WriteLine("[SERVER] error " + socketErrorCode);
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader,
        UnconnectedMessageType messageType)
    {
        if (messageType == UnconnectedMessageType.Broadcast)
        {
            if(_ourPeers.Count >= countUser)
            {
                return;
            }

             Console.WriteLine("[SERVER] Received discovery request. Send discovery response");
            NetDataWriter resp = new NetDataWriter();
            resp.Put(1);
            _netServer.SendUnconnectedMessage(resp, remoteEndPoint);
        }
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
    {
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("sample_app");
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
         Console.WriteLine("[SERVER] peer disconnected " + peer.EndPoint + ", info: " + disconnectInfo.Reason);
        _ourPeers.Remove(peer);

        OnPeerDisconnect?.Invoke(peer);
    }

    Dictionary<int, JoinMessage> _joins = new Dictionary<int, JoinMessage>();
    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        var msgType = (MsgType1)reader.PeekByte();
        OnReceiveMsg(peer, reader);
    }

    #endregion

    // public void WriteNet(NetLogLevel level, string str, params object[] args)
    // {
    //     Console.WriteLineFormat(str, args);
    // }

    // public void BroadCastBattleStart(BattleStartMessage msg)
    // {
    //     var joins = new JoinMessage[_ourPeers.Count];
    //     for(int i = 0; i < _ourPeers.Count; i++)
    //     {
    //         joins[i] = _joins[_ourPeers[i].Id];
    //     }

    //     msg.joins = joins;

    //     for(int i = 0; i < _ourPeers.Count; i++)
    //     {
    //         var peer = _ourPeers[i];
    //         _dataWriter.Reset();
    //         _dataWriter.Put(msg);

    //         peer.Send(_dataWriter, DeliveryMethod.ReliableOrdered);
    //     }
    // }



    public int Count => _ourPeers.Count;

    public Action<NetPeer> OnPeerDisconnect{get;set;}
}
