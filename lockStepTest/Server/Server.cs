using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib;
using LiteNetLib.Utils;

enum GameState
{
    NotBegin,
    Running,
    End,
}

public class Server
{
    public int frame;
    public float totalSeconds;
    public float preFrameSeconds;
    float _tick;
    IServerGameSocket _socket;
    private List<NetPeer> _netPeers;
    List<MessageItem> _currentFrameMessage = new List<MessageItem>();

    HashChecker _hashChecker;
    int pauseFrame = -1;
    
    GameState _gameState = GameState.NotBegin;
    private int _stageIndex;
    int[] _stages;
    int[] _finishRooms;

    FrameMsgBuffer _frameMsgBuffer = new FrameMsgBuffer();

    public Server(float tick, IServerGameSocket socket, List<NetPeer> netPeers)
    {
        frame = 0;
        totalSeconds = 0;
        preFrameSeconds = 0;
        _tick = tick;
        _socket = socket;
        _netPeers = netPeers;
    }

    bool IsPause => pauseFrame != int.MaxValue;

    public void Update(float deltaTime)
    {
        if(_gameState != GameState.Running) return;
        if(pauseFrame <= frame) return; // 用户手动暂停
        if(!IsPause && deltaTime == 0) return; // // TImeScale == 0 并且未暂停，就是游戏在初始化
        
        totalSeconds += deltaTime;
        if(preFrameSeconds + _tick > totalSeconds)
        {
            return;
        }

        preFrameSeconds += _tick;

        frame++;
        BroadCastMsg();

        // if (_currentFrameMessage != null) 
        // {
        //     _currentFrameMessage = null;
        // }
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

                if(stageIndx == 999)
                {
                    // End battle
                    _gameState = GameState.End;
                }
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

        // PackageItem packageItem = reader.Get<PackageItem >();

        // if(IsPause && CheckPauseStateOpt(packageItem.messageItem.messageBit))
        // {
        //     pauseFrame ++;
        // }

        reader.GetByte(); // reader去掉msgType
        _frameMsgBuffer.AddFromReader(reader);

        // if(_currentFrameMessage ==  null) 
        // {
        //     _currentFrameMessage = new List<MessageItem>();
        // }

        // _currentFrameMessage.Add(packageItem.messageItem);
    }

    private bool CheckPauseStateOpt(MessageBit messageBit)
    {
        return (messageBit & (MessageBit.ChooseSkill | MessageBit.RechooseSkill | MessageBit.CullSkill | MessageBit.Reborn | MessageBit.ExitGame)) > 0;
    }

    private void BroadCastMsg()
    {
        // Debug.LogError(list == null ? 0 : list.Count);
        // Debug.LogError($"Server:Send package  {frame} {Time.time}" );
        _socket.SendMessage(_netPeers, new ServerPackageItem(){
            frame = (ushort)frame, clientFrameMsgList = _frameMsgBuffer
        });
    }

    public void StartBattle(BattleStartMessage startMessage)
    {
        _hashChecker = new HashChecker(_netPeers.Count);
        _stages = new int[_netPeers.Count];
        _finishRooms = new int[_netPeers.Count];
        _gameState = GameState.Running;
        
        _socket.SendMessage(_netPeers, startMessage);
    }

    public void Destroy()
    {
        _socket.OnDestroy();
    }
}