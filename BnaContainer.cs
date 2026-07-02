using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class BnaContainer
    {
        private readonly List<BnaContainerEntry> entries;

        private BnaContainer(List<BnaContainerEntry> entries)
        {
            this.entries = entries;
        }

        public List<BnaContainerEntry> Entries
        {
            get { return entries; }
        }

        public static bool IsBna(byte[] data)
        {
            return data.Length >= 8
                && data[0] == (byte)'B'
                && data[1] == (byte)'N'
                && data[2] == (byte)'A'
                && data[3] == (byte)'0';
        }

        public static BnaContainer Parse(byte[] data)
        {
            if (!IsBna(data))
            {
                throw new InvalidDataException("BNA0 magic missing.");
            }

            int count = checked((int)ReadU32(data, 4));
            int tableEnd = checked(8 + count * 16);
            if (tableEnd > data.Length)
            {
                throw new InvalidDataException("BNA table extends past file end.");
            }

            List<BnaContainerEntry> rows = new List<BnaContainerEntry>();
            for (int index = 0; index < count; index++)
            {
                int rowOffset = 8 + index * 16;
                string dirName = ReadCString(data, checked((int)ReadU32(data, rowOffset)));
                string fileName = ReadCString(data, checked((int)ReadU32(data, rowOffset + 4)));
                int dataOffset = checked((int)ReadU32(data, rowOffset + 8));
                int size = checked((int)ReadU32(data, rowOffset + 12));
                if (dataOffset < 0 || size < 0 || dataOffset + size > data.Length)
                {
                    throw new InvalidDataException("BNA entry extends past file end.");
                }

                byte[] entryData = new byte[size];
                Buffer.BlockCopy(data, dataOffset, entryData, 0, size);

                string path;
                if (dirName.Length > 0 && fileName.Length > 0)
                {
                    path = dirName + "/" + fileName;
                }
                else
                {
                    path = fileName.Length > 0 ? fileName : dirName;
                }

                rows.Add(new BnaContainerEntry(index, dirName, fileName, NormalizePath(path), entryData));
            }

            return new BnaContainer(rows);
        }

        public byte[] Rebuild()
        {
            MemoryStream stream = new MemoryStream();
            WriteAscii(stream, "BNA0");
            WriteU32(stream, (uint)entries.Count);

            long tablePosition = stream.Position;
            for (int index = 0; index < entries.Count * 16; index++)
            {
                stream.WriteByte(0);
            }

            Dictionary<string, uint> nameOffsets = new Dictionary<string, uint>(StringComparer.Ordinal);
            List<BnaTableRow> tableRows = new List<BnaTableRow>();
            for (int index = 0; index < entries.Count; index++)
            {
                tableRows.Add(new BnaTableRow
                {
                    DirectoryOffset = GetNameOffset(stream, nameOffsets, entries[index].DirectoryName),
                    FileOffset = GetNameOffset(stream, nameOffsets, entries[index].FileName),
                    Size = checked((uint)entries[index].Data.Length)
                });
            }

            for (int index = 0; index < entries.Count; index++)
            {
                PadStream(stream, 0x80, 0);
                BnaTableRow row = tableRows[index];
                row.DataOffset = checked((uint)stream.Position);
                tableRows[index] = row;
                byte[] entryData = entries[index].Data;
                stream.Write(entryData, 0, entryData.Length);
            }

            byte[] output = stream.ToArray();
            for (int index = 0; index < tableRows.Count; index++)
            {
                int rowOffset = checked((int)tablePosition + index * 16);
                WriteU32(output, rowOffset, tableRows[index].DirectoryOffset);
                WriteU32(output, rowOffset + 4, tableRows[index].FileOffset);
                WriteU32(output, rowOffset + 8, tableRows[index].DataOffset);
                WriteU32(output, rowOffset + 12, tableRows[index].Size);
            }

            return output;
        }

        private static uint GetNameOffset(MemoryStream stream, Dictionary<string, uint> offsets, string name)
        {
            if (name == null)
            {
                name = String.Empty;
            }

            uint offset;
            if (offsets.TryGetValue(name, out offset))
            {
                return offset;
            }

            offset = checked((uint)stream.Position);
            offsets[name] = offset;
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte(0);
            return offset;
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        private static void WriteU32(Stream stream, uint value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteAscii(Stream stream, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void PadStream(Stream stream, int boundary, byte value)
        {
            while ((stream.Position % boundary) != 0)
            {
                stream.WriteByte(value);
            }
        }

        private static string ReadCString(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length)
            {
                return String.Empty;
            }

            int end = offset;
            while (end < data.Length && data[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(data, offset, end - offset);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private struct BnaTableRow
        {
            public uint DirectoryOffset;
            public uint FileOffset;
            public uint DataOffset;
            public uint Size;
        }
    }

    internal sealed class BnaContainerEntry
    {
        public readonly int Index;
        public readonly string DirectoryName;
        public readonly string FileName;
        public readonly string Path;
        public byte[] Data;

        public BnaContainerEntry(int index, string directoryName, string fileName, string path, byte[] data)
        {
            Index = index;
            DirectoryName = directoryName;
            FileName = fileName;
            Path = path;
            Data = data;
        }
    }
}
