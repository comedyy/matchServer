using System;
using System.Collections.Generic;
using LiteNetLib.Utils;

public class FrameMsgBuffer
{
    const int TOTAL_LENGTH = 4096;
    byte[] _frameBuffer = new byte[TOTAL_LENGTH]; // 接收buffer
    ushort _position;
    byte _msgCount;

    public void AddFromReader(NetDataReader reader)
    {
        var segment = reader.GetRemainingBytesSegment();
        var length = segment.Count;
        var remain = TOTAL_LENGTH - _position;

        if(remain < length)
        {
            Console.WriteLine("remain buffer < incomingFrameMsg {remain} {length}");
            return;
        }

        Buffer.BlockCopy(segment.Array, segment.Offset, _frameBuffer, _position, length);
        _position += (ushort)length;
        _msgCount++;
    }

    public byte Count => _msgCount;
    public void WriterToWriter(NetDataWriter writer)
    {
        writer.Put(_frameBuffer, 0, _position);
        _position = 0;
        _msgCount = 0;
    }
}