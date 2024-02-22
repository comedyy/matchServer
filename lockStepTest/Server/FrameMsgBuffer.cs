using System;
using System.Collections.Generic;
using System.Linq;
using LiteNetLib.Utils;

struct FrameData
{
    public int targetPos;
    public int fromPos;
    public int messageCount;
}

public class FrameMsgBuffer
{
    const int TOTAL_LENGTH = 4096;
    byte[] _frameBuffer = new byte[TOTAL_LENGTH]; // 接收buffer
    ushort _position;
    byte _frameMsgCount;
    byte _totalMsgCount;
    int _lastRecordFrame = 0;
    List<byte[]> _allMessage = new List<byte[]>();
    Dictionary<int, FrameData> _dicFramePosition = new Dictionary<int, FrameData>();

    public void AddFromReader(NetDataReader reader)
    {
        var segment = reader.GetRemainingBytesSegment();
        var length = segment.Count;
        var remain = TOTAL_LENGTH - _position;

        if(remain < length)
        {
            Console.WriteLine($"remain buffer < incomingFrameMsg {remain} {length}");
            return;
        }

        Buffer.BlockCopy(segment.Array, segment.Offset, _frameBuffer, _position, length);
        _position += (ushort)length;
        _frameMsgCount++;
        _totalMsgCount++;
    }

    public byte TotalMsgCount => _totalMsgCount;
    public byte FrameCount => (byte)_dicFramePosition.Count;
    public int FromFrame => _lastRecordFrame - FrameCount + 1;
    public byte GetMsgCount(int frame)
    {
        if(_dicFramePosition.TryGetValue(frame, out var x))
        {
            return (byte)x.messageCount;
        }

        return 0;
    }

    public void WriterToWriter(NetDataWriter writer, int frame)
    {
        if(!_dicFramePosition.TryGetValue(frame, out var x))
        {
            Console.WriteLine($"no frame data {frame}");
            return;
        }

        if(x.fromPos == x.targetPos) return;

        writer.Put(_frameBuffer, x.fromPos, x.targetPos - x.fromPos);
        _allMessage.Add(writer.CopyData());
    }

    public void Reset()
    {
        _position = 0;
        _totalMsgCount = 0;
        _dicFramePosition.Clear();
    }

    internal ServerReconnectMsgResponse GetReconnectMsg(int clientCurrentFrame, Dictionary<int, int> finishedStageFrames)
    {
        List<byte[]> list = new List<byte[]>();
        list.AddRange(_allMessage.GetRange(clientCurrentFrame, _allMessage.Count - clientCurrentFrame));

        ServerReconnectMsgResponse response = new ServerReconnectMsgResponse(){
            startFrame = clientCurrentFrame,
            bytes = list, 
            stageFinishedFrames = finishedStageFrames.Select(m=>new IntPair2(){Item1 = m.Key, Item2 = m.Value}).ToArray()
        };

        return response;
    }

    internal void RecordSendFrame(ushort frame)
    {
        _lastRecordFrame = frame;
        var preFrame = frame - 1;
        if(!_dicFramePosition.TryGetValue(preFrame, out var x))
        {
            _dicFramePosition.Add(frame, new FrameData(){
                fromPos = 0, targetPos = _position, messageCount = _frameMsgCount
            });
        }
        else
        {
            _dicFramePosition.Add(frame, new FrameData(){
                fromPos = x.targetPos, targetPos = _position, messageCount = _frameMsgCount
            });
        }

        _frameMsgCount = 0;
    }
}