using System;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class TitleUpdatePatcher
    {
        public static void Apply(
            string extractedRoot,
            string xexToolPath,
            string titleUpdatePath,
            string workRoot,
            Action<int, string> progress)
        {
            string defaultXexPath = Path.Combine(extractedRoot, "default.xex");
            if (!File.Exists(defaultXexPath))
            {
                throw new FileNotFoundException("default.xex\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", defaultXexPath);
            }

            if (!File.Exists(xexToolPath))
            {
                throw new FileNotFoundException("xextool.exe\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", xexToolPath);
            }

            if (!File.Exists(titleUpdatePath))
            {
                throw new FileNotFoundException("TU \ud30c\uc77c\uc744 \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", titleUpdatePath);
            }

            string titleUpdateWorkRoot = Path.Combine(workRoot, "title_update");
            Directory.CreateDirectory(titleUpdateWorkRoot);
            string xexpPath = Path.Combine(titleUpdateWorkRoot, "default.xexp");
            string patchedXexPath = Path.Combine(titleUpdateWorkRoot, "default_tu.xex");

            Report(progress, 27, "TU \ud30c\uc77c \ud655\uc778 \uc911...");
            StfsTitleUpdateReader.ExtractDefaultXexp(titleUpdatePath, xexpPath);

            Report(progress, 29, "default.xex\uc5d0 \ud0c0\uc774\ud2c0 \uc5c5\ub370\uc774\ud2b8 \ubc18\uc601 \uc911...");
            string workingDirectory = Path.GetDirectoryName(xexToolPath);
            if (String.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Environment.CurrentDirectory;
            }

            ExternalToolRunner.Run(
                xexToolPath,
                "-u -p " + ExternalToolRunner.QuoteArgument(xexpPath)
                    + " -o " + ExternalToolRunner.QuoteArgument(patchedXexPath)
                    + " " + ExternalToolRunner.QuoteArgument(defaultXexPath),
                workingDirectory,
                "xextool.exe");

            if (!File.Exists(patchedXexPath) || new FileInfo(patchedXexPath).Length == 0)
            {
                throw new FileNotFoundException("TU\uac00 \ubc18\uc601\ub41c default.xex\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", patchedXexPath);
            }

            File.Copy(patchedXexPath, defaultXexPath, true);
            Report(progress, 31, "\ud0c0\uc774\ud2c0 \uc5c5\ub370\uc774\ud2b8 \ubc18\uc601 \uc644\ub8cc");
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }
    }

    internal static class StfsTitleUpdateReader
    {
        private const int BlockSize = 0x1000;
        private const int FileEntrySize = 0x40;
        private const int FileEntriesPerBlock = BlockSize / FileEntrySize;

        public static void ExtractDefaultXexp(string sourcePath, string destinationPath)
        {
            using (FileStream stream = File.OpenRead(sourcePath))
            {
                string magic = ReadAscii(stream, 0, 4);
                if (magic == "XEX2")
                {
                    CopyStreamToFile(stream, destinationPath);
                    ValidateXexp(destinationPath);
                    return;
                }

                if (magic != "LIVE" && magic != "PIRS" && magic != "CON ")
                {
                    throw new InvalidDataException("\uc9c0\uc6d0\ud558\uc9c0 \uc54a\ub294 TU \ud30c\uc77c \ud615\uc2dd\uc785\ub2c8\ub2e4.");
                }

                if (stream.Length < 0x3A0)
                {
                    throw new InvalidDataException("TU \ud30c\uc77c\uc758 STFS \ud5e4\ub354\uac00 \uc798\ubabb\ub418\uc5c8\uc2b5\ub2c8\ub2e4.");
                }

                uint headerSize = ReadUInt32BigEndian(stream, 0x340);
                long firstHashTableOffset = (headerSize + 0xFFFU) & 0xFFFFF000U;
                int blockSeparation = ReadByte(stream, 0x37B);
                int fileTableBlockCount = ReadUInt16LittleEndian(stream, 0x37C);
                int fileTableStartBlock = ReadUInt24LittleEndian(stream, 0x37E);
                if (fileTableBlockCount <= 0 || fileTableBlockCount > 0x1000)
                {
                    throw new InvalidDataException("TU \ud30c\uc77c\uc758 \ud30c\uc77c \ud14c\uc774\ube14 \ud06c\uae30\uac00 \uc798\ubabb\ub418\uc5c8\uc2b5\ub2c8\ub2e4.");
                }

                StfsFileEntry patchEntry = null;
                byte[] tableBlock = new byte[BlockSize];
                for (int tableIndex = 0; tableIndex < fileTableBlockCount && patchEntry == null; tableIndex++)
                {
                    int logicalBlock = checked(fileTableStartBlock + tableIndex);
                    ReadDataBlock(stream, magic, firstHashTableOffset, blockSeparation, logicalBlock, tableBlock);
                    for (int entryIndex = 0; entryIndex < FileEntriesPerBlock; entryIndex++)
                    {
                        int entryOffset = entryIndex * FileEntrySize;
                        int nameLengthAndFlags = tableBlock[entryOffset + 0x28];
                        int nameLength = nameLengthAndFlags & 0x3F;
                        if (nameLength <= 0 || nameLength > 0x28)
                        {
                            continue;
                        }

                        string name = Encoding.ASCII.GetString(tableBlock, entryOffset, nameLength);
                        bool isDirectory = (nameLengthAndFlags & 0x80) != 0;
                        if (isDirectory || !String.Equals(name, "default.xexp", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        patchEntry = new StfsFileEntry();
                        patchEntry.Name = name;
                        patchEntry.IsConsecutive = (nameLengthAndFlags & 0x40) != 0;
                        patchEntry.AllocatedBlocks = ReadUInt24LittleEndian(tableBlock, entryOffset + 0x29);
                        patchEntry.StartBlock = ReadUInt24LittleEndian(tableBlock, entryOffset + 0x2F);
                        uint fileSize = ReadUInt32BigEndian(tableBlock, entryOffset + 0x34);
                        if (fileSize == 0 || fileSize > Int32.MaxValue)
                        {
                            throw new InvalidDataException("default.xexp \ud06c\uae30\uac00 \uc798\ubabb\ub418\uc5c8\uc2b5\ub2c8\ub2e4.");
                        }
                        patchEntry.Size = (int)fileSize;
                    }
                }

                if (patchEntry == null)
                {
                    throw new FileNotFoundException("TU \ud30c\uc77c \uc548\uc5d0\uc11c default.xexp\ub97c \ucc3e\uc744 \uc218 \uc5c6\uc2b5\ub2c8\ub2e4.", sourcePath);
                }

                int requiredBlocks = checked((patchEntry.Size + BlockSize - 1) / BlockSize);
                if (requiredBlocks > patchEntry.AllocatedBlocks)
                {
                    throw new InvalidDataException("default.xexp\uc758 \ube14\ub85d \uc815\ubcf4\uac00 \uc798\ubabb\ub418\uc5c8\uc2b5\ub2c8\ub2e4.");
                }

                if (!patchEntry.IsConsecutive && requiredBlocks > 1)
                {
                    throw new InvalidDataException("\ube14\ub85d\uc774 \ubd84\ud560\ub41c TU \ud30c\uc77c\uc740 \uc9c0\uc6d0\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.");
                }

                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!String.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                byte[] dataBlock = new byte[BlockSize];
                int remaining = patchEntry.Size;
                using (FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    for (int blockIndex = 0; blockIndex < requiredBlocks; blockIndex++)
                    {
                        int logicalBlock = checked(patchEntry.StartBlock + blockIndex);
                        ReadDataBlock(stream, magic, firstHashTableOffset, blockSeparation, logicalBlock, dataBlock);
                        int writeCount = Math.Min(remaining, BlockSize);
                        output.Write(dataBlock, 0, writeCount);
                        remaining -= writeCount;
                    }
                }
            }

            ValidateXexp(destinationPath);
        }

        private static void ReadDataBlock(
            FileStream stream,
            string magic,
            long firstHashTableOffset,
            int blockSeparation,
            int logicalBlock,
            byte[] buffer)
        {
            long physicalBlock = ComputeDataBlockNumber(magic, firstHashTableOffset, blockSeparation, logicalBlock);
            long offset = checked(firstHashTableOffset + physicalBlock * BlockSize);
            if (offset < 0 || offset + BlockSize > stream.Length)
            {
                throw new InvalidDataException("TU \ud30c\uc77c\uc758 \ube14\ub85d \uc704\uce58\uac00 \uc798\ubabb\ub418\uc5c8\uc2b5\ub2c8\ub2e4.");
            }

            stream.Position = offset;
            ReadExactly(stream, buffer, 0, buffer.Length);
        }

        private static long ComputeDataBlockNumber(
            string magic,
            long firstHashTableOffset,
            int blockSeparation,
            int logicalBlock)
        {
            if (logicalBlock < 0 || logicalBlock > 0xFFFFFF)
            {
                throw new InvalidDataException("TU \ube14\ub85d \ubc88\ud638\uac00 \uc720\ud6a8\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.");
            }

            int blockShift;
            if (firstHashTableOffset == 0xB000)
            {
                blockShift = 1;
            }
            else if ((blockSeparation & 1) == 1)
            {
                blockShift = 0;
            }
            else
            {
                blockShift = 1;
            }

            long hashBlocks = (logicalBlock + 0xAA) / 0xAA;
            if (magic == "CON ")
            {
                hashBlocks <<= blockShift;
            }

            long physicalBlock = logicalBlock + hashBlocks;
            if (logicalBlock > 0xAA)
            {
                hashBlocks = (logicalBlock + 0x70E4) / 0x70E4;
                if (magic == "CON ")
                {
                    hashBlocks <<= blockShift;
                }
                physicalBlock += hashBlocks;

                if (logicalBlock > 0x70E4)
                {
                    hashBlocks = (logicalBlock + 0x4AF768) / 0x4AF768;
                    if (magic == "CON ")
                    {
                        hashBlocks <<= blockShift;
                    }
                    physicalBlock += hashBlocks;
                }
            }

            return physicalBlock;
        }

        private static void ValidateXexp(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.Length == 0 || ReadAscii(stream, 0, 4) != "XEX2")
                {
                    throw new InvalidDataException("TU\uc5d0\uc11c \ucd94\ucd9c\ud55c default.xexp\uac00 \uc720\ud6a8\ud558\uc9c0 \uc54a\uc2b5\ub2c8\ub2e4.");
                }
            }
        }

        private static void CopyStreamToFile(FileStream source, string destinationPath)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (!String.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            source.Position = 0;
            using (FileStream output = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
            }
        }

        private static string ReadAscii(FileStream stream, long offset, int count)
        {
            byte[] bytes = new byte[count];
            stream.Position = offset;
            ReadExactly(stream, bytes, 0, bytes.Length);
            return Encoding.ASCII.GetString(bytes);
        }

        private static int ReadByte(FileStream stream, long offset)
        {
            stream.Position = offset;
            int value = stream.ReadByte();
            if (value < 0)
            {
                throw new EndOfStreamException();
            }
            return value;
        }

        private static int ReadUInt16LittleEndian(FileStream stream, long offset)
        {
            byte[] bytes = new byte[2];
            stream.Position = offset;
            ReadExactly(stream, bytes, 0, bytes.Length);
            return bytes[0] | (bytes[1] << 8);
        }

        private static int ReadUInt24LittleEndian(FileStream stream, long offset)
        {
            byte[] bytes = new byte[3];
            stream.Position = offset;
            ReadExactly(stream, bytes, 0, bytes.Length);
            return ReadUInt24LittleEndian(bytes, 0);
        }

        private static int ReadUInt24LittleEndian(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
        }

        private static uint ReadUInt32BigEndian(FileStream stream, long offset)
        {
            byte[] bytes = new byte[4];
            stream.Position = offset;
            ReadExactly(stream, bytes, 0, bytes.Length);
            return ReadUInt32BigEndian(bytes, 0);
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read <= 0)
                {
                    throw new EndOfStreamException();
                }
                offset += read;
                count -= read;
            }
        }

        private sealed class StfsFileEntry
        {
            public string Name;
            public bool IsConsecutive;
            public int AllocatedBlocks;
            public int StartBlock;
            public int Size;
        }
    }
}
