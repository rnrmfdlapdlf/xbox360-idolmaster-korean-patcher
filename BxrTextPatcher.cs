using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class BxrTextPatcher
    {
        private const int MaxStringBytes = 4096;

        private readonly Dictionary<string, BxrTextTranslation> translations;
        private readonly HangulRemapper remapper;

        public BxrTextPatcher(Dictionary<string, BxrTextTranslation> translations, HangulRemapper remapper)
        {
            this.translations = translations;
            this.remapper = remapper;
        }

        public BxrPatchResult PatchExtractedRoot(string extractedRoot, Action<int, string> progress)
        {
            BxrPatchResult result = new BxrPatchResult();
            result.TranslationRows = translations.Count;
            if (translations.Count == 0)
            {
                return result;
            }

            string[] bnaFiles = Directory.GetFiles(extractedRoot, "*.bna", SearchOption.AllDirectories);
            for (int index = 0; index < bnaFiles.Length; index++)
            {
                if (progress != null && (index == 0 || index % 25 == 0))
                {
                    int percent = 66 + (int)(6.0 * index / Math.Max(1, bnaFiles.Length));
                    progress(percent, String.Format("BXR \ud14d\uc2a4\ud2b8 \ud328\uce58 \uc911... {0:N0}/{1:N0}", index, bnaFiles.Length));
                }

                result.BnaFilesScanned++;
                try
                {
                    PatchBnaFile(bnaFiles[index], result);
                }
                catch
                {
                    result.Errors++;
                }
            }

            if (progress != null)
            {
                progress(72, String.Format("BXR \ud328\uce58 \uc644\ub8cc: {0:N0}\uac1c \ubb38\uc790\uc5f4", result.StringsPatched));
            }

            if (result.VerificationErrors > 0)
            {
                throw new InvalidOperationException("BXR string-pool verification failed.");
            }

            return result;
        }

        private void PatchBnaFile(string path, BxrPatchResult result)
        {
            byte[] original = File.ReadAllBytes(path);
            if (!BnaContainer.IsBna(original))
            {
                return;
            }

            BnaContainer bna = BnaContainer.Parse(original);
            bool isLessonBna = IsLessonBnaPath(path);
            bool changed = false;

            List<BnaContainerEntry> entries = bna.Entries;
            for (int index = 0; index < entries.Count; index++)
            {
                BnaContainerEntry entry = entries[index];
                if (!IsBxr(entry.Data))
                {
                    continue;
                }

                result.BxrFilesScanned++;
                BxrEntryPatch entryPatch = PatchBxr(entry.Data, isLessonBna, result);
                if (entryPatch.Changed)
                {
                    entry.Data = entryPatch.Data;
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

        private BxrEntryPatch PatchBxr(byte[] data, bool isLessonBna, BxrPatchResult result)
        {
            int poolBase;
            int oldPoolSize;
            if (!TryGetPool(data, out poolBase, out oldPoolSize))
            {
                return new BxrEntryPatch(false, data);
            }

            List<Replacement> replacements = new List<Replacement>();
            Dictionary<int, bool> seenRelOffsets = new Dictionary<int, bool>();
            List<int> relOffsets = new List<int>();

            for (int loc = 0; loc < poolBase - 3; loc += 4)
            {
                uint relValue = ReadU32(data, loc);
                if (relValue > Int32.MaxValue)
                {
                    continue;
                }

                int rel = (int)relValue;
                if (rel < 0 || rel >= oldPoolSize || seenRelOffsets.ContainsKey(rel))
                {
                    continue;
                }

                seenRelOffsets[rel] = true;
                relOffsets.Add(rel);
            }

            relOffsets.Sort();
            int previousEnd = -1;

            for (int relIndex = 0; relIndex < relOffsets.Count; relIndex++)
            {
                int rel = relOffsets[relIndex];
                if (!IsStringStartBoundary(data, poolBase, rel))
                {
                    continue;
                }

                CandidateString candidate;
                if (!TryReadString(data, poolBase + rel, out candidate))
                {
                    continue;
                }

                result.CandidateStringsScanned++;

                string textId = BxrTextTranslationStore.ComputeTextId(candidate.Text);
                BxrTextTranslation translation;
                if (!translations.TryGetValue(textId, out translation))
                {
                    continue;
                }

                string koText = SelectKoText(translation, isLessonBna);
                if (String.IsNullOrEmpty(koText))
                {
                    continue;
                }

                result.StringsMatched++;
                if (rel < previousEnd)
                {
                    continue;
                }

                string gameText;
                try
                {
                    gameText = remapper.Apply(koText);
                }
                catch
                {
                    result.MissingRemapErrors++;
                    continue;
                }

                byte[] newPayload = Encoding.BigEndianUnicode.GetBytes(gameText);
                replacements.Add(new Replacement(rel, candidate.ByteLength, newPayload));
                previousEnd = rel + candidate.ByteLength;
            }

            if (replacements.Count == 0)
            {
                return new BxrEntryPatch(false, data);
            }

            replacements.Sort(delegate(Replacement left, Replacement right)
            {
                return left.RelOffset.CompareTo(right.RelOffset);
            });

            byte[] rebuilt = RebuildBxr(data, poolBase, oldPoolSize, replacements, result);
            return new BxrEntryPatch(true, rebuilt);
        }

        private static string SelectKoText(BxrTextTranslation translation, bool isLessonBna)
        {
            if (!isLessonBna)
            {
                return translation.KoText;
            }

            if (!String.IsNullOrEmpty(translation.KoTextLesson))
            {
                return translation.KoTextLesson;
            }

            if (!String.IsNullOrEmpty(translation.KoText) && translation.KoText.Length > 15)
            {
                return translation.KoText.Substring(0, 15);
            }

            return translation.KoText;
        }

        private static bool IsLessonBnaPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith("root/scene/produce/lesson/", StringComparison.OrdinalIgnoreCase)
                || normalized.IndexOf("/root/scene/produce/lesson/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static byte[] RebuildBxr(byte[] data, int poolBase, int oldPoolSize, List<Replacement> replacements, BxrPatchResult result)
        {
            byte[] prefix = new byte[poolBase];
            Buffer.BlockCopy(data, 0, prefix, 0, poolBase);

            byte[] pool = new byte[oldPoolSize];
            Buffer.BlockCopy(data, poolBase, pool, 0, oldPoolSize);

            MemoryStream rebuiltPool = new MemoryStream();
            int cursor = 0;
            for (int index = 0; index < replacements.Count; index++)
            {
                Replacement item = replacements[index];
                if (item.RelOffset > cursor)
                {
                    rebuiltPool.Write(pool, cursor, item.RelOffset - cursor);
                }

                rebuiltPool.Write(item.Payload, 0, item.Payload.Length);
                cursor = item.RelOffset + item.OldByteLength;
            }

            if (cursor < pool.Length)
            {
                rebuiltPool.Write(pool, cursor, pool.Length - cursor);
            }

            byte[] newPool = rebuiltPool.ToArray();
            ShiftTable shiftTable = new ShiftTable(replacements);
            int updatedRefs = 0;

            for (int loc = 0; loc < poolBase - 3; loc += 4)
            {
                uint oldRawValue = ReadU32(prefix, loc);
                if (oldRawValue > Int32.MaxValue)
                {
                    continue;
                }

                int oldValue = (int)oldRawValue;
                if (oldValue < 0 || oldValue >= oldPoolSize || !IsStringStartBoundary(data, poolBase, oldValue))
                {
                    continue;
                }

                int newValue = oldValue + shiftTable.GetShift(oldValue);
                if (newValue != oldValue)
                {
                    WriteU32(prefix, loc, checked((uint)newValue));
                    updatedRefs++;
                }
            }

            WriteU32(prefix, 20, checked((uint)newPool.Length));

            byte[] output = new byte[prefix.Length + newPool.Length];
            Buffer.BlockCopy(prefix, 0, output, 0, prefix.Length);
            Buffer.BlockCopy(newPool, 0, output, prefix.Length, newPool.Length);

            result.UpdatedPrefixRefFields += updatedRefs;
            result.StringsPatched += replacements.Count;
            result.VerificationErrors += CountVerificationErrors(output, poolBase, replacements, shiftTable);
            return output;
        }

        private static int CountVerificationErrors(byte[] data, int poolBase, List<Replacement> replacements, ShiftTable shiftTable)
        {
            int errors = 0;
            for (int index = 0; index < replacements.Count; index++)
            {
                Replacement item = replacements[index];
                int newRel = item.RelOffset + shiftTable.GetShift(item.RelOffset);
                int newOffset = poolBase + newRel;
                if (newOffset < poolBase || newOffset + item.Payload.Length > data.Length)
                {
                    errors++;
                    continue;
                }

                for (int byteIndex = 0; byteIndex < item.Payload.Length; byteIndex++)
                {
                    if (data[newOffset + byteIndex] != item.Payload[byteIndex])
                    {
                        errors++;
                        break;
                    }
                }

                if (!HasAlignedPrefixRef(data, poolBase, newRel))
                {
                    errors++;
                }
            }

            return errors;
        }

        private static bool HasAlignedPrefixRef(byte[] data, int poolBase, int rel)
        {
            for (int loc = 0; loc < poolBase - 3; loc += 4)
            {
                if (ReadU32(data, loc) == (uint)rel)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetPool(byte[] data, out int poolBase, out int poolSize)
        {
            poolBase = 0;
            poolSize = 0;
            if (!IsBxr(data) || data.Length < 28)
            {
                return false;
            }

            uint poolSizeValue = ReadU32(data, 20);
            if (poolSizeValue > Int32.MaxValue)
            {
                return false;
            }

            poolSize = (int)poolSizeValue;
            poolBase = data.Length - poolSize;
            return poolSize > 0 && poolBase >= 28 && poolBase <= data.Length;
        }

        private static bool IsBxr(byte[] data)
        {
            return data.Length >= 4
                && data[0] == (byte)'B'
                && data[1] == (byte)'X'
                && data[2] == (byte)'R'
                && data[3] == (byte)'0';
        }

        private static bool IsStringStartBoundary(byte[] data, int poolBase, int relOffset)
        {
            int pos = poolBase + relOffset;
            if (pos < poolBase || pos >= data.Length || data[pos] == 0)
            {
                return false;
            }

            if (pos == poolBase)
            {
                return true;
            }

            return data[pos - 1] == 0 || (pos >= 2 && data[pos - 2] == 0 && data[pos - 1] == 0);
        }

        private static bool TryReadString(byte[] data, int offset, out CandidateString candidate)
        {
            candidate = null;
            int end = offset;
            int limit = Math.Min(data.Length - 1, offset + MaxStringBytes);
            while (end + 1 < data.Length && end <= limit)
            {
                if (data[end] == 0 && data[end + 1] == 0)
                {
                    break;
                }

                end += 2;
            }

            if (end == offset || end + 1 >= data.Length || end > limit)
            {
                return false;
            }

            string text = Encoding.BigEndianUnicode.GetString(data, offset, end - offset);
            candidate = new CandidateString(text, end - offset);
            return true;
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

        private sealed class BxrEntryPatch
        {
            public readonly bool Changed;
            public readonly byte[] Data;

            public BxrEntryPatch(bool changed, byte[] data)
            {
                Changed = changed;
                Data = data;
            }
        }

        private sealed class CandidateString
        {
            public readonly string Text;
            public readonly int ByteLength;

            public CandidateString(string text, int byteLength)
            {
                Text = text;
                ByteLength = byteLength;
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
