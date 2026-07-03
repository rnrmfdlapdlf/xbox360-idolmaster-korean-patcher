using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ImasKoreanPatcher
{
    internal sealed class AuditionFanPatcher
    {
        private const int HeaderSize = 28;
        private const int NodeRecordSize = 28;
        private const int AttrRecordSize = 12;
        private const int ExpectedProgramCount = 59;
        private const string TargetBxrPath = "root/scene/produce/audition/system/auditionlist.bxr";

        private static readonly Regex ProgramIdPattern = new Regex("^[a-f][0-9]+(?:_[0-9]+)?$", RegexOptions.Compiled);

        private static readonly ProgramFanValue[] ExpectedFanValues = new ProgramFanValue[]
        {
            new ProgramFanValue("a1", 1000),
            new ProgramFanValue("a2", 5000),
            new ProgramFanValue("a3", 6000),
            new ProgramFanValue("a4", 7000),
            new ProgramFanValue("a5", 4000),
            new ProgramFanValue("b1", 10000),
            new ProgramFanValue("b2", 30000),
            new ProgramFanValue("b3", 20000),
            new ProgramFanValue("b4_1", 7500),
            new ProgramFanValue("b4_2", 12500),
            new ProgramFanValue("b4_3", 25000),
            new ProgramFanValue("b4_4", 40000),
            new ProgramFanValue("c1", 20000),
            new ProgramFanValue("c2", 25000),
            new ProgramFanValue("c3", 30000),
            new ProgramFanValue("c4", 50000),
            new ProgramFanValue("c5_1", 25000),
            new ProgramFanValue("c5_2", 30000),
            new ProgramFanValue("c5_3", 50000),
            new ProgramFanValue("c5_4", 70000),
            new ProgramFanValue("d1", 50000),
            new ProgramFanValue("d2", 50000),
            new ProgramFanValue("d3", 50000),
            new ProgramFanValue("d4", 50000),
            new ProgramFanValue("d5", 100000),
            new ProgramFanValue("d6", 30000),
            new ProgramFanValue("d7", 50000),
            new ProgramFanValue("d8", 50000),
            new ProgramFanValue("d9_1", 50000),
            new ProgramFanValue("d9_2", 50000),
            new ProgramFanValue("d9_3", 50000),
            new ProgramFanValue("d9_4", 50000),
            new ProgramFanValue("d9_5", 50000),
            new ProgramFanValue("e1", 120000),
            new ProgramFanValue("e2", 70000),
            new ProgramFanValue("e3", 80000),
            new ProgramFanValue("e4", 75000),
            new ProgramFanValue("e5", 90000),
            new ProgramFanValue("e6", 20000),
            new ProgramFanValue("e7", 20000),
            new ProgramFanValue("e8", 20000),
            new ProgramFanValue("f1", 50000),
            new ProgramFanValue("f2", 50000),
            new ProgramFanValue("f3", 30000),
            new ProgramFanValue("f4", 25000),
            new ProgramFanValue("f5", 60000),
            new ProgramFanValue("f6", 55000),
            new ProgramFanValue("f7", 50000),
            new ProgramFanValue("f8", 55000),
            new ProgramFanValue("f9_1", 60000),
            new ProgramFanValue("f9_2", 60000),
            new ProgramFanValue("f9_3", 60000),
            new ProgramFanValue("f9_4", 60000),
            new ProgramFanValue("f9_5", 60000),
            new ProgramFanValue("f9_6", 60000),
            new ProgramFanValue("f9_7", 60000),
            new ProgramFanValue("f9_8", 60000),
            new ProgramFanValue("f9_9", 60000),
            new ProgramFanValue("f9_10", 60000),
        };

        public AuditionFanPatchResult PatchExtractedRoot(
            string extractedRoot,
            bool doubleFanValues,
            bool ensureAtLeastTwoPasses,
            Action<int, string> progress)
        {
            AuditionFanPatchResult result = new AuditionFanPatchResult();
            if (!doubleFanValues && !ensureAtLeastTwoPasses)
            {
                return result;
            }

            if (progress != null)
            {
                progress(73, "\uc624\ub514\uc158 \uce58\ud2b8 \uc635\uc158 \uc801\uc6a9 \uc911...");
            }

            PatchStandaloneBxr(extractedRoot, doubleFanValues, ensureAtLeastTwoPasses, result);
            PatchEntryBna(extractedRoot, doubleFanValues, ensureAtLeastTwoPasses, result);

            if (result.Errors > 0 || result.VerificationErrors > 0)
            {
                throw new InvalidOperationException("\uc624\ub514\uc158 \uce58\ud2b8 \ud328\uce58 \uc911 \uc624\ub958\uac00 \ubc1c\uc0dd\ud588\uc2b5\ub2c8\ub2e4.");
            }

            if (progress != null)
            {
                progress(
                    74,
                    String.Format(
                        "\uc624\ub514\uc158 \uce58\ud2b8 \ud328\uce58 \uc644\ub8cc: \ud32c {0:N0}\uac1c, \ud569\uaca9\uc790\uc218 {1:N0}\uac1c",
                        result.FanValuesPatched,
                        result.PassValuesPatched));
            }

            return result;
        }

        private static void PatchStandaloneBxr(
            string extractedRoot,
            bool doubleFanValues,
            bool ensureAtLeastTwoPasses,
            AuditionFanPatchResult result)
        {
            string bxrPath = Path.Combine(
                extractedRoot,
                Path.Combine("root", Path.Combine("scene", Path.Combine("produce", Path.Combine("audition", Path.Combine("system", "auditionlist.bxr"))))));

            if (!File.Exists(bxrPath))
            {
                return;
            }

            result.BxrFilesScanned++;
            try
            {
                byte[] original = File.ReadAllBytes(bxrPath);
                BxrFanPatch patch = PatchBxr(original, doubleFanValues, ensureAtLeastTwoPasses);
                if (patch.TargetSeen)
                {
                    result.TargetBxrFilesSeen++;
                    result.FanValuesPatched += patch.FanValuesPatched;
                    result.FanValuesAlreadyPatched += patch.FanValuesAlreadyPatched;
                    result.PassValuesPatched += patch.PassValuesPatched;
                    result.PassValuesAlreadyAtLeastTwo += patch.PassValuesAlreadyAtLeastTwo;
                    result.UpdatedReferenceFields += patch.UpdatedReferenceFields;
                    result.VerificationErrors += patch.VerificationErrors;
                }

                if (patch.Changed)
                {
                    File.WriteAllBytes(bxrPath, patch.Data);
                    result.BxrFilesPatched++;
                }
            }
            catch
            {
                result.Errors++;
            }
        }

        private static void PatchEntryBna(
            string extractedRoot,
            bool doubleFanValues,
            bool ensureAtLeastTwoPasses,
            AuditionFanPatchResult result)
        {
            string bnaPath = Path.Combine(
                extractedRoot,
                Path.Combine("root", Path.Combine("scene", Path.Combine("produce", Path.Combine("audition", "entry.bna")))));

            if (!File.Exists(bnaPath))
            {
                return;
            }

            result.BnaFilesScanned++;
            try
            {
                PatchBnaFile(bnaPath, doubleFanValues, ensureAtLeastTwoPasses, result);
            }
            catch
            {
                result.Errors++;
            }
        }

        private static void PatchBnaFile(
            string path,
            bool doubleFanValues,
            bool ensureAtLeastTwoPasses,
            AuditionFanPatchResult result)
        {
            byte[] original = File.ReadAllBytes(path);
            if (!BnaContainer.IsBna(original))
            {
                return;
            }

            BnaContainer bna = BnaContainer.Parse(original);
            bool changed = false;
            List<BnaContainerEntry> entries = bna.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                BnaContainerEntry entry = entries[index];
                if (!String.Equals(entry.Path, TargetBxrPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.BxrFilesScanned++;
                BxrFanPatch patch = PatchBxr(entry.Data, doubleFanValues, ensureAtLeastTwoPasses);
                if (patch.TargetSeen)
                {
                    result.TargetBxrFilesSeen++;
                    result.FanValuesPatched += patch.FanValuesPatched;
                    result.FanValuesAlreadyPatched += patch.FanValuesAlreadyPatched;
                    result.PassValuesPatched += patch.PassValuesPatched;
                    result.PassValuesAlreadyAtLeastTwo += patch.PassValuesAlreadyAtLeastTwo;
                    result.UpdatedReferenceFields += patch.UpdatedReferenceFields;
                    result.VerificationErrors += patch.VerificationErrors;
                }

                if (patch.Changed)
                {
                    entry.Data = patch.Data;
                    changed = true;
                    result.BxrFilesPatched++;
                }
            }

            if (changed)
            {
                File.WriteAllBytes(path, bna.Rebuild());
                result.BnaFilesPatched++;
            }
        }

        private static BxrFanPatch PatchBxr(byte[] data, bool doubleFanValues, bool ensureAtLeastTwoPasses)
        {
            BxrLayout layout;
            if (!TryGetLayout(data, out layout))
            {
                return new BxrFanPatch(false, false, data);
            }

            Dictionary<string, int> expectedFans = BuildExpectedFanMap();
            List<ProgramRow> rows = FindProgramRows(data, layout);
            int matchedRows = 0;
            int fanValuesAlreadyPatched = 0;
            int fanValuesPatched = 0;
            int passValuesAlreadyAtLeastTwo = 0;
            int passValuesPatched = 0;
            List<Replacement> replacements = new List<Replacement>();

            for (int index = 0; index < rows.Count; index++)
            {
                ProgramRow row = rows[index];
                int expectedFan;
                if (!expectedFans.TryGetValue(row.ProgramId, out expectedFan))
                {
                    continue;
                }

                matchedRows++;
                if (doubleFanValues)
                {
                    int doubledFan = checked(expectedFan * 2);
                    if (row.FanValue == doubledFan)
                    {
                        fanValuesAlreadyPatched++;
                    }
                    else
                    {
                        if (row.FanValue != expectedFan)
                        {
                            throw new InvalidDataException(
                                String.Format(
                                    "Unexpected fan value for {0}: {1}, expected {2}.",
                                    row.ProgramId,
                                    row.FanValue,
                                    expectedFan));
                        }

                        byte[] payload = Encoding.ASCII.GetBytes(doubledFan.ToString());
                        replacements.Add(new Replacement(row.FanRelOffset, row.FanByteLength, payload));
                        fanValuesPatched++;
                    }
                }

                if (ensureAtLeastTwoPasses)
                {
                    if (row.PassValue == 1)
                    {
                        replacements.Add(new Replacement(row.PassRelOffset, row.PassByteLength, Encoding.ASCII.GetBytes("2")));
                        passValuesPatched++;
                    }
                    else
                    {
                        passValuesAlreadyAtLeastTwo++;
                    }
                }
            }

            if (matchedRows != ExpectedProgramCount)
            {
                throw new InvalidDataException(
                    String.Format(
                        "Unexpected audition program count: {0}, expected {1}.",
                        matchedRows,
                        ExpectedProgramCount));
            }

            if (replacements.Count == 0)
            {
                return new BxrFanPatch(
                    true,
                    false,
                    data,
                    0,
                    fanValuesAlreadyPatched,
                    0,
                    passValuesAlreadyAtLeastTwo,
                    0,
                    0);
            }

            replacements.Sort(delegate(Replacement left, Replacement right)
            {
                return left.RelOffset.CompareTo(right.RelOffset);
            });

            BxrFanPatch rebuilt = RebuildBxr(data, layout, replacements);
            rebuilt.TargetSeen = true;
            rebuilt.FanValuesPatched = fanValuesPatched;
            rebuilt.FanValuesAlreadyPatched = fanValuesAlreadyPatched;
            rebuilt.PassValuesPatched = passValuesPatched;
            rebuilt.PassValuesAlreadyAtLeastTwo = passValuesAlreadyAtLeastTwo;
            return rebuilt;
        }

        private static List<ProgramRow> FindProgramRows(byte[] data, BxrLayout layout)
        {
            List<PoolString> strings = ReadPoolStrings(data, layout.PoolBase, layout.PoolSize);
            List<ProgramRow> rows = new List<ProgramRow>();
            for (int index = 0; index + 4 < strings.Count; index++)
            {
                PoolString id = strings[index];
                if (!id.IsAscii || !ProgramIdPattern.IsMatch(id.Text))
                {
                    continue;
                }

                PoolString place = strings[index + 2];
                PoolString pass = strings[index + 3];
                PoolString fan = strings[index + 4];
                int passValue;
                int fanValue;
                if (!place.IsAscii || place.Text.Length != 2 || !IsAsciiDigitString(pass.Text) || !Int32.TryParse(pass.Text, out passValue))
                {
                    continue;
                }

                if (passValue < 1 || passValue > 4 || !fan.IsAscii || !IsAsciiDigitString(fan.Text) || !Int32.TryParse(fan.Text, out fanValue))
                {
                    continue;
                }

                if (fanValue < 1000)
                {
                    continue;
                }

                rows.Add(
                    new ProgramRow(
                        id.Text,
                        pass.RelOffset,
                        pass.ByteLength,
                        passValue,
                        fan.RelOffset,
                        fan.ByteLength,
                        fanValue));
            }

            return rows;
        }

        private static List<PoolString> ReadPoolStrings(byte[] data, int poolBase, int poolSize)
        {
            List<PoolString> result = new List<PoolString>();
            int rel = 0;
            while (rel < poolSize)
            {
                int absolute = poolBase + rel;
                if (data[absolute] == 0)
                {
                    rel++;
                    continue;
                }

                PoolString item;
                if (TryReadAsciiString(data, poolBase, poolSize, rel, out item))
                {
                    result.Add(item);
                    rel = item.NextRelOffset;
                    continue;
                }

                if (TryReadUtf16String(data, poolBase, poolSize, rel, out item))
                {
                    result.Add(item);
                    rel = item.NextRelOffset;
                    continue;
                }

                rel++;
            }

            return result;
        }

        private static bool TryReadAsciiString(byte[] data, int poolBase, int poolSize, int rel, out PoolString item)
        {
            item = null;
            int absolute = poolBase + rel;
            if (data[absolute] < 32 || data[absolute] >= 127)
            {
                return false;
            }

            int end = absolute;
            int poolEnd = poolBase + poolSize;
            while (end < poolEnd && data[end] != 0)
            {
                if (data[end] < 32 || data[end] >= 127)
                {
                    return false;
                }

                end++;
            }

            if (end == absolute || end >= poolEnd)
            {
                return false;
            }

            string text = Encoding.ASCII.GetString(data, absolute, end - absolute);
            item = new PoolString(rel, end - absolute, end + 1 - poolBase, text, true);
            return true;
        }

        private static bool TryReadUtf16String(byte[] data, int poolBase, int poolSize, int rel, out PoolString item)
        {
            item = null;
            int absolute = poolBase + rel;
            int poolEnd = poolBase + poolSize;
            int end = absolute;
            while (end + 1 < poolEnd)
            {
                if (data[end] == 0 && data[end + 1] == 0)
                {
                    break;
                }

                end += 2;
            }

            if (end == absolute || end + 1 >= poolEnd)
            {
                return false;
            }

            string text = Encoding.BigEndianUnicode.GetString(data, absolute, end - absolute);
            item = new PoolString(rel, end - absolute, end + 2 - poolBase, text, false);
            return true;
        }

        private static BxrFanPatch RebuildBxr(byte[] data, BxrLayout layout, List<Replacement> replacements)
        {
            byte[] prefix = new byte[layout.PoolBase];
            Buffer.BlockCopy(data, 0, prefix, 0, layout.PoolBase);

            byte[] pool = new byte[layout.PoolSize];
            Buffer.BlockCopy(data, layout.PoolBase, pool, 0, layout.PoolSize);

            MemoryStream rebuiltPool = new MemoryStream();
            int cursor = 0;
            for (int index = 0; index < replacements.Count; index++)
            {
                Replacement replacement = replacements[index];
                if (replacement.RelOffset > cursor)
                {
                    rebuiltPool.Write(pool, cursor, replacement.RelOffset - cursor);
                }

                rebuiltPool.Write(replacement.Payload, 0, replacement.Payload.Length);
                cursor = replacement.RelOffset + replacement.OldByteLength;
            }

            if (cursor < pool.Length)
            {
                rebuiltPool.Write(pool, cursor, pool.Length - cursor);
            }

            byte[] newPool = rebuiltPool.ToArray();
            ShiftTable shiftTable = new ShiftTable(replacements);
            int updatedRefs = UpdateReferenceFields(prefix, layout, shiftTable);
            WriteU32(prefix, 20, checked((uint)newPool.Length));

            byte[] output = new byte[prefix.Length + newPool.Length];
            Buffer.BlockCopy(prefix, 0, output, 0, prefix.Length);
            Buffer.BlockCopy(newPool, 0, output, prefix.Length, newPool.Length);

            int verificationErrors = CountVerificationErrors(output, layout.PoolBase, replacements, shiftTable);
            return new BxrFanPatch(true, true, output, 0, 0, 0, 0, updatedRefs, verificationErrors);
        }

        private static int UpdateReferenceFields(byte[] prefix, BxrLayout layout, ShiftTable shiftTable)
        {
            int updated = 0;

            for (int offset = HeaderSize; offset < layout.NodeStart; offset += 4)
            {
                updated += UpdateReferenceField(prefix, offset, layout.PoolSize, shiftTable);
            }

            for (int index = 0; index < layout.NodeCount; index++)
            {
                int nodeOffset = layout.NodeStart + index * NodeRecordSize;
                updated += UpdateReferenceField(prefix, nodeOffset + 12, layout.PoolSize, shiftTable);
                updated += UpdateReferenceField(prefix, nodeOffset + 20, layout.PoolSize, shiftTable);
            }

            for (int index = 0; index < layout.AttrCount; index++)
            {
                int attrOffset = layout.AttrStart + index * AttrRecordSize;
                updated += UpdateReferenceField(prefix, attrOffset + 8, layout.PoolSize, shiftTable);
            }

            return updated;
        }

        private static int UpdateReferenceField(byte[] data, int offset, int oldPoolSize, ShiftTable shiftTable)
        {
            uint rawValue = ReadU32(data, offset);
            if (rawValue == UInt32.MaxValue || rawValue > Int32.MaxValue)
            {
                return 0;
            }

            int oldRel = (int)rawValue;
            if (oldRel < 0 || oldRel >= oldPoolSize)
            {
                return 0;
            }

            int newRel = oldRel + shiftTable.GetShift(oldRel);
            if (newRel == oldRel)
            {
                return 0;
            }

            WriteU32(data, offset, checked((uint)newRel));
            return 1;
        }

        private static int CountVerificationErrors(byte[] data, int poolBase, List<Replacement> replacements, ShiftTable shiftTable)
        {
            int errors = 0;
            for (int index = 0; index < replacements.Count; index++)
            {
                Replacement replacement = replacements[index];
                int newRel = replacement.RelOffset + shiftTable.GetShift(replacement.RelOffset);
                int offset = poolBase + newRel;
                if (offset < poolBase || offset + replacement.Payload.Length > data.Length)
                {
                    errors++;
                    continue;
                }

                for (int byteIndex = 0; byteIndex < replacement.Payload.Length; byteIndex++)
                {
                    if (data[offset + byteIndex] != replacement.Payload[byteIndex])
                    {
                        errors++;
                        break;
                    }
                }
            }

            return errors;
        }

        private static bool TryGetLayout(byte[] data, out BxrLayout layout)
        {
            layout = null;
            if (!IsBxr(data) || data.Length < HeaderSize)
            {
                return false;
            }

            uint nodeCountRaw = ReadU32(data, 12);
            uint attrCountRaw = ReadU32(data, 16);
            uint poolSizeRaw = ReadU32(data, 20);
            if (nodeCountRaw > Int32.MaxValue || attrCountRaw > Int32.MaxValue || poolSizeRaw > Int32.MaxValue)
            {
                return false;
            }

            int nodeCount = (int)nodeCountRaw;
            int attrCount = (int)attrCountRaw;
            int poolSize = (int)poolSizeRaw;
            int poolBase = data.Length - poolSize;
            int attrStart = checked(poolBase - attrCount * AttrRecordSize);
            int nodeStart = checked(attrStart - nodeCount * NodeRecordSize);
            if (poolSize <= 0 || nodeStart < HeaderSize || attrStart < nodeStart || poolBase < attrStart || poolBase > data.Length)
            {
                return false;
            }

            layout = new BxrLayout(nodeStart, nodeCount, attrStart, attrCount, poolBase, poolSize);
            return true;
        }

        private static bool IsBxr(byte[] data)
        {
            return data.Length >= 4
                && data[0] == (byte)'B'
                && data[1] == (byte)'X'
                && data[2] == (byte)'R'
                && data[3] == (byte)'0';
        }

        private static bool IsAsciiDigitString(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char ch = value[index];
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, int> BuildExpectedFanMap()
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < ExpectedFanValues.Length; index++)
            {
                ProgramFanValue item = ExpectedFanValues[index];
                result[item.ProgramId] = item.FanValue;
            }

            return result;
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

        private sealed class ProgramFanValue
        {
            public readonly string ProgramId;
            public readonly int FanValue;

            public ProgramFanValue(string programId, int fanValue)
            {
                ProgramId = programId;
                FanValue = fanValue;
            }
        }

        private sealed class ProgramRow
        {
            public readonly string ProgramId;
            public readonly int PassRelOffset;
            public readonly int PassByteLength;
            public readonly int PassValue;
            public readonly int FanRelOffset;
            public readonly int FanByteLength;
            public readonly int FanValue;

            public ProgramRow(
                string programId,
                int passRelOffset,
                int passByteLength,
                int passValue,
                int fanRelOffset,
                int fanByteLength,
                int fanValue)
            {
                ProgramId = programId;
                PassRelOffset = passRelOffset;
                PassByteLength = passByteLength;
                PassValue = passValue;
                FanRelOffset = fanRelOffset;
                FanByteLength = fanByteLength;
                FanValue = fanValue;
            }
        }

        private sealed class PoolString
        {
            public readonly int RelOffset;
            public readonly int ByteLength;
            public readonly int NextRelOffset;
            public readonly string Text;
            public readonly bool IsAscii;

            public PoolString(int relOffset, int byteLength, int nextRelOffset, string text, bool isAscii)
            {
                RelOffset = relOffset;
                ByteLength = byteLength;
                NextRelOffset = nextRelOffset;
                Text = text;
                IsAscii = isAscii;
            }
        }

        private sealed class BxrLayout
        {
            public readonly int NodeStart;
            public readonly int NodeCount;
            public readonly int AttrStart;
            public readonly int AttrCount;
            public readonly int PoolBase;
            public readonly int PoolSize;

            public BxrLayout(int nodeStart, int nodeCount, int attrStart, int attrCount, int poolBase, int poolSize)
            {
                NodeStart = nodeStart;
                NodeCount = nodeCount;
                AttrStart = attrStart;
                AttrCount = attrCount;
                PoolBase = poolBase;
                PoolSize = poolSize;
            }
        }

        private sealed class BxrFanPatch
        {
            public bool TargetSeen;
            public readonly bool Changed;
            public readonly byte[] Data;
            public int FanValuesPatched;
            public int FanValuesAlreadyPatched;
            public int PassValuesPatched;
            public int PassValuesAlreadyAtLeastTwo;
            public readonly int UpdatedReferenceFields;
            public readonly int VerificationErrors;

            public BxrFanPatch(bool targetSeen, bool changed, byte[] data)
                : this(targetSeen, changed, data, 0, 0, 0, 0, 0, 0)
            {
            }

            public BxrFanPatch(
                bool targetSeen,
                bool changed,
                byte[] data,
                int fanValuesPatched,
                int fanValuesAlreadyPatched,
                int passValuesPatched,
                int passValuesAlreadyAtLeastTwo,
                int updatedReferenceFields,
                int verificationErrors)
            {
                TargetSeen = targetSeen;
                Changed = changed;
                Data = data;
                FanValuesPatched = fanValuesPatched;
                FanValuesAlreadyPatched = fanValuesAlreadyPatched;
                PassValuesPatched = passValuesPatched;
                PassValuesAlreadyAtLeastTwo = passValuesAlreadyAtLeastTwo;
                UpdatedReferenceFields = updatedReferenceFields;
                VerificationErrors = verificationErrors;
            }
        }

        private sealed class Replacement
        {
            public readonly int RelOffset;
            public readonly int OldByteLength;
            public readonly byte[] Payload;

            public Replacement(int relOffset, int oldByteLength, byte[] payload)
            {
                RelOffset = relOffset;
                OldByteLength = oldByteLength;
                Payload = payload;
            }

            public int Delta
            {
                get { return Payload.Length - OldByteLength; }
            }
        }

        private sealed class ShiftTable
        {
            private readonly int[] relOffsets;
            private readonly int[] cumulativeShiftAfter;

            public ShiftTable(List<Replacement> replacements)
            {
                relOffsets = new int[replacements.Count];
                cumulativeShiftAfter = new int[replacements.Count];

                int shift = 0;
                for (int index = 0; index < replacements.Count; index++)
                {
                    relOffsets[index] = replacements[index].RelOffset;
                    shift += replacements[index].Delta;
                    cumulativeShiftAfter[index] = shift;
                }
            }

            public int GetShift(int oldRel)
            {
                int low = 0;
                int high = relOffsets.Length - 1;
                int found = -1;

                while (low <= high)
                {
                    int mid = low + ((high - low) / 2);
                    if (relOffsets[mid] < oldRel)
                    {
                        found = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }

                return found < 0 ? 0 : cumulativeShiftAfter[found];
            }
        }
    }
}
