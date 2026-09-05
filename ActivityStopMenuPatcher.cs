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
        private const int ActivityStopCodeCaveGuardSize = 0x100;
        private const int ActivityStopWeekIndex = 0x34;
        private const int ActivityStopMenuResult = 3;
        private const int KeepCurrentMenuResult = 2;
        private const int OfficeMorningReentryResult = 0x18;

        private static readonly byte[] MakotoCaveAnchorPattern = ParseHex(
            "deadbeef0020481100000000000000001ae00000000002570000000000800000");
        private static readonly byte[] GetActiveProduceDataPrefix = ParseHex(
            "8143001c812315c42b0a0000419a002c816300203d003dd3");
        private static readonly byte[] OriginalBaseWeekMenuCode = ParseHex(
            "3d608204807e03f838e0ffff38cb48c43d60820438ab48b43d608204388b48a0" +
            "81630000816b004c7d6903a64e800421");
        private static readonly byte[] OriginalTitleUpdateWeekMenuCode = ParseHex(
            "3d608204807e03f838e0ffff38cb52a43d60820438ab52943d608204388b5280" +
            "81630000816b004c7d6903a64e800421");
        private static readonly byte[] OriginalWeekMenuResultTail = ParseHex(
            "39600009907e066c480034d4");
        private static readonly byte[] OriginalWeekMenuClose = ParseHex(
            "807e03f8817e00402b0b000081630000419a1a08816b00787d6903a64e800421" +
            "2b030000419a34bc480034a4");

        private static readonly PatchLayout BaseLayout = new PatchLayout(
            "원본판 v0.0.0.1",
            0x6AEB0,
            0x29F73C,
            0x29F7DC,
            0x29F7E8,
            0x29F810,
            0x235450,
            -0x840,
            0x483B44,
            0x483B80,
            0x82068FB0,
            0x820448C4,
            0x820448B4,
            0x820448A0,
            0x82006000,
            0xDA000,
            0x48A000,
            OriginalBaseWeekMenuCode);

        private static readonly PatchLayout TitleUpdateLayout = new PatchLayout(
            "타이틀 업데이트판 v0.0.2.1",
            0x6B9C0,
            0x2A207C,
            0x2A211C,
            0x2A2128,
            0x2A2150,
            0x236448,
            -0x6C0,
            0x48736C,
            0x4873A8,
            0x82069AC0,
            0x820452A4,
            0x82045294,
            0x82045280,
            0x82006000,
            0xDA000,
            0x48A000,
            OriginalTitleUpdateWeekMenuCode);

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
            PatchLayout layout = DetectLayout(data);
            string donorText = remapper.Apply(ActivityStopText);
            byte[] encodedText = Encoding.BigEndianUnicode.GetBytes(donorText);
            byte[] stringPayload = new byte[encodedText.Length + 2];
            Buffer.BlockCopy(encodedText, 0, stringPayload, 0, encodedText.Length);
            if (stringPayload.Length != 10)
            {
                throw new InvalidDataException("활동중단 문자열은 NUL을 포함해 10바이트여야 합니다.");
            }

            int stringOffset = layout.CaveAnchorOffset + ActivityStopStringOffsetFromAnchor;
            byte[] fourItemMenuCode = BuildFourItemMenuCode(
                layout,
                layout.OriginalWeekMenuCode.Length);
            byte[] resultTrampoline = BuildResultTrampoline(layout);
            byte[] closeTrampoline = BuildCloseTrampoline(layout);
            byte[] resultHook = BuildBranch(
                CodeFileOffsetToGuest(layout, layout.WeekMenuResultTailOffset),
                CodeFileOffsetToGuest(layout, layout.ResultTrampolineOffset),
                false);
            byte[] closeHook = BuildBranch(
                CodeFileOffsetToGuest(layout, layout.WeekMenuCloseCompleteOffset),
                CodeFileOffsetToGuest(layout, layout.CloseTrampolineOffset),
                false);

            int originalMenuOffset = FindPattern(data, layout.OriginalWeekMenuCode);
            if (originalMenuOffset < 0)
            {
                ValidateAlreadyPatched(
                    data,
                    layout,
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

            originalMenuOffset = FindUniquePattern(
                data,
                layout.OriginalWeekMenuCode,
                layout.Name + " week-start three-item menu code");
            if (originalMenuOffset != layout.WeekMenuOffset)
            {
                throw UnexpectedOffset("week-start menu", originalMenuOffset, layout.WeekMenuOffset);
            }

            int resultTailOffset = FindUniquePattern(
                data,
                OriginalWeekMenuResultTail,
                "week-start menu result tail");
            if (resultTailOffset != layout.WeekMenuResultTailOffset)
            {
                throw UnexpectedOffset(
                    "week-start menu result tail",
                    resultTailOffset,
                    layout.WeekMenuResultTailOffset);
            }

            int menuCloseOffset = FindUniquePattern(data, OriginalWeekMenuClose, "week-start menu close sequence");
            if (menuCloseOffset != layout.WeekMenuCloseOffset)
            {
                throw UnexpectedOffset("week-start menu close", menuCloseOffset, layout.WeekMenuCloseOffset);
            }

            int menuCloseCompleteOffset = menuCloseOffset + OriginalWeekMenuClose.Length - 4;
            if (menuCloseCompleteOffset != layout.WeekMenuCloseCompleteOffset)
            {
                throw UnexpectedOffset(
                    "week-start menu close completion",
                    menuCloseCompleteOffset,
                    layout.WeekMenuCloseCompleteOffset);
            }

            List<int> getActiveOffsets = FindAllPatternOffsets(data, GetActiveProduceDataPrefix);
            if (getActiveOffsets.Count != 2 ||
                getActiveOffsets[0] != layout.GetActiveProduceDataOffset ||
                getActiveOffsets[1] != layout.GetActiveProduceDataOffset + 0x48)
            {
                throw new InvalidDataException(layout.Name + " GetActiveProduceData 함수 배치가 예상과 다릅니다.");
            }

            EnsureAllZero(data, stringOffset, stringPayload.Length, "activity-stop string cave");
            EnsureAllZero(
                data,
                layout.ResultTrampolineOffset,
                ActivityStopCodeCaveGuardSize,
                "activity-stop CODE cave");

            WriteBytes(data, stringOffset, stringPayload);
            WriteBytes(data, layout.WeekMenuOffset, fourItemMenuCode);
            WriteBytes(data, layout.WeekMenuResultTailOffset, resultHook);
            WriteBytes(data, layout.WeekMenuCloseCompleteOffset, closeHook);
            WriteBytes(data, layout.ResultTrampolineOffset, resultTrampoline);
            WriteBytes(data, layout.CloseTrampolineOffset, closeTrampoline);

            ValidateAlreadyPatched(
                data,
                layout,
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
            PatchLayout layout,
            int stringOffset,
            byte[] stringPayload,
            byte[] fourItemMenuCode,
            byte[] resultHook,
            byte[] closeHook,
            byte[] resultTrampoline,
            byte[] closeTrampoline)
        {
            int patchedMenuOffset = FindUniquePattern(data, fourItemMenuCode, "patched week-start four-item menu code");
            if (patchedMenuOffset != layout.WeekMenuOffset)
            {
                throw UnexpectedOffset("patched week-start menu", patchedMenuOffset, layout.WeekMenuOffset);
            }

            EnsureMatches(data, stringOffset, stringPayload, "activity-stop string");
            EnsureMatches(data, layout.WeekMenuResultTailOffset, resultHook, "activity-stop result hook");
            EnsureMatches(data, layout.WeekMenuCloseCompleteOffset, closeHook, "activity-stop close hook");
            EnsureMatches(
                data,
                layout.ResultTrampolineOffset,
                resultTrampoline,
                "activity-stop result trampoline");
            EnsureMatches(
                data,
                layout.CloseTrampolineOffset,
                closeTrampoline,
                "activity-stop close trampoline");
        }

        private static byte[] BuildFourItemMenuCode(PatchLayout layout, int expectedSize)
        {
            List<byte> code = new List<byte>();
            Append(code, BuildLoadAddressHigh(11, layout.ActivityStopStringAddress));
            Append(code, BuildDForm(32, 3, 30, 0x3F8));             // lwz r3, 0x3f8(r30)
            Append(code, BuildDForm(14, 8, 0, -1));                 // li r8, -1
            Append(code, BuildAddressLow(7, 11, layout.ActivityStopStringAddress));
            Append(code, BuildLoadAddressHigh(11, layout.KeepCurrentStringAddress));
            Append(code, BuildAddressLow(6, 11, layout.KeepCurrentStringAddress));
            Append(code, BuildAddressLow(5, 11, layout.ChangeSongStringAddress));
            Append(code, BuildAddressLow(4, 11, layout.ChangeCostumeStringAddress));
            Append(code, BuildDForm(32, 11, 3, 0));                 // lwz r11, 0(r3)
            Append(code, BuildDForm(32, 11, 11, 0x44));             // lwz r11, 0x44(r11)
            Append(code, ParseHex("7d6903a6"));                     // mtctr r11
            Append(code, ParseHex("4e800421"));                     // bctrl
            return RequireSize(code.ToArray(), expectedSize, "four-item menu code");
        }

        private static byte[] BuildLoadAddressHigh(int targetRegister, uint address)
        {
            int highAdjusted = (int)((address + 0x8000) >> 16) & 0xFFFF;
            return BuildDForm(15, targetRegister, 0, highAdjusted);
        }

        private static byte[] BuildAddressLow(int targetRegister, int sourceRegister, uint address)
        {
            return BuildDForm(14, targetRegister, sourceRegister, (int)(address & 0xFFFF));
        }

        private static byte[] BuildResultTrampoline(PatchLayout layout)
        {
            uint trampolineAddress = CodeFileOffsetToGuest(layout, layout.ResultTrampolineOffset);
            uint resultTailAddress = CodeFileOffsetToGuest(layout, layout.WeekMenuResultTailOffset);
            uint getActiveAddress = CodeFileOffsetToGuest(layout, layout.GetActiveProduceDataOffset);
            uint commonContinueAddress = resultTailAddress + 0x34DC;
            List<byte> code = new List<byte>();

            Append(code, BuildDForm(11, 0, 3, ActivityStopMenuResult));       // cmpwi r3, 3
            Append(code, ParseHex("4182000c"));                               // beq activity_stop
            Append(code, BuildDForm(14, 11, 0, 9));                           // li r11, 9
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), resultTailAddress + 4, false));
            Append(code, BuildDForm(32, 3, 28, layout.ProduceManagerOffset));  // lwz r3, produce manager(r28)
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), getActiveAddress, true));
            Append(code, BuildDForm(14, 10, 0, ActivityStopWeekIndex));       // li r10, 0x34
            Append(code, BuildDForm(36, 10, 3, 0x240));                       // stw r10, 0x240(r3)
            Append(code, BuildDForm(14, 10, 0, ActivityStopMenuResult));      // li r10, 3
            Append(code, BuildDForm(36, 10, 30, 0x66C));                      // stw r10, 0x66c(r30)
            Append(code, BuildDForm(14, 11, 0, 9));                           // li r11, 9
            Append(code, BuildBranch(CurrentAddress(trampolineAddress, code), commonContinueAddress, false));
            return RequireSize(code.ToArray(), 48, "activity-stop result trampoline");
        }

        private static byte[] BuildCloseTrampoline(PatchLayout layout)
        {
            uint trampolineAddress = CodeFileOffsetToGuest(layout, layout.CloseTrampolineOffset);
            uint resultTailAddress = CodeFileOffsetToGuest(layout, layout.WeekMenuResultTailOffset);
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

        private static PatchLayout DetectLayout(byte[] data)
        {
            int caveAnchorOffset = FindUniquePattern(
                data,
                MakotoCaveAnchorPattern,
                "Makoto/data cave anchor");
            if (caveAnchorOffset == BaseLayout.CaveAnchorOffset)
            {
                return BaseLayout;
            }

            if (caveAnchorOffset == TitleUpdateLayout.CaveAnchorOffset)
            {
                return TitleUpdateLayout;
            }

            throw new InvalidDataException(
                String.Format(
                    "지원하지 않는 default.xex 배치입니다. Makoto/data cave anchor: 0x{0:X}",
                    caveAnchorOffset));
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

        private static uint CodeFileOffsetToGuest(PatchLayout layout, int fileOffset)
        {
            if (fileOffset < layout.CodeFileStart || fileOffset >= layout.CodeFileEnd)
            {
                throw new InvalidDataException("XEX CODE 범위 밖 파일 오프셋입니다.");
            }

            return layout.CodeFileToGuestBias + (uint)fileOffset;
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

        private sealed class PatchLayout
        {
            public readonly string Name;
            public readonly int CaveAnchorOffset;
            public readonly int WeekMenuOffset;
            public readonly int WeekMenuResultTailOffset;
            public readonly int WeekMenuCloseOffset;
            public readonly int WeekMenuCloseCompleteOffset;
            public readonly int GetActiveProduceDataOffset;
            public readonly int ProduceManagerOffset;
            public readonly int ResultTrampolineOffset;
            public readonly int CloseTrampolineOffset;
            public readonly uint ActivityStopStringAddress;
            public readonly uint KeepCurrentStringAddress;
            public readonly uint ChangeSongStringAddress;
            public readonly uint ChangeCostumeStringAddress;
            public readonly uint CodeFileToGuestBias;
            public readonly int CodeFileStart;
            public readonly int CodeFileEnd;
            public readonly byte[] OriginalWeekMenuCode;

            public PatchLayout(
                string name,
                int caveAnchorOffset,
                int weekMenuOffset,
                int weekMenuResultTailOffset,
                int weekMenuCloseOffset,
                int weekMenuCloseCompleteOffset,
                int getActiveProduceDataOffset,
                int produceManagerOffset,
                int resultTrampolineOffset,
                int closeTrampolineOffset,
                uint activityStopStringAddress,
                uint keepCurrentStringAddress,
                uint changeSongStringAddress,
                uint changeCostumeStringAddress,
                uint codeFileToGuestBias,
                int codeFileStart,
                int codeFileEnd,
                byte[] originalWeekMenuCode)
            {
                Name = name;
                CaveAnchorOffset = caveAnchorOffset;
                WeekMenuOffset = weekMenuOffset;
                WeekMenuResultTailOffset = weekMenuResultTailOffset;
                WeekMenuCloseOffset = weekMenuCloseOffset;
                WeekMenuCloseCompleteOffset = weekMenuCloseCompleteOffset;
                GetActiveProduceDataOffset = getActiveProduceDataOffset;
                ProduceManagerOffset = produceManagerOffset;
                ResultTrampolineOffset = resultTrampolineOffset;
                CloseTrampolineOffset = closeTrampolineOffset;
                ActivityStopStringAddress = activityStopStringAddress;
                KeepCurrentStringAddress = keepCurrentStringAddress;
                ChangeSongStringAddress = changeSongStringAddress;
                ChangeCostumeStringAddress = changeCostumeStringAddress;
                CodeFileToGuestBias = codeFileToGuestBias;
                CodeFileStart = codeFileStart;
                CodeFileEnd = codeFileEnd;
                OriginalWeekMenuCode = originalWeekMenuCode;
            }
        }
    }
}
