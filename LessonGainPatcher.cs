using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class LessonGainPatcher
    {
        private const int HeaderSize = 28;
        private const int NodeRecordSize = 28;
        private const int AttrRecordSize = 12;
        private const int ExpectedRowCount = 65;
        private const int ExpectedFieldCount = 195;
        private const int ExpectedNonzeroCount = 77;
        private const int ExpectedZeroCount = 118;
        private const string TargetBxrPath = "root/initialFix/lesson.bxr";

        private static readonly string[] StatNames = new string[] { "dance", "visual", "vocal" };
        private static readonly Dictionary<string, LessonExpectedRow> ExpectedRows = BuildExpectedRows();

        public LessonGainPatchResult PatchExtractedRoot(string extractedRoot, Action<int, string> progress)
        {
            if (String.IsNullOrEmpty(extractedRoot))
            {
                throw new ArgumentException("ISO 해제 폴더가 지정되지 않았습니다.", "extractedRoot");
            }

            if (progress != null)
            {
                progress(72, "레슨 능력치 상승 2배 적용 중...");
            }

            string bnaPath = Path.Combine(extractedRoot, Path.Combine("root", Path.Combine("initialFix", "initialFix.bna")));
            if (!File.Exists(bnaPath))
            {
                throw new FileNotFoundException("레슨 능력치 패치 대상 initialFix.bna를 찾을 수 없습니다.", bnaPath);
            }

            byte[] originalBna = File.ReadAllBytes(bnaPath);
            if (!BnaContainer.IsBna(originalBna))
            {
                throw new InvalidDataException("레슨 능력치 패치 대상이 올바른 BNA 파일이 아닙니다.");
            }

            BnaContainer bna = BnaContainer.Parse(originalBna);
            List<BnaContainerEntry> entries = bna.Entries;
            BnaContainerEntry target = null;
            int targetPosition = -1;
            int targetCount = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                if (!String.Equals(entries[index].Path, TargetBxrPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                target = entries[index];
                targetPosition = index;
                targetCount++;
            }

            if (targetCount != 1 || target == null)
            {
                throw new InvalidDataException(
                    String.Format("lesson.bxr 대상 수가 예상과 다릅니다: {0:N0}개", targetCount));
            }

            byte[][] originalEntryData = new byte[entries.Count][];
            for (int index = 0; index < entries.Count; index++)
            {
                originalEntryData[index] = entries[index].Data;
            }

            BxrLessonPatch patch = PatchBxr(target.Data);
            LessonGainPatchResult result = new LessonGainPatchResult();
            result.TargetBnaFound = true;
            result.TargetBxrEntriesSeen = 1;
            result.BnaEntriesVerified = entries.Count;
            result.RowsVerified = patch.RowsVerified;
            result.FieldsVerified = patch.FieldsVerified;
            result.NonzeroValuesPatched = patch.NonzeroValuesPatched;
            result.NonzeroValuesAlreadyPatched = patch.NonzeroValuesAlreadyPatched;
            result.ZeroValuesUnchanged = patch.ZeroValuesUnchanged;
            result.UpdatedReferenceFields = patch.UpdatedReferenceFields;
            result.OtherBnaEntriesPreserved = entries.Count - 1;

            if (patch.Changed)
            {
                target.Data = patch.Data;
                byte[] rebuiltBna = bna.Rebuild();
                BnaContainer verifiedBna = BnaContainer.Parse(rebuiltBna);
                if (verifiedBna.Entries.Count != entries.Count)
                {
                    throw new InvalidDataException("BNA 재빌드 후 엔트리 수가 변경되었습니다.");
                }

                BnaContainerEntry verifiedTarget = null;
                int verifiedTargetCount = 0;
                for (int index = 0; index < verifiedBna.Entries.Count; index++)
                {
                    BnaContainerEntry verifiedEntry = verifiedBna.Entries[index];
                    if (index != targetPosition && !BytesEqual(originalEntryData[index], verifiedEntry.Data))
                    {
                        throw new InvalidDataException(
                            String.Format("레슨 패치 중 대상 외 BNA 엔트리가 변경되었습니다: {0}", verifiedEntry.Path));
                    }

                    if (String.Equals(verifiedEntry.Path, TargetBxrPath, StringComparison.OrdinalIgnoreCase))
                    {
                        verifiedTarget = verifiedEntry;
                        verifiedTargetCount++;
                    }
                }

                if (verifiedTargetCount != 1 || verifiedTarget == null)
                {
                    throw new InvalidDataException("BNA 재빌드 후 lesson.bxr를 확인할 수 없습니다.");
                }

                BxrLessonPatch verification = PatchBxr(verifiedTarget.Data);
                if (verification.Changed ||
                    verification.NonzeroValuesAlreadyPatched != ExpectedNonzeroCount ||
                    verification.ZeroValuesUnchanged != ExpectedZeroCount)
                {
                    throw new InvalidDataException("BNA 재빌드 후 레슨 능력치 2배 검증에 실패했습니다.");
                }

                File.WriteAllBytes(bnaPath, rebuiltBna);
                result.BnaFilesPatched = 1;
            }

            if (progress != null)
            {
                progress(
                    73,
                    String.Format(
                        "레슨 능력치 상승 2배 완료: 변경 {0:N0}개, 이미 적용 {1:N0}개",
                        result.NonzeroValuesPatched,
                        result.NonzeroValuesAlreadyPatched));
            }

            return result;
        }

        private static BxrLessonPatch PatchBxr(byte[] data)
        {
            BxrLayout layout = GetLayout(data);
            Dictionary<string, Dictionary<string, LessonValue>> rows = ParseRows(data, layout);
            List<Replacement> replacements = new List<Replacement>();
            HashSet<int> replacementOffsets = new HashSet<int>();
            int originalNonzero = 0;
            int alreadyDoubled = 0;
            int zeroFields = 0;

            foreach (KeyValuePair<string, LessonExpectedRow> rowPair in ExpectedRows)
            {
                Dictionary<string, LessonValue> values = rows[rowPair.Key];
                for (int statIndex = 0; statIndex < StatNames.Length; statIndex++)
                {
                    string statName = StatNames[statIndex];
                    int expected = rowPair.Value.GetValue(statName);
                    LessonValue current = values[statName];
                    if (expected == 0)
                    {
                        if (current.Value != 0)
                        {
                            throw new InvalidDataException(
                                String.Format("예상하지 못한 레슨 0 필드 값입니다: {0}.{1}={2}", rowPair.Key, statName, current.Value));
                        }

                        zeroFields++;
                    }
                    else if (current.Value == expected)
                    {
                        if (!replacementOffsets.Add(current.RelOffset))
                        {
                            throw new InvalidDataException("레슨 능력치 문자열 오프셋이 중복되었습니다.");
                        }

                        replacements.Add(
                            new Replacement(
                                current.RelOffset,
                                current.ByteLength,
                                Encoding.ASCII.GetBytes(checked(expected * 2).ToString())));
                        originalNonzero++;
                    }
                    else if (current.Value == checked(expected * 2))
                    {
                        alreadyDoubled++;
                    }
                    else
                    {
                        throw new InvalidDataException(
                            String.Format(
                                "예상하지 못한 레슨 능력치 값입니다: {0}.{1}={2}, 예상 {3} 또는 {4}",
                                rowPair.Key,
                                statName,
                                current.Value,
                                expected,
                                expected * 2));
                    }
                }
            }

            if ((originalNonzero != 0 && originalNonzero != ExpectedNonzeroCount) ||
                (alreadyDoubled != 0 && alreadyDoubled != ExpectedNonzeroCount) ||
                zeroFields != ExpectedZeroCount)
            {
                throw new InvalidDataException(
                    String.Format(
                        "레슨 능력치 필드 수가 예상과 다르거나 원본값과 2배값이 섞여 있습니다: 원본 {0}, 2배 {1}, 0 {2}",
                        originalNonzero,
                        alreadyDoubled,
                        zeroFields));
            }

            byte[] output = data;
            int updatedReferenceFields = 0;
            if (replacements.Count > 0)
            {
                replacements.Sort(delegate(Replacement left, Replacement right)
                {
                    return left.RelOffset.CompareTo(right.RelOffset);
                });
                output = RebuildBxr(data, layout, replacements, out updatedReferenceFields);
            }

            BxrLayout verifiedLayout = GetLayout(output);
            Dictionary<string, Dictionary<string, LessonValue>> verifiedRows = ParseRows(output, verifiedLayout);
            foreach (KeyValuePair<string, LessonExpectedRow> rowPair in ExpectedRows)
            {
                for (int statIndex = 0; statIndex < StatNames.Length; statIndex++)
                {
                    string statName = StatNames[statIndex];
                    int expected = rowPair.Value.GetValue(statName);
                    int target = expected == 0 ? 0 : checked(expected * 2);
                    int actual = verifiedRows[rowPair.Key][statName].Value;
                    if (actual != target)
                    {
                        throw new InvalidDataException(
                            String.Format("레슨 능력치 패치 검증 실패: {0}.{1}={2}, 목표 {3}", rowPair.Key, statName, actual, target));
                    }
                }
            }

            return new BxrLessonPatch(
                replacements.Count > 0,
                output,
                ExpectedRowCount,
                ExpectedFieldCount,
                originalNonzero,
                alreadyDoubled,
                zeroFields,
                updatedReferenceFields);
        }

        private static Dictionary<string, Dictionary<string, LessonValue>> ParseRows(byte[] data, BxrLayout layout)
        {
            int typeCount = checked((int)ReadU32(data, 4));
            int attributeNameCount = checked((int)ReadU32(data, 8));
            List<string> symbols = new List<string>();
            int symbolRelOffset = 0;
            for (int index = 0; index < typeCount + attributeNameCount; index++)
            {
                string value = ReadAsciiString(data, layout, symbolRelOffset);
                symbols.Add(value);
                symbolRelOffset = checked(symbolRelOffset + Encoding.ASCII.GetByteCount(value) + 1);
            }

            string[] expectedTypes = new string[] { "lessonParamTable", "lesson", "data" };
            string[] expectedAttributes = new string[] { "id", "dance", "visual", "vocal" };
            if (typeCount != expectedTypes.Length || attributeNameCount != expectedAttributes.Length)
            {
                throw new InvalidDataException("lesson.bxr 심볼 개수가 예상과 다릅니다.");
            }

            for (int index = 0; index < expectedTypes.Length; index++)
            {
                if (!String.Equals(symbols[index], expectedTypes[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException("lesson.bxr 노드 타입 심볼이 예상과 다릅니다.");
                }
            }

            for (int index = 0; index < expectedAttributes.Length; index++)
            {
                if (!String.Equals(symbols[typeCount + index], expectedAttributes[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException("lesson.bxr 속성 심볼이 예상과 다릅니다.");
                }
            }

            BxrAttribute[] attributes = new BxrAttribute[layout.AttrCount];
            for (int index = 0; index < layout.AttrCount; index++)
            {
                int offset = layout.AttrStart + index * AttrRecordSize;
                attributes[index] = new BxrAttribute(
                    ReadU32(data, offset),
                    ReadU32(data, offset + 4),
                    ReadU32(data, offset + 8));
            }

            Dictionary<string, Dictionary<string, LessonValue>> rows =
                new Dictionary<string, Dictionary<string, LessonValue>>(StringComparer.Ordinal);
            for (int index = 0; index < layout.NodeCount; index++)
            {
                int nodeOffset = layout.NodeStart + index * NodeRecordSize;
                uint typeId = ReadU32(data, nodeOffset + 8);
                uint valueOffset = ReadU32(data, nodeOffset + 12);
                uint firstAttribute = ReadU32(data, nodeOffset + 16);
                if (typeId >= typeCount)
                {
                    throw new InvalidDataException("lesson.bxr 노드 타입 ID가 범위를 벗어났습니다.");
                }

                if (!String.Equals(symbols[(int)typeId], "data", StringComparison.Ordinal) || valueOffset == UInt32.MaxValue)
                {
                    continue;
                }

                string rowId = ReadAsciiString(data, layout, checked((int)valueOffset));
                if (!ExpectedRows.ContainsKey(rowId))
                {
                    continue;
                }

                if (rows.ContainsKey(rowId))
                {
                    throw new InvalidDataException("lesson.bxr에 중복 레슨 행이 있습니다: " + rowId);
                }

                Dictionary<string, LessonValue> values = new Dictionary<string, LessonValue>(StringComparer.Ordinal);
                uint attributeIndex = firstAttribute;
                int guard = 0;
                while (attributeIndex != UInt32.MaxValue)
                {
                    if (attributeIndex >= attributes.Length || guard >= attributes.Length)
                    {
                        throw new InvalidDataException("lesson.bxr 속성 체인이 올바르지 않습니다: " + rowId);
                    }

                    BxrAttribute attribute = attributes[(int)attributeIndex];
                    if (attribute.NameId >= attributeNameCount)
                    {
                        throw new InvalidDataException("lesson.bxr 속성 이름 ID가 범위를 벗어났습니다: " + rowId);
                    }

                    string name = symbols[typeCount + (int)attribute.NameId];
                    string text = ReadAsciiString(data, layout, checked((int)attribute.ValueOffset));
                    int numericValue;
                    if (!IsAsciiDigitString(text) || !Int32.TryParse(text, out numericValue))
                    {
                        throw new InvalidDataException("lesson.bxr 능력치가 숫자가 아닙니다: " + rowId + "." + name);
                    }

                    if (values.ContainsKey(name))
                    {
                        throw new InvalidDataException("lesson.bxr 행에 중복 속성이 있습니다: " + rowId + "." + name);
                    }

                    values[name] = new LessonValue(
                        numericValue,
                        checked((int)attribute.ValueOffset),
                        Encoding.ASCII.GetByteCount(text));
                    attributeIndex = attribute.NextIndex;
                    guard++;
                }

                if (values.Count != StatNames.Length)
                {
                    throw new InvalidDataException("lesson.bxr 행의 능력치 필드 수가 예상과 다릅니다: " + rowId);
                }

                for (int statIndex = 0; statIndex < StatNames.Length; statIndex++)
                {
                    if (!values.ContainsKey(StatNames[statIndex]))
                    {
                        throw new InvalidDataException("lesson.bxr 행에 필요한 능력치 필드가 없습니다: " + rowId);
                    }
                }

                rows[rowId] = values;
            }

            if (rows.Count != ExpectedRows.Count)
            {
                throw new InvalidDataException(
                    String.Format("lesson.bxr 레슨 행 수가 예상과 다릅니다: {0:N0}개", rows.Count));
            }

            foreach (string rowId in ExpectedRows.Keys)
            {
                if (!rows.ContainsKey(rowId))
                {
                    throw new InvalidDataException("lesson.bxr에 필요한 레슨 행이 없습니다: " + rowId);
                }
            }

            return rows;
        }

        private static byte[] RebuildBxr(byte[] data, BxrLayout layout, List<Replacement> replacements, out int updatedReferenceFields)
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
                if (replacement.RelOffset < cursor || replacement.RelOffset + replacement.OldByteLength > pool.Length)
                {
                    throw new InvalidDataException("lesson.bxr 문자열 교체 범위가 올바르지 않습니다.");
                }

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
            updatedReferenceFields = UpdateReferenceFields(prefix, layout, shiftTable);
            WriteU32(prefix, 20, checked((uint)newPool.Length));

            byte[] output = new byte[prefix.Length + newPool.Length];
            Buffer.BlockCopy(prefix, 0, output, 0, prefix.Length);
            Buffer.BlockCopy(newPool, 0, output, prefix.Length, newPool.Length);
            VerifyReplacements(output, layout.PoolBase, replacements, shiftTable);
            return output;
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

            int newRel = checked(oldRel + shiftTable.GetShift(oldRel));
            if (newRel == oldRel)
            {
                return 0;
            }

            WriteU32(data, offset, checked((uint)newRel));
            return 1;
        }

        private static void VerifyReplacements(byte[] data, int poolBase, List<Replacement> replacements, ShiftTable shiftTable)
        {
            for (int index = 0; index < replacements.Count; index++)
            {
                Replacement replacement = replacements[index];
                int newRel = checked(replacement.RelOffset + shiftTable.GetShift(replacement.RelOffset));
                int offset = checked(poolBase + newRel);
                if (offset < poolBase || offset + replacement.Payload.Length > data.Length)
                {
                    throw new InvalidDataException("lesson.bxr 교체 문자열이 범위를 벗어났습니다.");
                }

                for (int byteIndex = 0; byteIndex < replacement.Payload.Length; byteIndex++)
                {
                    if (data[offset + byteIndex] != replacement.Payload[byteIndex])
                    {
                        throw new InvalidDataException("lesson.bxr 교체 문자열 검증에 실패했습니다.");
                    }
                }
            }
        }

        private static BxrLayout GetLayout(byte[] data)
        {
            if (!IsBxr(data) || data.Length < HeaderSize)
            {
                throw new InvalidDataException("lesson.bxr가 올바른 BXR 파일이 아닙니다.");
            }

            uint nodeCountRaw = ReadU32(data, 12);
            uint attrCountRaw = ReadU32(data, 16);
            uint poolSizeRaw = ReadU32(data, 20);
            if (nodeCountRaw > Int32.MaxValue || attrCountRaw > Int32.MaxValue || poolSizeRaw > Int32.MaxValue)
            {
                throw new InvalidDataException("lesson.bxr 크기 필드가 지원 범위를 벗어났습니다.");
            }

            int nodeCount = (int)nodeCountRaw;
            int attrCount = (int)attrCountRaw;
            int poolSize = (int)poolSizeRaw;
            int poolBase = checked(data.Length - poolSize);
            int attrStart = checked(poolBase - attrCount * AttrRecordSize);
            int nodeStart = checked(attrStart - nodeCount * NodeRecordSize);
            if (poolSize <= 0 || nodeStart < HeaderSize || attrStart < nodeStart || poolBase < attrStart || poolBase > data.Length)
            {
                throw new InvalidDataException("lesson.bxr 레이아웃이 올바르지 않습니다.");
            }

            return new BxrLayout(nodeStart, nodeCount, attrStart, attrCount, poolBase, poolSize);
        }

        private static string ReadAsciiString(byte[] data, BxrLayout layout, int relOffset)
        {
            if (relOffset < 0 || relOffset >= layout.PoolSize)
            {
                throw new InvalidDataException("lesson.bxr 문자열 오프셋이 범위를 벗어났습니다.");
            }

            int start = layout.PoolBase + relOffset;
            int end = start;
            int poolEnd = layout.PoolBase + layout.PoolSize;
            while (end < poolEnd && data[end] != 0)
            {
                if (data[end] >= 128)
                {
                    throw new InvalidDataException("lesson.bxr에서 ASCII가 아닌 문자열을 발견했습니다.");
                }

                end++;
            }

            if (end >= poolEnd)
            {
                throw new InvalidDataException("lesson.bxr 문자열이 종료되지 않았습니다.");
            }

            return Encoding.ASCII.GetString(data, start, end - start);
        }

        private static bool IsAsciiDigitString(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsBxr(byte[] data)
        {
            return data != null && data.Length >= 4 &&
                data[0] == (byte)'B' && data[1] == (byte)'X' && data[2] == (byte)'R' && data[3] == (byte)'0';
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (Object.ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, LessonExpectedRow> BuildExpectedRows()
        {
            Dictionary<string, LessonExpectedRow> rows = new Dictionary<string, LessonExpectedRow>(StringComparer.Ordinal);

            string[] rankLabels = new string[] { "a", "b", "c", "d", "e" };
            int[] rankPrimary = new int[] { 10, 8, 6, 5, 4 };
            int[] rankDanceSecondary = new int[] { 6, 5, 4, 4, 3 };
            int[] rankSingle = new int[] { 16, 13, 10, 9, 7 };
            AddExpectedGroup(rows, "rank", rankLabels, rankPrimary, rankDanceSecondary, rankSingle);

            string[] tensionLabels = new string[] { "sp", "hi", "normal", "low" };
            int[] tensionPrimary = new int[] { 3, 2, 1, 0 };
            int[] tensionDanceSecondary = new int[] { 1, 1, 1, 0 };
            int[] tensionSingle = new int[] { 4, 3, 2, 0 };
            AddExpectedGroup(rows, "tension", tensionLabels, tensionPrimary, tensionDanceSecondary, tensionSingle);

            string[] evalLabels = new string[] { "perfect", "good", "normal", "bad" };
            int[] evalPrimary = new int[] { 10, 6, 2, 0 };
            int[] evalDanceSecondary = new int[] { 5, 4, 1, 0 };
            int[] evalSingle = new int[] { 15, 10, 3, 0 };
            AddExpectedGroup(rows, "eval", evalLabels, evalPrimary, evalDanceSecondary, evalSingle);

            if (rows.Count != ExpectedRowCount)
            {
                throw new InvalidOperationException("레슨 능력치 기준표 행 수가 올바르지 않습니다.");
            }

            return rows;
        }

        private static void AddExpectedGroup(
            Dictionary<string, LessonExpectedRow> rows,
            string prefix,
            string[] labels,
            int[] primary,
            int[] danceSecondary,
            int[] single)
        {
            for (int index = 0; index < labels.Length; index++)
            {
                AddExpectedRow(rows, prefix + "_voice_" + labels[index], danceSecondary[index], 0, primary[index]);
                AddExpectedRow(rows, prefix + "_pose_" + labels[index], danceSecondary[index], primary[index], 0);
                AddExpectedRow(rows, prefix + "_lyrics_" + labels[index], 0, 0, single[index]);
                AddExpectedRow(rows, prefix + "_dance_" + labels[index], single[index], 0, 0);
                AddExpectedRow(rows, prefix + "_expression_" + labels[index], 0, single[index], 0);
            }
        }

        private static void AddExpectedRow(
            Dictionary<string, LessonExpectedRow> rows,
            string id,
            int dance,
            int visual,
            int vocal)
        {
            if (rows.ContainsKey(id))
            {
                throw new InvalidOperationException("레슨 능력치 기준표에 중복 행이 있습니다: " + id);
            }

            rows[id] = new LessonExpectedRow(dance, visual, vocal);
        }

        private static uint ReadU32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
            {
                throw new InvalidDataException("BXR 32비트 필드가 파일 범위를 벗어났습니다.");
            }

            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        private sealed class LessonExpectedRow
        {
            private readonly int dance;
            private readonly int visual;
            private readonly int vocal;

            public LessonExpectedRow(int dance, int visual, int vocal)
            {
                this.dance = dance;
                this.visual = visual;
                this.vocal = vocal;
            }

            public int GetValue(string name)
            {
                if (String.Equals(name, "dance", StringComparison.Ordinal))
                {
                    return dance;
                }

                if (String.Equals(name, "visual", StringComparison.Ordinal))
                {
                    return visual;
                }

                if (String.Equals(name, "vocal", StringComparison.Ordinal))
                {
                    return vocal;
                }

                throw new ArgumentException("지원하지 않는 레슨 능력치입니다.", "name");
            }
        }

        private sealed class LessonValue
        {
            public readonly int Value;
            public readonly int RelOffset;
            public readonly int ByteLength;

            public LessonValue(int value, int relOffset, int byteLength)
            {
                Value = value;
                RelOffset = relOffset;
                ByteLength = byteLength;
            }
        }

        private sealed class BxrAttribute
        {
            public readonly uint NextIndex;
            public readonly uint NameId;
            public readonly uint ValueOffset;

            public BxrAttribute(uint nextIndex, uint nameId, uint valueOffset)
            {
                NextIndex = nextIndex;
                NameId = nameId;
                ValueOffset = valueOffset;
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

        private sealed class BxrLessonPatch
        {
            public readonly bool Changed;
            public readonly byte[] Data;
            public readonly int RowsVerified;
            public readonly int FieldsVerified;
            public readonly int NonzeroValuesPatched;
            public readonly int NonzeroValuesAlreadyPatched;
            public readonly int ZeroValuesUnchanged;
            public readonly int UpdatedReferenceFields;

            public BxrLessonPatch(
                bool changed,
                byte[] data,
                int rowsVerified,
                int fieldsVerified,
                int nonzeroValuesPatched,
                int nonzeroValuesAlreadyPatched,
                int zeroValuesUnchanged,
                int updatedReferenceFields)
            {
                Changed = changed;
                Data = data;
                RowsVerified = rowsVerified;
                FieldsVerified = fieldsVerified;
                NonzeroValuesPatched = nonzeroValuesPatched;
                NonzeroValuesAlreadyPatched = nonzeroValuesAlreadyPatched;
                ZeroValuesUnchanged = zeroValuesUnchanged;
                UpdatedReferenceFields = updatedReferenceFields;
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
                    shift = checked(shift + replacements[index].Delta);
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

                return found >= 0 ? cumulativeShiftAfter[found] : 0;
            }
        }
    }
}
