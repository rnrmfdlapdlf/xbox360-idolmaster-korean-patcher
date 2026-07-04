using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class CreditLinePatcher
    {
        private const string TargetBnaRelativePath = "root/initialFix/initialFix.bna";
        private const string TargetScbEntryPath = "root/initialFix/message_list/f172_list_msg_etc.scb";
        private const int TargetMsgIndex = 377;
        private const int ScbSectionTable = 0x70;
        private const int ScbSectionCount = 7;
        private const int MsgCountOffset = 0x20;
        private const int MsgTableOffset = 0x30;
        private const string TargetText = "\uD55C\uAE00 \uD328\uCE58 by Gideon";

        public static CreditLinePatchResult PatchExtractedRoot(string extractedRoot, HangulRemapper remapper, Action<int, string> progress)
        {
            CreditLinePatchResult result = new CreditLinePatchResult();
            string bnaPath = Path.Combine(extractedRoot, TargetBnaRelativePath.Replace('/', Path.DirectorySeparatorChar));

            Report(progress, 65, "\ud06c\ub808\ub527 \ubb38\uad6c \ubc18\uc601 \uc911...");
            if (!File.Exists(bnaPath))
            {
                return result;
            }

            result.TargetBnaFound = true;
            try
            {
                byte[] original = File.ReadAllBytes(bnaPath);
                if (!BnaContainer.IsBna(original))
                {
                    result.Errors++;
                    return result;
                }

                BnaContainer bna = BnaContainer.Parse(original);
                BnaContainerEntry entry = FindEntry(bna.Entries, TargetScbEntryPath);
                if (entry == null)
                {
                    return result;
                }

                result.TargetScbFound = true;
                string replacement = remapper.Apply(TargetText);
                ScbMsgPatch patch = PatchScbMessage(entry.Data, replacement);
                result.TargetMsgFound = patch.TargetMsgFound;
                result.AlreadyPatched = patch.AlreadyPatched;

                if (patch.Changed)
                {
                    entry.Data = patch.Data;
                    File.WriteAllBytes(bnaPath, bna.Rebuild());
                    result.Changed = true;
                    result.StringsPatched = 1;
                }
            }
            catch
            {
                result.Errors++;
            }

            if (progress != null)
            {
                if (result.StringsPatched > 0)
                {
                    progress(66, "\ud06c\ub808\ub527 \ubb38\uad6c \ubc18\uc601 \uc644\ub8cc");
                }
                else if (result.AlreadyPatched)
                {
                    progress(66, "\ud06c\ub808\ub527 \ubb38\uad6c \uc774\ubbf8 \uc801\uc6a9\ub428");
                }
            }

            return result;
        }

        private static ScbMsgPatch PatchScbMessage(byte[] scbData, string replacement)
        {
            ScbMsgPatch result = new ScbMsgPatch();
            result.Data = scbData;

            if (scbData.Length < ScbSectionTable + ScbSectionCount * 16 || !StartsWithAscii(scbData, "SCB"))
            {
                return result;
            }

            List<ScbSection> sections = ParseScb(scbData);
            for (int index = 0; index < sections.Count; index++)
            {
                ScbSection section = sections[index];
                if (section.Label != "MSG")
                {
                    continue;
                }

                MsgPatch msgPatch = PatchMsg(section.Data, replacement);
                result.TargetMsgFound = msgPatch.TargetMsgFound;
                result.AlreadyPatched = msgPatch.AlreadyPatched;
                if (msgPatch.Changed)
                {
                    section.Data = msgPatch.Data;
                    sections[index] = section;
                    result.Data = RebuildScb(scbData, sections);
                    result.Changed = true;
                }

                return result;
            }

            return result;
        }

        private static MsgPatch PatchMsg(byte[] msgData, string replacement)
        {
            MsgPatch result = new MsgPatch();
            result.Data = msgData;

            if (msgData.Length < MsgTableOffset || !StartsWithAscii(msgData, "MSG"))
            {
                return result;
            }

            int count = ReadU16(msgData, MsgCountOffset);
            if (TargetMsgIndex < 0 || TargetMsgIndex >= count)
            {
                return result;
            }

            int tableEnd = MsgTableOffset + count * 8;
            int zeroPoint = Align(tableEnd, 0x10);
            if (zeroPoint > msgData.Length)
            {
                return result;
            }

            List<string> texts = new List<string>();
            for (int index = 0; index < count; index++)
            {
                int tableOffset = MsgTableOffset + index * 8;
                int size = (int)ReadU32(msgData, tableOffset);
                int textOffset = (int)ReadU32(msgData, tableOffset + 4);
                int start = zeroPoint + textOffset;
                int end = start + size;
                if (start < 0 || end > msgData.Length || size < 2)
                {
                    return result;
                }

                texts.Add(Encoding.BigEndianUnicode.GetString(msgData, start, size - 2));
            }

            result.TargetMsgFound = true;
            if (String.Equals(texts[TargetMsgIndex], replacement, StringComparison.Ordinal))
            {
                result.AlreadyPatched = true;
                return result;
            }

            texts[TargetMsgIndex] = replacement;
            result.Data = BuildMsg(msgData, texts);
            result.Changed = true;
            return result;
        }

        private static List<ScbSection> ParseScb(byte[] data)
        {
            List<ScbSection> sections = new List<ScbSection>();
            for (int index = 0; index < ScbSectionCount; index++)
            {
                int tableOffset = ScbSectionTable + index * 16;
                byte[] labelRaw = new byte[4];
                Buffer.BlockCopy(data, tableOffset, labelRaw, 0, 4);
                uint size = ReadU32(data, tableOffset + 4);
                uint offset = ReadU32(data, tableOffset + 8);
                byte[] pad = new byte[4];
                Buffer.BlockCopy(data, tableOffset + 12, pad, 0, 4);
                if (offset + size > data.Length)
                {
                    throw new InvalidDataException("SCB section extends past file end.");
                }

                byte[] sectionData = new byte[size];
                Buffer.BlockCopy(data, (int)offset, sectionData, 0, (int)size);

                sections.Add(new ScbSection
                {
                    Index = index,
                    LabelRaw = labelRaw,
                    Label = DecodeSectionLabel(labelRaw),
                    Offset = (int)offset,
                    Pad = pad,
                    Data = sectionData
                });
            }

            return sections;
        }

        private static byte[] RebuildScb(byte[] original, List<ScbSection> sections)
        {
            List<ScbSection> ordered = new List<ScbSection>(sections);
            ordered.Sort(delegate(ScbSection left, ScbSection right)
            {
                return left.Offset.CompareTo(right.Offset);
            });

            int firstOffset = ordered[0].Offset;
            MemoryStream stream = new MemoryStream();
            stream.Write(original, 0, firstOffset);

            bool postMsg = false;
            Dictionary<int, int> newOffsets = new Dictionary<int, int>();
            Dictionary<int, int> newSizes = new Dictionary<int, int>();
            for (int index = 0; index < ordered.Count; index++)
            {
                ScbSection section = ordered[index];
                newOffsets[section.Index] = (int)stream.Position;
                newSizes[section.Index] = section.Data.Length;
                stream.Write(section.Data, 0, section.Data.Length);
                PadStream(stream, 0x10, postMsg ? (byte)0xCC : (byte)0xCD);
                if (section.Label == "MSG")
                {
                    postMsg = true;
                }
            }

            PadStream(stream, 0x10, 0xCC);
            byte[] output = stream.ToArray();
            WriteU32(output, 0x10, (uint)(output.Length - 0x20));

            for (int index = 0; index < sections.Count; index++)
            {
                ScbSection section = sections[index];
                int tableOffset = ScbSectionTable + section.Index * 16;
                Buffer.BlockCopy(section.LabelRaw, 0, output, tableOffset, 4);
                WriteU32(output, tableOffset + 4, (uint)newSizes[section.Index]);
                WriteU32(output, tableOffset + 8, (uint)newOffsets[section.Index]);
                Buffer.BlockCopy(section.Pad, 0, output, tableOffset + 12, 4);
            }

            return output;
        }

        private static byte[] BuildMsg(byte[] original, List<string> texts)
        {
            MemoryStream stream = new MemoryStream();
            int prefixLength = Math.Min(MsgTableOffset, original.Length);
            stream.Write(original, 0, prefixLength);
            while (stream.Length < MsgTableOffset)
            {
                stream.WriteByte(0);
            }

            WriteU16ToStreamBuffer(stream, MsgCountOffset, (ushort)texts.Count);

            List<byte[]> payloads = new List<byte[]>();
            int stringDataSize = 0;
            for (int index = 0; index < texts.Count; index++)
            {
                byte[] encoded = Encoding.BigEndianUnicode.GetBytes(texts[index]);
                byte[] payload = new byte[encoded.Length + 2];
                Buffer.BlockCopy(encoded, 0, payload, 0, encoded.Length);
                payloads.Add(payload);
                stringDataSize += payload.Length;
            }

            int headerSize = 16 + texts.Count * 8 + ((texts.Count % 2) == 1 ? 8 : 0);
            WriteU16ToStreamBuffer(stream, 0x26, (ushort)(stringDataSize & 0xFFFF));
            WriteU16ToStreamBuffer(stream, 0x2A, (ushort)headerSize);

            int textOffset = 0;
            for (int index = 0; index < payloads.Count; index++)
            {
                WriteU32(stream, (uint)payloads[index].Length);
                WriteU32(stream, (uint)textOffset);
                textOffset += payloads[index].Length;
            }

            PadStream(stream, 0x10, 0xCD);
            for (int index = 0; index < payloads.Count; index++)
            {
                stream.Write(payloads[index], 0, payloads[index].Length);
            }

            byte[] output = stream.ToArray();
            WriteU32(output, 0x10, (uint)(output.Length - 0x20));
            return output;
        }

        private static BnaContainerEntry FindEntry(List<BnaContainerEntry> entries, string entryPath)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (String.Equals(entries[index].Path, entryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[index];
                }
            }

            return null;
        }

        private static bool StartsWithAscii(byte[] data, string value)
        {
            if (data.Length < value.Length)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (data[index] != (byte)value[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string DecodeSectionLabel(byte[] bytes)
        {
            int length = 0;
            while (length < bytes.Length && bytes[length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(bytes, 0, length);
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static int ReadU16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
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

        private static void WriteU16ToStreamBuffer(MemoryStream stream, int offset, ushort value)
        {
            long oldPosition = stream.Position;
            stream.Position = offset;
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
            stream.Position = oldPosition;
        }

        private static void PadStream(Stream stream, int boundary, byte value)
        {
            while ((stream.Position % boundary) != 0)
            {
                stream.WriteByte(value);
            }
        }

        private static int Align(int value, int boundary)
        {
            int over = value % boundary;
            return over == 0 ? value : value + boundary - over;
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }

        private struct ScbSection
        {
            public int Index;
            public byte[] LabelRaw;
            public string Label;
            public int Offset;
            public byte[] Pad;
            public byte[] Data;
        }

        private sealed class ScbMsgPatch
        {
            public bool Changed;
            public bool TargetMsgFound;
            public bool AlreadyPatched;
            public byte[] Data;
        }

        private sealed class MsgPatch
        {
            public bool Changed;
            public bool TargetMsgFound;
            public bool AlreadyPatched;
            public byte[] Data;
        }
    }
}
