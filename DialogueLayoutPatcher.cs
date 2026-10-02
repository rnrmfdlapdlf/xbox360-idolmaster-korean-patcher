using System;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class DialogueLayoutPatcher
    {
        private const string EntryPath = "root/widget/system_widget.bxr";
        private const string OriginalX = "260";
        private const string PreviousTestX = "196";
        private const string TargetX = "228";

        public static void PatchExtractedRoot(string extractedRoot)
        {
            string path = Path.Combine(extractedRoot, "root/widget/widget.bna");
            BnaArchive archive = BnaArchive.Load(path);
            byte[] data = archive.ReadEntry(EntryPath);
            if (PatchBxr(data))
            {
                archive.ReplaceEntry(EntryPath, data);
                archive.Save(path);
            }
        }

        internal static bool PatchBxr(byte[] data)
        {
            if (data.Length < 24 || Encoding.ASCII.GetString(data, 0, 4) != "BXR0")
            {
                throw new InvalidDataException("Dialogue layout BXR0 magic missing.");
            }

            int typeCount = ReadInt(data, 4);
            int attributeTypeCount = ReadInt(data, 8);
            int nodeCount = ReadInt(data, 12);
            int attributeCount = ReadInt(data, 16);
            int poolSize = ReadInt(data, 20);
            int nodeStart = checked(24 + (typeCount + attributeTypeCount) * 4);
            int attributeStart = checked(nodeStart + nodeCount * 28);
            int poolStart = checked(attributeStart + attributeCount * 12);
            if (poolSize <= 0 || checked(poolStart + poolSize) != data.Length)
            {
                throw new InvalidDataException("Invalid dialogue layout BXR sections.");
            }

            int targetAttribute = -1;
            for (int index = 0; index < nodeCount; index++)
            {
                int node = nodeStart + index * 28;
                int value = ReadInt(data, node + 12);
                if (value < 0 || ReadPoolString(data, poolStart, value) != "m_txt_message")
                {
                    continue;
                }

                int type = ReadInt(data, node + 8);
                if (type < 0 || type >= typeCount ||
                    ReadPoolString(data, poolStart, ReadInt(data, 24 + type * 4)) != "text")
                {
                    throw new InvalidDataException("Dialogue message node is not text.");
                }

                string y = null;
                string size = null;
                int attribute = ReadInt(data, node + 16);
                int visited = 0;
                while (attribute != -1)
                {
                    if (attribute < 0 || attribute >= attributeCount || ++visited > attributeCount)
                    {
                        throw new InvalidDataException("Invalid dialogue attribute chain.");
                    }
                    int offset = attributeStart + attribute * 12;
                    int attributeType = ReadInt(data, offset + 4);
                    if (attributeType < 0 || attributeType >= attributeTypeCount)
                    {
                        throw new InvalidDataException("Invalid dialogue attribute type.");
                    }
                    string name = ReadPoolString(data, poolStart,
                        ReadInt(data, 24 + (typeCount + attributeType) * 4));
                    string text = ReadPoolString(data, poolStart, ReadInt(data, offset + 8));
                    if (name == "x")
                    {
                        // Accept the earlier 64px test layout when updating it to 32px.
                        if (targetAttribute >= 0 ||
                            (text != OriginalX && text != PreviousTestX && text != TargetX))
                        {
                            throw new InvalidDataException("Unexpected or duplicate dialogue X coordinate.");
                        }
                        targetAttribute = offset + 8;
                    }
                    if (name == "y") y = text;
                    if (name == "size") size = text;
                    attribute = ReadInt(data, offset);
                }
                if (y != "598" || size != "32")
                {
                    throw new InvalidDataException("Unexpected dialogue Y coordinate or font size.");
                }
            }

            if (targetAttribute < 0)
            {
                throw new InvalidDataException("Dialogue message X coordinate was not found.");
            }
            if (ReadPoolString(data, poolStart, ReadInt(data, targetAttribute)) == TargetX)
            {
                return false;
            }

            // Coordinates share pool strings. Redirect only this attribute to the
            // existing target string so other nodes and the BNA layout stay unchanged.
            byte[] target = Encoding.ASCII.GetBytes(TargetX + "\0");
            for (int offset = poolStart; offset <= data.Length - target.Length; offset++)
            {
                if (offset > poolStart && data[offset - 1] != 0) continue;
                bool match = true;
                for (int index = 0; index < target.Length; index++)
                {
                    if (data[offset + index] != target[index]) { match = false; break; }
                }
                if (!match) continue;
                int relative = offset - poolStart;
                data[targetAttribute] = (byte)(relative >> 24);
                data[targetAttribute + 1] = (byte)(relative >> 16);
                data[targetAttribute + 2] = (byte)(relative >> 8);
                data[targetAttribute + 3] = (byte)relative;
                return true;
            }
            throw new InvalidDataException("Dialogue target X coordinate is missing from the string pool.");
        }

        private static int ReadInt(byte[] data, int offset)
        {
            if (offset < 0 || offset > data.Length - 4)
            {
                throw new InvalidDataException("Dialogue layout read is out of bounds.");
            }
            return (data[offset] << 24) | (data[offset + 1] << 16)
                | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static string ReadPoolString(byte[] data, int poolStart, int relative)
        {
            if (relative < 0 || relative >= data.Length - poolStart)
            {
                throw new InvalidDataException("Dialogue string offset is out of bounds.");
            }
            int start = poolStart + relative;
            int end = Array.IndexOf(data, (byte)0, start);
            if (end < 0) throw new InvalidDataException("Unterminated dialogue layout string.");
            return Encoding.UTF8.GetString(data, start, end - start);
        }
    }
}
