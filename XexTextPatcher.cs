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
                    result.ReplacementsTooLong++;
                    continue;
                }

                Array.Clear(data, offset, candidate.SlotBytes);
                Buffer.BlockCopy(encoded, 0, data, offset, encoded.Length);
                patchedRanges.Add(new PatchedRange(offset, offset + candidate.SlotBytes));
                result.StringsPatched++;
            }
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
