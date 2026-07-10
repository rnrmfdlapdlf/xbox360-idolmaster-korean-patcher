using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class XexTextPatcher
    {
        private const int MaxCandidateChars = 256;
        private const string MakotoTextId = "jp_e756c6ef2a8e66f5";
        private static readonly byte[] MakotoPointerCodePrefix = new byte[]
        {
            0x3D, 0x60,
        };
        private static readonly byte[] MakotoPointerCodeMiddle = new byte[]
        {
            0x7C, 0x7B, 0x1B, 0x78,
            0x3B, 0xAB,
        };
        private static readonly byte[] MakotoCaveAnchorPattern = new byte[]
        {
            0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x20, 0x48, 0x11,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x1A, 0xE0, 0x00, 0x00, 0x00, 0x00, 0x02, 0x57,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00,
        };
        private static readonly byte[] BootLogoProjectHeightPattern = new byte[]
        {
            0x82, 0x26, 0xB3, 0x78,
            0x82, 0x26, 0x99, 0x70,
            0x82, 0x26, 0x99, 0x78,
            0x43, 0x78, 0x00, 0x00,
            0x43, 0xC2, 0x80, 0x00,
            0xC3, 0x88, 0x00, 0x00
        };
        private static readonly byte[] BootLogoProjectHeight384Pattern = new byte[]
        {
            0x82, 0x26, 0xB3, 0x78,
            0x82, 0x26, 0x99, 0x70,
            0x82, 0x26, 0x99, 0x78,
            0x43, 0xC0, 0x00, 0x00,
            0x43, 0xC2, 0x80, 0x00,
            0xC3, 0x88, 0x00, 0x00
        };
        private static readonly byte[] BootLogoProjectHeight512Pattern = new byte[]
        {
            0x82, 0x26, 0xB3, 0x78,
            0x82, 0x26, 0x99, 0x70,
            0x82, 0x26, 0x99, 0x78,
            0x44, 0x00, 0x00, 0x00,
            0x43, 0xC2, 0x80, 0x00,
            0xC3, 0x88, 0x00, 0x00
        };
        private static readonly byte[] BootLogoProjectHeight512 = new byte[] { 0x44, 0x00, 0x00, 0x00 };

        private readonly Dictionary<string, string> translations;
        private readonly HangulRemapper remapper;

        public XexTextPatcher(Dictionary<string, string> translations, HangulRemapper remapper)
        {
            this.translations = translations;
            this.remapper = remapper;
        }

        public XexPatchResult PatchExtractedRoot(string extractedRoot, string xexToolPath, string workRoot, Action<int, string> progress)
        {
            XexPatchResult result = new XexPatchResult();
            result.TranslationRows = translations.Count;
            if (translations.Count == 0)
            {
                return result;
            }

            string defaultXexPath = Path.Combine(extractedRoot, "default.xex");
            if (!File.Exists(defaultXexPath))
            {
                throw new FileNotFoundException("default.xex를 찾을 수 없습니다.", defaultXexPath);
            }

            if (!File.Exists(xexToolPath))
            {
                throw new FileNotFoundException("xextool.exe를 찾을 수 없습니다.", xexToolPath);
            }

            string patchWorkRoot = Path.Combine(workRoot, "xex_patch");
            Directory.CreateDirectory(patchWorkRoot);

            string decryptedPath = Path.Combine(patchWorkRoot, "default_decrypted_uncompressed.xex");
            Report(progress, 76, "default.xex 변환 중...");
            ExternalToolRunner.Run(
                xexToolPath,
                "-e d -c u -o " + ExternalToolRunner.QuoteArgument(decryptedPath) + " " + ExternalToolRunner.QuoteArgument(defaultXexPath),
                Path.GetDirectoryName(xexToolPath),
                "xextool.exe");

            if (!File.Exists(decryptedPath))
            {
                throw new FileNotFoundException("변환된 default.xex를 찾을 수 없습니다.", decryptedPath);
            }

            byte[] data = File.ReadAllBytes(decryptedPath);
            Report(progress, 78, "default.xex 문자열 패치 중...");
            PatchUtf16BeStrings(data, result);
            PatchBootLogoProjectHeight(data, result);

            if (result.StringsPatched == 0)
            {
                throw new InvalidOperationException("default.xex에 반영된 문자열이 0개입니다.");
            }

            File.WriteAllBytes(defaultXexPath, data);
            Report(progress, 80, String.Format("default.xex 패치 완료: {0:N0}개 문자열", result.StringsPatched));
            return result;
        }

        private static void PatchBootLogoProjectHeight(byte[] data, XexPatchResult result)
        {
            int offset = FindPattern(data, BootLogoProjectHeightPattern);
            if (offset >= 0)
            {
                Buffer.BlockCopy(BootLogoProjectHeight512, 0, data, offset + 12, BootLogoProjectHeight512.Length);
                result.BootLogoLayoutPatched++;
                return;
            }

            offset = FindPattern(data, BootLogoProjectHeight384Pattern);
            if (offset >= 0)
            {
                Buffer.BlockCopy(BootLogoProjectHeight512, 0, data, offset + 12, BootLogoProjectHeight512.Length);
                result.BootLogoLayoutPatched++;
                return;
            }

            if (FindPattern(data, BootLogoProjectHeight512Pattern) >= 0)
            {
                result.BootLogoLayoutAlreadyPatched++;
            }
        }

        private static int FindPattern(byte[] data, byte[] pattern)
        {
            if (pattern.Length == 0 || data.Length < pattern.Length)
            {
                return -1;
            }

            for (int offset = 0; offset <= data.Length - pattern.Length; offset++)
            {
                bool matched = true;
                for (int index = 0; index < pattern.Length; index++)
                {
                    if (data[offset + index] != pattern[index])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return offset;
                }
            }

            return -1;
        }

        private void PatchUtf16BeStrings(byte[] data, XexPatchResult result)
        {
            List<PatchedRange> patchedRanges = new List<PatchedRange>();

            for (int offset = 0; offset + 4 <= data.Length; offset += 2)
            {
                CandidateString candidate;
                if (!TryReadCandidate(data, offset, out candidate))
                {
                    continue;
                }

                result.CandidateStringsScanned++;
                string textId = "jp_" + Sha1Prefix(candidate.Text);
                string koText;
                if (!translations.TryGetValue(textId, out koText))
                {
                    continue;
                }

                result.StringsMatched++;
                if (OverlapsAny(offset, candidate.EndOffset, patchedRanges))
                {
                    continue;
                }

                string donorText;
                try
                {
                    donorText = remapper.Apply(koText);
                }
                catch
                {
                    result.MissingRemapErrors++;
                    continue;
                }

                byte[] encoded = Encoding.BigEndianUnicode.GetBytes(donorText);
                int payloadLength = encoded.Length + 2;
                if (payloadLength > candidate.SlotBytes)
                {
                    if (String.Equals(textId, MakotoTextId, StringComparison.Ordinal))
                    {
                        PatchRelocatedMakotoString(data, offset, encoded);
                        patchedRanges.Add(new PatchedRange(offset, candidate.EndOffset));
                        result.StringsPatched++;
                        result.RelocatedStringsPatched++;
                        continue;
                    }

                    result.ReplacementsTooLong++;
                    continue;
                }

                Array.Clear(data, offset, candidate.SlotBytes);
                Buffer.BlockCopy(encoded, 0, data, offset, encoded.Length);
                patchedRanges.Add(new PatchedRange(offset, offset + candidate.SlotBytes));
                result.StringsPatched++;
            }
        }

        private static void PatchRelocatedMakotoString(byte[] data, int originalStringOffset, byte[] encoded)
        {
            byte[] payload = new byte[encoded.Length + 2];
            Buffer.BlockCopy(encoded, 0, payload, 0, encoded.Length);

            int caveAnchorOffset = FindUniquePattern(data, MakotoCaveAnchorPattern, "Makoto relocation cave anchor");
            int relocatedStringOffset = caveAnchorOffset + MakotoCaveAnchorPattern.Length;
            if (relocatedStringOffset < 0 || relocatedStringOffset + payload.Length > data.Length)
            {
                throw new InvalidDataException("Makoto relocation string would be outside default.xex.");
            }

            for (int index = 0; index < payload.Length; index++)
            {
                if (data[relocatedStringOffset + index] != 0)
                {
                    throw new InvalidDataException("Makoto relocation cave is not empty in default.xex.");
                }
            }

            uint originalRuntimeAddress = FileOffsetToRuntimeAddress(data, originalStringOffset);
            byte[] pointerPattern = BuildMakotoPointerPattern(originalRuntimeAddress);
            int pointerCodeOffset = FindUniquePattern(data, pointerPattern, "Makoto string pointer code");
            uint relocatedRuntimeAddress = FileOffsetToRuntimeAddress(data, relocatedStringOffset);

            Buffer.BlockCopy(payload, 0, data, relocatedStringOffset, payload.Length);
            PatchLisAddiAddress(data, pointerCodeOffset, relocatedRuntimeAddress);
        }

        private static byte[] BuildMakotoPointerPattern(uint runtimeAddress)
        {
            ushort high = (ushort)((runtimeAddress + 0x8000u) >> 16);
            ushort low = (ushort)(runtimeAddress & 0xFFFFu);
            byte[] pattern = new byte[12];
            Buffer.BlockCopy(MakotoPointerCodePrefix, 0, pattern, 0, MakotoPointerCodePrefix.Length);
            WriteU16Be(pattern, 2, high);
            Buffer.BlockCopy(MakotoPointerCodeMiddle, 0, pattern, 4, MakotoPointerCodeMiddle.Length);
            WriteU16Be(pattern, 10, low);
            return pattern;
        }

        private static void PatchLisAddiAddress(byte[] data, int offset, uint runtimeAddress)
        {
            ushort high = (ushort)((runtimeAddress + 0x8000u) >> 16);
            ushort low = (ushort)(runtimeAddress & 0xFFFFu);
            WriteU16Be(data, offset + 2, high);
            WriteU16Be(data, offset + 10, low);
        }

        private static uint FileOffsetToRuntimeAddress(byte[] data, int fileOffset)
        {
            int imageOffset = FindEmbeddedPeImageOffset(data);
            int peOffset = imageOffset + ReadI32Le(data, imageOffset + 0x3C);
            int sectionCount = ReadU16Le(data, peOffset + 6);
            int optionalHeaderSize = ReadU16Le(data, peOffset + 20);
            int optionalHeaderOffset = peOffset + 24;
            if (ReadU16Le(data, optionalHeaderOffset) != 0x10B)
            {
                throw new InvalidDataException("Embedded default.xex image is not PE32.");
            }

            uint imageBase = ReadU32Le(data, optionalHeaderOffset + 28);
            int sectionOffset = optionalHeaderOffset + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int current = sectionOffset + index * 40;
                uint virtualAddress = ReadU32Le(data, current + 12);
                uint rawSize = ReadU32Le(data, current + 16);
                uint rawPointer = ReadU32Le(data, current + 20);
                long rawStart = imageOffset + rawPointer;
                long rawEnd = rawStart + rawSize;
                if (fileOffset >= rawStart && fileOffset < rawEnd)
                {
                    uint sectionOffsetInFile = (uint)(fileOffset - rawStart);
                    return imageBase + virtualAddress + sectionOffsetInFile;
                }
            }

            throw new InvalidDataException("Could not map default.xex file offset to a runtime address.");
        }

        private static int FindEmbeddedPeImageOffset(byte[] data)
        {
            for (int offset = 0; offset + 0x40 <= data.Length && offset <= 0x10000; offset += 0x200)
            {
                if (data[offset] != 0x4D || data[offset + 1] != 0x5A)
                {
                    continue;
                }

                int peRelativeOffset = ReadI32Le(data, offset + 0x3C);
                int peOffset = offset + peRelativeOffset;
                if (peRelativeOffset >= 0
                    && peOffset >= offset
                    && peOffset + 4 <= data.Length
                    && data[peOffset] == 0x50
                    && data[peOffset + 1] == 0x45
                    && data[peOffset + 2] == 0
                    && data[peOffset + 3] == 0)
                {
                    return offset;
                }
            }

            throw new InvalidDataException("Could not find the embedded PE image in default.xex.");
        }

        private static int FindUniquePattern(byte[] data, byte[] pattern, string label)
        {
            int found = FindPattern(data, pattern);
            if (found < 0)
            {
                throw new InvalidDataException(label + " was not found in default.xex.");
            }

            for (int offset = found + 1; offset <= data.Length - pattern.Length; offset++)
            {
                bool matched = true;
                for (int index = 0; index < pattern.Length; index++)
                {
                    if (data[offset + index] != pattern[index])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    throw new InvalidDataException(label + " is not unique in default.xex.");
                }
            }

            return found;
        }

        private static int ReadU16Le(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length)
            {
                throw new InvalidDataException("Unexpected end of default.xex while reading PE metadata.");
            }

            return data[offset] | (data[offset + 1] << 8);
        }

        private static int ReadI32Le(byte[] data, int offset)
        {
            return unchecked((int)ReadU32Le(data, offset));
        }

        private static uint ReadU32Le(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
            {
                throw new InvalidDataException("Unexpected end of default.xex while reading PE metadata.");
            }

            return (uint)(data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24));
        }

        private static void WriteU16Be(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value >> 8);
            data[offset + 1] = (byte)value;
        }

        private static bool TryReadCandidate(byte[] data, int offset, out CandidateString candidate)
        {
            candidate = null;
            StringBuilder builder = new StringBuilder();
            int position = offset;

            while (position + 1 < data.Length && builder.Length <= MaxCandidateChars)
            {
                int value = (data[position] << 8) | data[position + 1];
                position += 2;
                if (value == 0)
                {
                    break;
                }

                char ch = (char)value;
                if (!IsLikelyTextChar(ch))
                {
                    return false;
                }

                builder.Append(ch);
            }

            if (builder.Length == 0 || builder.Length > MaxCandidateChars || position + 1 >= data.Length)
            {
                return false;
            }

            int end = position;
            while (end + 1 < data.Length && data[end] == 0 && data[end + 1] == 0)
            {
                end += 2;
                if (end - offset > (MaxCandidateChars + 1) * 2)
                {
                    break;
                }
            }

            candidate = new CandidateString(builder.ToString(), end - offset, end);
            return true;
        }

        private static bool IsLikelyTextChar(char ch)
        {
            if (ch == '\n' || ch == '\r' || ch == '\t')
            {
                return true;
            }

            if (Char.IsSurrogate(ch) || Char.IsControl(ch))
            {
                return false;
            }

            return ch >= 0x20;
        }

        private static bool OverlapsAny(int start, int end, List<PatchedRange> ranges)
        {
            for (int index = 0; index < ranges.Count; index++)
            {
                PatchedRange range = ranges[index];
                if (!(end <= range.Start || start >= range.End))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Sha1Prefix(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }

        private sealed class CandidateString
        {
            public readonly string Text;
            public readonly int SlotBytes;
            public readonly int EndOffset;

            public CandidateString(string text, int slotBytes, int endOffset)
            {
                Text = text;
                SlotBytes = slotBytes;
                EndOffset = endOffset;
            }
        }

        private struct PatchedRange
        {
            public readonly int Start;
            public readonly int End;

            public PatchedRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }
    }
}
