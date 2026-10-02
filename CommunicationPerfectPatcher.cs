using System;
using System.IO;

namespace ImasKoreanPatcher
{
    internal static class CommunicationPerfectPatcher
    {
        private const int ResultBranchOffset = 12;
        private static readonly byte[] Nop = new byte[] { 0x60, 0x00, 0x00, 0x00 };

        // Both the original game and TU classify the final communication result here.
        // Bypass the below-threshold branch so the existing Perfect (3) path stores
        // the result before the game displays it and processes its rewards.
        private static readonly byte[] OriginalResultPattern = new byte[]
        {
            0x39, 0x60, 0x00, 0x01, 0x7F, 0x1D, 0x18, 0x00,
            0x91, 0x7E, 0x0A, 0xD8, 0x41, 0x98, 0x00, 0x0C,
            0x39, 0x60, 0x00, 0x03, 0x48, 0x00, 0x00, 0x28,
            0x2F, 0x1D, 0x00, 0x00, 0x41, 0x99, 0x00, 0x0C,
            0x39, 0x60, 0x00, 0x00, 0x48, 0x00, 0x00, 0x18,
            0x39, 0x63, 0xFF, 0xFF, 0x7D, 0x7D, 0x58, 0x50,
            0x7D, 0x6B, 0x00, 0x34, 0x55, 0x6B, 0xDF, 0xFE,
            0x39, 0x6B, 0x00, 0x01
        };
        private static readonly byte[] PerfectResultPattern = BuildPerfectResultPattern();
        private static readonly byte[] OriginalResultStore = new byte[]
        {
            0x91, 0x7E, 0x0A, 0xD8
        };
        private static readonly byte[] TitleUpdateResultStore = new byte[]
        {
            0x89, 0x5E, 0x00, 0xA9, 0x91, 0x7E, 0x0A, 0xD8,
            0x2B, 0x0A, 0x00, 0x00, 0x40, 0x9A, 0x00, 0x6C
        };

        public static CommunicationPerfectPatchResult Patch(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            int foundOffset = -1;
            bool alreadyPatched = false;
            for (int offset = 0; offset <= data.Length - OriginalResultPattern.Length; offset++)
            {
                bool original = MatchesPattern(data, offset, OriginalResultPattern);
                bool patched = MatchesPattern(data, offset, PerfectResultPattern);
                if (!original && !patched)
                {
                    continue;
                }

                if (foundOffset >= 0)
                {
                    throw new InvalidDataException("영업 결과 퍼펙트 판정 코드가 여러 곳에서 발견되었습니다. 패치를 중단합니다.");
                }

                foundOffset = offset;
                alreadyPatched = patched;
            }

            if (foundOffset < 0)
            {
                throw new InvalidDataException("default.xex에서 영업 결과 퍼펙트 판정 코드를 찾을 수 없습니다.");
            }

            int storeOffset = foundOffset + OriginalResultPattern.Length;
            if ((foundOffset & 3) != 0 ||
                (!MatchesPattern(data, storeOffset, OriginalResultStore) &&
                 !MatchesPattern(data, storeOffset, TitleUpdateResultStore)))
            {
                throw new InvalidDataException("영업 결과 퍼펙트 판정 코드의 저장 위치가 예상과 다릅니다. 패치를 중단합니다.");
            }

            CommunicationPerfectPatchResult result = new CommunicationPerfectPatchResult();
            if (alreadyPatched)
            {
                result.ResultBranchesAlreadyPatched = 1;
            }
            else
            {
                Buffer.BlockCopy(Nop, 0, data, foundOffset + ResultBranchOffset, Nop.Length);
                result.ResultBranchesPatched = 1;
            }

            return result;
        }

        private static byte[] BuildPerfectResultPattern()
        {
            byte[] pattern = (byte[])OriginalResultPattern.Clone();
            Buffer.BlockCopy(Nop, 0, pattern, ResultBranchOffset, Nop.Length);
            return pattern;
        }

        private static bool MatchesPattern(byte[] data, int offset, byte[] pattern)
        {
            if (offset < 0 || offset > data.Length - pattern.Length)
            {
                return false;
            }

            for (int index = 0; index < pattern.Length; index++)
            {
                if (data[offset + index] != pattern[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
