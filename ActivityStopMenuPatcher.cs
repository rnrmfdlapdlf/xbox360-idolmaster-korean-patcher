using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class ActivityStopMenuPatcher
    {
        private const string ActivityStopText = "활동중단";
        private const int ActivityStopStringOffsetFromAnchor = 0x100;
        private const int ActivityStopResultTrampolineOffset = 0x483B44;
        private const int ActivityStopCloseTrampolineOffset = 0x483B80;
        private const int ActivityStopCodeCaveGuardSize = 0x100;
        private const int ActivityStopWeekIndex = 0x34;
        private const int ActivityStopMenuResult = 3;
        private const int KeepCurrentMenuResult = 2;
        private const int OfficeMorningReentryResult = 0x18;
        private const int ExpectedCaveAnchorOffset = 0x6AEB0;
        private const int ExpectedWeekMenuOffset = 0x29F73C;
        private const int ExpectedWeekMenuResultTailOffset = 0x29F7DC;
        private const int ExpectedWeekMenuCloseOffset = 0x29F7E8;
        private const int ExpectedWeekMenuCloseCompleteOffset = 0x29F810;
        private const int ExpectedGetActiveProduceDataOffset = 0x235450;
        private const uint ExpectedActivityStopStringAddress = 0x82068FB0;
        private const uint XexCodeFileToGuestBias = 0x82006000;
        private const int XexCodeFileStart = 0xDA000;
        private const int XexCodeFileEnd = 0x48A000;

        private static readonly byte[] MakotoCaveAnchorPattern = ParseHex(
            "deadbeef0020481100000000000000001ae00000000002570000000000800000");
        private static readonly byte[] GetActiveProduceDataPrefix = ParseHex(
            "8143001c812315c42b0a0000419a002c816300203d003dd3");
        private static readonly byte[] OriginalWeekMenuCode = ParseHex(
            "3d608204807e03f838e0ffff38cb48c43d60820438ab48b43d608204388b48a0" +
            "81630000816b004c7d6903a64e800421");
        private static readonly byte[] OriginalWeekMenuResultTail = ParseHex(
            "39600009907e066c480034d4");
        private static readonly byte[] OriginalWeekMenuClose = ParseHex(
            "807e03f8817e00402b0b000081630000419a1a08816b00787d6903a64e800421" +
            "2b030000419a34bc480034a4");

        public static ActivityStopMenuPatchResult Patch(byte[] data, HangulRemapper remapper)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (remapper == null)
            {
                throw new ArgumentNullException("remapper");
            }

            ActivityStopMenuPatchResult result = new ActivityStopMenuPatchResult();
            string donorText = remapper.Apply(ActivityStopText);
            byte[] encodedText = Encoding.BigEndianUnicode.GetBytes(donorText);
            byte[] stringPayload = new byte[encodedText.Length + 2];
            Buffer.BlockCopy(encodedText, 0, stringPayload, 0, encodedText.Length);
            if (stringPayload.Length != 10)
            {
                throw new InvalidDataException("활동중단 문자열은 NUL을 포함해 10바이트여야 합니다.");
            }

            int caveAnchorOffset = FindUniquePattern(data, MakotoCaveAnchorPattern, "Makoto/data cave anchor");
            if (caveAnchorOffset != ExpectedCaveAnchorOffset)
            {
                throw UnexpectedOffset("Makoto/data cave anchor", caveAnchorOffset, ExpectedCaveAnchorOffset);
            }

            int stringOffset = caveAnchorOffset + ActivityStopStringOffsetFromAnchor;
            byte[] fourItemMenuCode = BuildFourItemMenuCode(ExpectedActivityStopStringAddress);
            byte[] resultTrampoline = BuildResultTrampoline();
            byte[] closeTrampoline = BuildCloseTrampoline();
            byte[] resultHook = BuildBranch(
                CodeFileOffsetToGuest(ExpectedWeekMenuResultTailOffset),
                CodeFileOffsetToGuest(ActivityStopResultTrampolineOffset),
                false);
            byte[] closeHook = BuildBranch(
                CodeFileOffsetToGuest(ExpectedWeekMenuCloseCompleteOffset),
                CodeFileOffsetToGuest(ActivityStopCloseTrampolineOffset),
                false);

            int originalMenuOffset = FindPattern(data, OriginalWeekMenuCode);
            if (originalMenuOffset < 0)
            {
                ValidateAlreadyPatched(
                    data,
                    stringOffset,
                    stringPayload,
                    fourItemMenuCode,
                    resultHook,
                    closeHook,
                    resultTrampoline,
                    closeTrampoline);
                result.MenusAlreadyPatched++;
                return result;
            }

            originalMenuOffset = FindUniquePattern(data, OriginalWeekMenuCode, "week-start three-item menu code");
            if (originalMenuOffset != ExpectedWeekMenuOffset)
            {
                throw UnexpectedOffset("week-start menu", originalMenuOffset, ExpectedWeekMenuOffset);
            }

            int resultTailOffset = FindUniquePattern(
                data,
                OriginalWeekMenuResultTail,
                "week-start menu result tail");
            if (resultTailOffset != ExpectedWeekMenuResultTailOffset)
            {
                throw UnexpectedOffset(
                    "week-start menu result tail",
                    resultTailOffset,
                    ExpectedWeekMenuResultTailOffset);
            }

            int menuCloseOffset = FindUniquePattern(data, OriginalWeekMenuClose, "week-start menu close sequence");
            if (menuCloseOffset != ExpectedWeekMenuCloseOffset)
            {
                throw UnexpectedOffset("week-start menu close", menuCloseOffset, ExpectedWeekMenuCloseOffset);
            }

            int menuCloseCompleteOffset = menuCloseOffset + OriginalWeekMenuClose.Length - 4;
            if (menuCloseCompleteOffset != ExpectedWeekMenuCloseCompleteOffset)
            {
                throw UnexpectedOffset(
                    "week-start menu close completion",
                    menuCloseCompleteOffset,
                    ExpectedWeekMenuCloseCompleteOffset);
            }

            List<int> getActiveOffsets = FindAllPatternOffsets(data, GetActiveProduceDataPrefix);
            if (getActiveOffsets.Count != 2 ||
                getActiveOffsets[0] != ExpectedGetActiveProduceDataOffset ||
                getActiveOffsets[1] != ExpectedGetActiveProduceDataOffset + 0x48)
            {
                throw new InvalidDataException("GetActiveProduceData 함수 배치가 예상과 다릅니다.");
            }

            EnsureAllZero(data, stringOffset, stringPayload.Length, "activity-stop string cave");
            EnsureAllZero(
                data,
                ActivityStopResultTrampolineOffset,
                ActivityStopCodeCaveGuardSize,
                "activity-stop CODE cave");

            WriteBytes(data, stringOffset, stringPayload);
            WriteBytes(data, ExpectedWeekMenuOffset, fourItemMenuCode);
            WriteBytes(data, ExpectedWeekMenuResultTailOffset, resultHook);
            WriteBytes(data, ExpectedWeekMenuCloseCompleteOffset, closeHook);
            WriteBytes(data, ActivityStopResultTrampolineOffset, resultTrampoline);
            WriteBytes(data, ActivityStopCloseTrampolineOffset, closeTrampoline);

            ValidateAlreadyPatched(
                data,
                stringOffset,
                stringPayload,
                fourItemMenuCode,
                resultHook,
                closeHook,
                resultTrampoline,
                closeTrampoline);
            result.MenusPatched++;
            return result;
        }

        private static void ValidateAlreadyPatched(
            byte[] data,
            int stringOffset,
            byte[] stringPayload,
            byte[] fourItemMenuCode,
            byte[] resultHook,
            byte[] closeHook,
            byte[] resultTrampoline,
            byte[] closeTrampoline)
        {
            int patchedMenuOffset = FindUniquePattern(data, fourItemMenuCode, "patched week-start four-item menu code");
            if (patchedMenuOffset != ExpectedWeekMenuOffset)
            {
                throw UnexpectedOffset("patched week-start menu", patchedMenuOffset, ExpectedWeekMenuOffset);
            }

            EnsureMatches(data, stringOffset, stringPayload, "activity-stop string");
            EnsureMatches(data, ExpectedWeekMenuResultTailOffset, resultHook, "activity-stop result hook");
            EnsureMatches(data, ExpectedWeekMenuCloseCompleteOffset, closeHook, "activity-stop close hook");
            EnsureMatches(
                data,
                ActivityStopResultTrampolineOffset,
                resultTrampoline,
                "activity-stop result trampoline");
            EnsureMatches(
                data,
                ActivityStopCloseTrampolineOffset,
                closeTrampoline,
                "activity-stop close trampoline");
        }

        private static byte[] BuildFourItemMenuCode(uint stringAddress)
        {
            List<byte> code = new List<byte>();
            int highAdjusted = (int)((stringAddress + 0x8000) >> 16) & 0xFFFF;
            int low = (int)(stringAddress & 0xFFFF);
            Append(code, BuildDForm(15, 11, 0, highAdjusted));       // lis r11, activity-stop@ha
            Append(code, BuildDForm(32, 3, 30, 0x3F8));             // lwz r3, 0x3f8(r30)
            Append(code, BuildDForm(14, 8, 0, -1));                 // li r8, -1
            Append(code, BuildDForm(14, 7, 11, low));               // addi r7, r11, activity-stop@l
            Append(code, BuildDForm(15, 11, 0, 0x8204));            // lis r11, 0x8204
            Append(code, BuildDForm(14, 6, 11, 0x48C4));            // addi r6, r11, 이대로
            Append(code, BuildDForm(14, 5, 11, 0x48B4));            // addi r5, r11, 곡 변경
            Append(code, BuildDForm(14, 4, 11, 0x48A0));            // addi r4, r11, 의상 변경
            Append(code, BuildDForm(32, 11, 3, 0));                 // lwz r11, 0(r3)
            Append(code, BuildDForm(32, 11, 11, 0x44));             // lwz r11, 0x44(r11)
            Append(code, ParseHex("7d6903a6"));                     // mtctr r11
            Append(code, ParseHex("4e800421"));                     // bctrl
            return RequireSize(code.ToArray(), OriginalWeekMenuCode.Length, "four-item menu code");
        }

        private static byte[] BuildResultTrampoline()
        {
            uint trampolineAddress = CodeFileOffsetToGuest(ActivityStopResultTrampolineOffset);
            uint resultTailAddress = CodeFileOffsetToGuest(ExpectedWeekMenuResultTailOffset);
            uint getActiveAddress = CodeFileOffsetToGuest(ExpectedGetActiveProduceDataOffset);
            uint commonContinueAddress = resultTailAddress + 0x34DC;
            List<byte> code = new List<byte>();

            Append(code, BuildDForm(11, 0, 3, ActivityStopMenuResult));       // cmpwi r3, 3
            Append(code, ParseHex("4182000c"));                               // beq activity_stop
            Append(code, BuildDForm(14, 11, 0, 9));                           // li r11, 9
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), resultTailAddress + 4, false));
            Append(code, BuildDForm(32, 3, 28, -0x840));                      // lwz r3, -0x840(r28)
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), getActiveAddress, true));
            Append(code, BuildDForm(14, 10, 0, ActivityStopWeekIndex));       // li r10, 0x34
            Append(code, BuildDForm(36, 10, 3, 0x240));                       // stw r10, 0x240(r3)
            Append(code, BuildDForm(14, 10, 0, ActivityStopMenuResult));      // li r10, 3
            Append(code, BuildDForm(36, 10, 30, 0x66C));                      // stw r10, 0x66c(r30)
            Append(code, BuildDForm(14, 11, 0, 9));                           // li r11, 9
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), commonContinueAddress, false));
            return RequireSize(code.ToArray(), 48, "activity-stop result trampoline");
        }

        private static byte[] BuildCloseTrampoline()
        {
            uint trampolineAddress = CodeFileOffsetToGuest(ActivityStopCloseTrampolineOffset);
            uint resultTailAddress = CodeFileOffsetToGuest(ExpectedWeekMenuResultTailOffset);
            uint commonContinueAddress = resultTailAddress + 0x34DC;
            List<byte> code = new List<byte>();

            Append(code, BuildDForm(32, 10, 30, 0x66C));                      // lwz r10, 0x66c(r30)
            Append(code, BuildDForm(11, 0, 10, ActivityStopMenuResult));      // cmpwi r10, 3
            Append(code, ParseHex("4182000c"));                               // beq activity_stop
            Append(code, BuildDForm(14, 11, 0, 1));                           // li r11, 1
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), commonContinueAddress, false));
            Append(code, BuildDForm(14, 10, 0, KeepCurrentMenuResult));       // li r10, 2
            Append(code, BuildDForm(36, 10, 30, 0x66C));                      // stw r10, 0x66c(r30)
            Append(code, BuildDForm(14, 10, 0, OfficeMorningReentryResult));  // li r10, 0x18
            Append(code, BuildDForm(36, 10, 30, 0x24));                       // stw r10, 0x24(r30)
            Append(code, BuildDForm(32, 11, 30, 0));                          // lwz r11, 0(r30)
            Append(code, BuildDForm(32, 11, 11, 0x0C));                       // lwz r11, 0x0c(r11)
            Append(code, ParseHex("7fc3f378"));                               // mr r3, r30
            Append(code, ParseHex("7d6903a6"));                               // mtctr r11
            Append(code, ParseHex("4e800421"));                               // bctrl
            Append(code, BuildDForm(14, 11, 0, 1));                           // li r11, 1
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), commonContinueAddress, false));
            return RequireSize(code.ToArray(), 64, "activity-stop close trampoline");
        }

        private static uint CurrentAddress(uint startAddress, List<byte> code)
        {
            return startAddress + (uint)code.Count;
        }

        private static byte[] BuildDForm(int opcode, int target, int source, int immediate)
        {
            uint word = ((uint)opcode << 26)
                | ((uint)target << 21)
                | ((uint)source << 16)
                | ((uint)immediate & 0xFFFF);
            return U32Be(word);
        }

        private static byte[] BuildBranch(uint sourceAddress, uint targetAddress, bool link)
        {
            long delta = (long)targetAddress - sourceAddress;
            if ((delta & 3) != 0)
            {
                throw new InvalidDataException("PowerPC 분기 주소가 정렬되지 않았습니다.");
            }

            if (delta < -0x02000000L || delta > 0x01FFFFFCL)
            {
                throw new InvalidDataException("PowerPC 분기 대상이 허용 범위를 벗어났습니다.");
            }

            uint word = 0x48000000u | ((uint)delta & 0x03FFFFFCu);
            if (link)
            {
                word |= 1;
            }

            return U32Be(word);
        }

        private static uint CodeFileOffsetToGuest(int fileOffset)
        {
            if (fileOffset < XexCodeFileStart || fileOffset >= XexCodeFileEnd)
            {
                throw new InvalidDataException("XEX CODE 범위 밖 파일 오프셋입니다.");
            }

            return XexCodeFileToGuestBias + (uint)fileOffset;
        }

        private static byte[] RequireSize(byte[] value, int expectedSize, string label)
        {
            if (value.Length != expectedSize)
            {
                throw new InvalidDataException(
                    String.Format("{0} 크기가 예상과 다릅니다: {1} != {2}", label, value.Length, expectedSize));
            }

            return value;
        }

        private static void EnsureAllZero(byte[] data, int offset, int length, string label)
        {
            EnsureRange(data, offset, length, label);
            for (int index = 0; index < length; index++)
            {
                if (data[offset + index] != 0)
                {
                    throw new InvalidDataException(label + " is not empty in default.xex.");
                }
            }
        }

        private static void EnsureMatches(byte[] data, int offset, byte[] expected, string label)
        {
            EnsureRange(data, offset, expected.Length, label);
            for (int index = 0; index < expected.Length; index++)
            {
                if (data[offset + index] != expected[index])
                {
                    throw new InvalidDataException(label + " bytes do not match default.xex.");
                }
            }
        }

        private static void WriteBytes(byte[] data, int offset, byte[] value)
        {
            EnsureRange(data, offset, value.Length, "activity-stop patch");
            Buffer.BlockCopy(value, 0, data, offset, value.Length);
        }

        private static void EnsureRange(byte[] data, int offset, int length, string label)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
            {
                throw new InvalidDataException(label + " would be outside default.xex.");
            }
        }

        private static InvalidDataException UnexpectedOffset(string label, int actual, int expected)
        {
            return new InvalidDataException(
                String.Format("{0} offset is unexpected: 0x{1:X} != 0x{2:X}", label, actual, expected));
        }

        private static int FindPattern(byte[] data, byte[] pattern)
        {
            if (pattern.Length == 0 || data.Length < pattern.Length)
            {
                return -1;
            }

            for (int offset = 0; offset <= data.Length - pattern.Length; offset++)
            {
                if (MatchesAt(data, offset, pattern))
                {
                    return offset;
                }
            }

            return -1;
        }

        private static int FindUniquePattern(byte[] data, byte[] pattern, string label)
        {
            List<int> offsets = FindAllPatternOffsets(data, pattern);
            if (offsets.Count == 0)
            {
                throw new InvalidDataException(label + " was not found in default.xex.");
            }

            if (offsets.Count != 1)
            {
                throw new InvalidDataException(label + " is not unique in default.xex.");
            }

            return offsets[0];
        }

        private static List<int> FindAllPatternOffsets(byte[] data, byte[] pattern)
        {
            List<int> offsets = new List<int>();
            if (pattern.Length == 0 || data.Length < pattern.Length)
            {
                return offsets;
            }

            for (int offset = 0; offset <= data.Length - pattern.Length; offset++)
            {
                if (MatchesAt(data, offset, pattern))
                {
                    offsets.Add(offset);
                }
            }

            return offsets;
        }

        private static bool MatchesAt(byte[] data, int offset, byte[] pattern)
        {
            for (int index = 0; index < pattern.Length; index++)
            {
                if (data[offset + index] != pattern[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void Append(List<byte> target, byte[] value)
        {
            target.AddRange(value);
        }

        private static byte[] U32Be(uint value)
        {
            return new byte[]
            {
                (byte)(value >> 24),
                (byte)(value >> 16),
                (byte)(value >> 8),
                (byte)value,
            };
        }

        private static byte[] ParseHex(string value)
        {
            if ((value.Length & 1) != 0)
            {
                throw new ArgumentException("Hex string length must be even.", "value");
            }

            byte[] result = new byte[value.Length / 2];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            }

            return result;
        }
    }
}
