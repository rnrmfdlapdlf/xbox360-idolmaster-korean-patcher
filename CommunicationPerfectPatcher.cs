using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class CommunicationPerfectPatcher
    {
        private const int ScbSectionTable = 0x70;
        private const int ScbSectionCount = 7;

        private readonly List<CommunicationPerfectPatchEntry> entries;

        private CommunicationPerfectPatcher(List<CommunicationPerfectPatchEntry> entries)
        {
            this.entries = entries;
        }

        public static CommunicationPerfectPatcher Load(string assetRoot)
        {
            string manifestPath = Path.Combine(assetRoot, "communication_always_perfect.jsonl");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Communication perfect patch data was not found.", manifestPath);
            }

            List<CommunicationPerfectPatchEntry> rows = new List<CommunicationPerfectPatchEntry>();
            using (StreamReader reader = new StreamReader(manifestPath, Encoding.UTF8, true))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (line.Trim().Length == 0)
                    {
                        continue;
                    }

                    string eventType = JsonTranslationStore.TryReadStringProperty(line, "event_type");
                    string bna = JsonTranslationStore.TryReadStringProperty(line, "bna");
                    string entry = JsonTranslationStore.TryReadStringProperty(line, "entry");
                    string commandOffset = JsonTranslationStore.TryReadStringProperty(line, "cmd_offset");
                    string score = JsonTranslationStore.TryReadStringProperty(line, "score");
                    string maxScore = JsonTranslationStore.TryReadStringProperty(line, "max_score");
                    if (String.IsNullOrEmpty(eventType) ||
                        String.IsNullOrEmpty(bna) ||
                        String.IsNullOrEmpty(entry) ||
                        String.IsNullOrEmpty(commandOffset) ||
                        String.IsNullOrEmpty(score) ||
                        String.IsNullOrEmpty(maxScore))
                    {
                        throw new InvalidDataException("Invalid communication perfect patch row at line " + lineNumber.ToString() + ".");
                    }

                    rows.Add(new CommunicationPerfectPatchEntry(
                        NormalizePath(bna),
                        NormalizePath(entry),
                        eventType,
                        ParseRequiredOffset(commandOffset, lineNumber),
                        ParseRequiredScore(score, lineNumber),
                        ParseRequiredScore(maxScore, lineNumber)));
                }
            }

            return new CommunicationPerfectPatcher(rows);
        }

        public CommunicationPerfectPatchResult PatchExtractedRoot(string extractedRoot, Action<int, string> progress)
        {
            CommunicationPerfectPatchResult result = new CommunicationPerfectPatchResult();
            result.ManifestRows = entries.Count;
            if (entries.Count == 0)
            {
                return result;
            }

            Dictionary<string, List<CommunicationPerfectPatchEntry>> byBna =
                new Dictionary<string, List<CommunicationPerfectPatchEntry>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.Count; index++)
            {
                CommunicationPerfectPatchEntry entry = entries[index];
                List<CommunicationPerfectPatchEntry> rows;
                if (!byBna.TryGetValue(entry.BnaPath, out rows))
                {
                    rows = new List<CommunicationPerfectPatchEntry>();
                    byBna[entry.BnaPath] = rows;
                }

                rows.Add(entry);
            }

            int bnaIndex = 0;
            foreach (KeyValuePair<string, List<CommunicationPerfectPatchEntry>> pair in byBna)
            {
                if (progress != null)
                {
                    int percent = 65 + (int)(1.0 * bnaIndex / Math.Max(1, byBna.Count));
                    progress(percent, String.Format("\uc601\uc5c5 \ud37c\ud399\ud2b8 \uce58\ud2b8 \uc801\uc6a9 \uc911... {0:N0}/{1:N0}", bnaIndex + 1, byBna.Count));
                }

                bnaIndex++;
                try
                {
                    PatchBnaFile(extractedRoot, pair.Key, pair.Value, result);
                }
                catch
                {
                    result.Errors++;
                }
            }

            if (progress != null)
            {
                progress(66, String.Format("\uc601\uc5c5 \ud37c\ud399\ud2b8 \uce58\ud2b8 \uc644\ub8cc: {0:N0}\uac1c", result.ScoreValuesPatched));
            }

            return result;
        }

        private static void PatchBnaFile(
            string extractedRoot,
            string bnaRelativePath,
            List<CommunicationPerfectPatchEntry> rows,
            CommunicationPerfectPatchResult result)
        {
            string bnaPath = Path.Combine(extractedRoot, bnaRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(bnaPath))
            {
                result.MissingBnaFiles++;
                return;
            }

            result.BnaFilesSeen++;
            byte[] original = File.ReadAllBytes(bnaPath);
            if (!BnaContainer.IsBna(original))
            {
                result.Errors++;
                return;
            }

            BnaContainer bna = BnaContainer.Parse(original);
            Dictionary<string, List<CommunicationPerfectPatchEntry>> byEntry =
                new Dictionary<string, List<CommunicationPerfectPatchEntry>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < rows.Count; index++)
            {
                CommunicationPerfectPatchEntry row = rows[index];
                List<CommunicationPerfectPatchEntry> entryRows;
                if (!byEntry.TryGetValue(row.EntryPath, out entryRows))
                {
                    entryRows = new List<CommunicationPerfectPatchEntry>();
                    byEntry[row.EntryPath] = entryRows;
                }

                entryRows.Add(row);
            }

            bool changed = false;
            foreach (KeyValuePair<string, List<CommunicationPerfectPatchEntry>> pair in byEntry)
            {
                BnaContainerEntry entry = FindEntry(bna.Entries, pair.Key);
                if (entry == null)
                {
                    result.MissingScbEntries++;
                    continue;
                }

                result.ScbEntriesSeen++;
                if (PatchScb(entry.Data, pair.Value, result))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                File.WriteAllBytes(bnaPath, bna.Rebuild());
                result.BnaFilesPatched++;
            }
        }

        private static bool PatchScb(
            byte[] scbData,
            List<CommunicationPerfectPatchEntry> rows,
            CommunicationPerfectPatchResult result)
        {
            int commandSectionOffset;
            int commandSectionSize;
            if (!TryFindScbSection(scbData, "CMD", out commandSectionOffset, out commandSectionSize))
            {
                result.Errors++;
                return false;
            }

            bool changed = false;
            for (int index = 0; index < rows.Count; index++)
            {
                CommunicationPerfectPatchEntry row = rows[index];
                int scoreFieldOffset = GetScoreFieldOffset(row.EventType);
                if (scoreFieldOffset < 0)
                {
                    result.InvalidRows++;
                    continue;
                }

                long commandStart = (long)commandSectionOffset + row.CommandOffset;
                long scoreOffset = commandStart + scoreFieldOffset;
                long commandSectionEnd = (long)commandSectionOffset + commandSectionSize;
                if (commandStart < commandSectionOffset ||
                    scoreOffset < commandSectionOffset ||
                    scoreOffset + 4 > commandSectionEnd ||
                    scoreOffset + 4 > scbData.Length)
                {
                    result.InvalidRows++;
                    continue;
                }

                uint current = ReadU32(scbData, (int)scoreOffset);
                if (current == row.MaxScore)
                {
                    result.ScoreValuesAlreadyPerfect++;
                    continue;
                }

                if (current != row.Score)
                {
                    result.ScoreMismatches++;
                    continue;
                }

                WriteU32(scbData, (int)scoreOffset, row.MaxScore);
                result.ScoreValuesPatched++;
                changed = true;
            }

            return changed;
        }

        private static int GetScoreFieldOffset(string eventType)
        {
            if (String.Equals(eventType, "choice", StringComparison.OrdinalIgnoreCase))
            {
                return 8;
            }

            if (String.Equals(eventType, "touch", StringComparison.OrdinalIgnoreCase))
            {
                return 16;
            }

            return -1;
        }

        private static bool TryFindScbSection(byte[] data, string wantedLabel, out int sectionOffset, out int sectionSize)
        {
            sectionOffset = 0;
            sectionSize = 0;
            if (data.Length < ScbSectionTable + ScbSectionCount * 16 || !StartsWithAscii(data, "SCB"))
            {
                return false;
            }

            for (int index = 0; index < ScbSectionCount; index++)
            {
                int tableOffset = ScbSectionTable + index * 16;
                string label = DecodeSectionLabel(data, tableOffset);
                uint sizeRaw = ReadU32(data, tableOffset + 4);
                uint offsetRaw = ReadU32(data, tableOffset + 8);
                if (sizeRaw > Int32.MaxValue || offsetRaw > Int32.MaxValue)
                {
                    return false;
                }

                int size = (int)sizeRaw;
                int offset = (int)offsetRaw;
                if (offset < 0 || size < 0 || (long)offset + size > data.Length)
                {
                    return false;
                }

                if (String.Equals(label, wantedLabel, StringComparison.Ordinal))
                {
                    sectionOffset = offset;
                    sectionSize = size;
                    return true;
                }
            }

            return false;
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

        private static int ParseRequiredOffset(string value, int lineNumber)
        {
            uint parsed;
            string trimmed = value.Trim();
            if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (UInt32.TryParse(trimmed.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed) &&
                    parsed <= Int32.MaxValue)
                {
                    return (int)parsed;
                }
            }
            else if (UInt32.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) &&
                parsed <= Int32.MaxValue)
            {
                return (int)parsed;
            }

            throw new InvalidDataException("Invalid command offset at communication perfect patch line " + lineNumber.ToString() + ".");
        }

        private static uint ParseRequiredScore(string value, int lineNumber)
        {
            uint parsed;
            if (UInt32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            throw new InvalidDataException("Invalid score at communication perfect patch line " + lineNumber.ToString() + ".");
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

        private static string DecodeSectionLabel(byte[] data, int offset)
        {
            int length = 0;
            while (length < 4 && offset + length < data.Length && data[offset + length] != 0)
            {
                length++;
            }

            return Encoding.ASCII.GetString(data, offset, length);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private sealed class CommunicationPerfectPatchEntry
        {
            public readonly string BnaPath;
            public readonly string EntryPath;
            public readonly string EventType;
            public readonly int CommandOffset;
            public readonly uint Score;
            public readonly uint MaxScore;

            public CommunicationPerfectPatchEntry(
                string bnaPath,
                string entryPath,
                string eventType,
                int commandOffset,
                uint score,
                uint maxScore)
            {
                BnaPath = bnaPath;
                EntryPath = entryPath;
                EventType = eventType;
                CommandOffset = commandOffset;
                Score = score;
                MaxScore = maxScore;
            }
        }
    }
}
