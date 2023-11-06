using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

public class Server
{
    public int frame;
    public float totalSeconds;
    public float preFrameSeconds;
    float _tick;
    // event Action<byte[]> FrameCallback;
    IServerGameSocket _socket;
    private List<NetPeer> _netPeers;
    Dictionary<int , List<MessageItem>> _allMessage = new Dictionary<int, List<MessageItem>>();
    List<MessageItem> _currentFrameMessage = new List<MessageItem>();

    HashChecker _hashChecker;
    int pauseFrame = -1;

    public Server(float tick, IServerGameSocket socket, List<NetPeer> netPeers)
    {
        frame = 0;
        totalSeconds = 0;
        preFrameSeconds = 0;
        _tick = tick;
        _allMessage.Clear();
        _socket = socket;
        _netPeers = netPeers;
    }

    bool IsPause => pauseFrame != int.MaxValue;

    public void Update(float deltaTime)
    {
        if(!start) return;
        if(pauseFrame <= frame) return; // 用户手动暂停
        if(!IsPause && deltaTime == 0) return; // // TImeScale == 0 并且未暂停，就是游戏在初始化
        
        totalSeconds += deltaTime;
        if(preFrameSeconds + _tick > totalSeconds)
        {
            return;
        }

        preFrameSeconds += _tick;

        if(_allMessage.TryGetValue(frame, out var list))
        {
            _allMessage.Remove(frame);
        }

        frame++;
        BroadCastMsg(_currentFrameMessage);

        if (_currentFrameMessage != null) 
        {
            _currentFrameMessage = null;
        }
    }

    public void AddMessage(NetDataReader reader)
    {
        var msgType = reader.PeekByte();
        if(msgType == (byte)MsgType1.HashMsg)
        {
            FrameHash hash = reader.Get<FrameHash>();
            string[] unsyncs = _hashChecker.AddHash(hash);
            if(unsyncs != null)
            {
                _socket.SendMessage(_netPeers, new UnSyncMsg()
                {
                    unSyncInfos = unsyncs
                });
            }

            return;
        }
        else if(msgType == (byte)MsgType1.ReadyForNextStage)
        {
            ReadyStageMsg ready = reader.Get<ReadyStageMsg>();
            var roomIndex = ready.stageIndex;
            var id = ready.id;
            _stages[id] = roomIndex;
            var stageIndx = _stages.Min();

            if(stageIndx > _stageIndex)
            {
                _stageIndex = stageIndx;
                _socket.SendMessage(_netPeers, new ServerReadyForNextStage(){
                    stageIndex = _stageIndex,
                    frameIndex = frame
                });

                pauseFrame = int.MaxValue;
            }
            
            return;
        }
        else if(msgType == (byte)MsgType1.FinishCurrentStage)
        {
            FinishRoomMsg ready = reader.Get<FinishRoomMsg>();
            var roomIndex = ready.stageIndex;
            var id = ready.id;
            _finishRooms[id] = roomIndex;
            var stageIndx = _finishRooms.Min();

            if(stageIndx > _stageIndex)
            {
                pauseFrame = frame;
 
                _socket.SendMessage(_netPeers, new ServerEnterLoading(){
                    frameIndex = frame,
                });
            }
            
            return;
        }
        else if(msgType == (byte)MsgType1.PauseGame)
        {
            PauseGameMsg pause = reader.Get<PauseGameMsg>();

            if(pause.pause)
            {
                pauseFrame = frame + 1;
            }
            else
            {
                pauseFrame = int.MaxValue;
            }

            _socket.SendMessage(_netPeers, pause);
            return;
        }

        PackageItem packageItem = reader.Get<PackageItem >();

        if(IsPause && CheckPauseStateOpt(packageItem.messageItem.messageBit))
        {
            pauseFrame ++;
        }


        if(_currentFrameMessage ==  null) 
        {
            _currentFrameMessage = new List<MessageItem>();
        }

        // 已经有这个对象的消息了，覆盖
        for(int i = 0; i < _currentFrameMessage.Count; i++)
        {
            if(_currentFrameMessage[i].id == packageItem.messageItem.id)
            {
                _currentFrameMessage[i] = packageItem.messageItem;
                return;
            }
        }

        _currentFrameMessage.Add(packageItem.messageItem);
    }

    private bool CheckPauseStateOpt(MessageBit messageBit)
    {
        return (messageBit & (MessageBit.ChooseSkill | MessageBit.RechooseSkill | MessageBit.CullSkill | MessageBit.Reborn)) > 0;
    }

    private void BroadCastMsg(List<MessageItem> list)
    {
        // Debug.LogError(list == null ? 0 : list.Count);
        // Debug.LogError($"Server:Send package  {frame} {Time.time}" );
        _socket.SendMessage(_netPeers, new ServerPackageItem(){
            frame = (ushort)frame, list = list
        });
    }

    bool start = false;
    private int _stageIndex;
    int[] _stages;
    int[] _finishRooms;

    public void StartBattle(BattleStartMessage startMessage)
    {
        _hashChecker = new HashChecker(_netPeers.Count);
        _stages = new int[_netPeers.Count];
        _finishRooms = new int[_netPeers.Count];
        start = true;
        
        _socket.SendMessage(_netPeers, startMessage);
    }

    public void Destroy()
    {
        _socket.OnDestroy();
    }
}