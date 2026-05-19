namespace Chat.Shared.Models;

/// <summary>
/// Fixed-size binary header prepended to every file chunk sent over the file transfer channel.
/// Layout (52 bytes): FileId (16) + ChunkIndex (8) + PayloadSize (8) + TotalSize (8) + IsLast flag as int (4).
/// </summary>
public struct FileChunkHeader
{
    public const int HeaderSize = 44; // Guid(16) + long(8) + int(4) + long(8) + int(4) + int(4)

    public Guid FileId;
    public long ChunkIndex;
    public int PayloadSize;
    public long TotalFileSize;
    public int IsLastChunk; // 1 = true, 0 = false
    public int Reserved;    // alignment / future use

    public byte[] ToBytes()
    {
        byte[] buffer = new byte[HeaderSize];
        int offset = 0;

        Buffer.BlockCopy(FileId.ToByteArray(), 0, buffer, offset, 16);
        offset += 16;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 8), ChunkIndex);
        offset += 8;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), PayloadSize);
        offset += 4;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 8), TotalFileSize);
        offset += 8;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), IsLastChunk);
        offset += 4;

        BitConverter.TryWriteBytes(buffer.AsSpan(offset, 4), Reserved);

        return buffer;
    }

    public static FileChunkHeader FromBytes(byte[] buffer)
    {
        int offset = 0;

        FileChunkHeader header = new()
        {
            FileId = new Guid(buffer.AsSpan(offset, 16))
        };
        offset += 16;

        header.ChunkIndex = BitConverter.ToInt64(buffer, offset);
        offset += 8;

        header.PayloadSize = BitConverter.ToInt32(buffer, offset);
        offset += 4;

        header.TotalFileSize = BitConverter.ToInt64(buffer, offset);
        offset += 8;

        header.IsLastChunk = BitConverter.ToInt32(buffer, offset);
        offset += 4;

        header.Reserved = BitConverter.ToInt32(buffer, offset);

        return header;
    }
}
