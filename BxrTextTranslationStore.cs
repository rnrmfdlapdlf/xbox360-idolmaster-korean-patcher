using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ImasKoreanPatcher
{
    internal static class BxrTextTranslationStore
    {
        private const string IdPrefix = "BXR-TEXT-";
        private const string HashSalt = "BXR_TEXT_V1\0";

        public static Dictionary<string, BxrTextTranslation> Load(string path)
        {
            Dictionary<string, BxrTextTranslation> translations = new Dictionary<string, BxrTextTranslation>(StringComparer.Ordinal);

            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
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

                    string textId = JsonTranslationStore.TryReadStringProperty(line, "text_id");
                    string koText = JsonTranslationStore.TryReadStringProperty(line, "ko_text") ?? String.Empty;
                    string koTextLesson = JsonTranslationStore.TryReadStringProperty(line, "ko_text_lesson") ?? String.Empty;
                    if (String.IsNullOrEmpty(koText) && String.IsNullOrEmpty(koTextLesson))
                    {
                        continue;
                    }

                    if (String.IsNullOrEmpty(textId) || !textId.StartsWith(IdPrefix, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Invalid BXR text_id at line " + lineNumber.ToString() + ".");
                    }

                    string jpText = JsonTranslationStore.TryReadStringProperty(line, "jp_text");
                    if (!String.IsNullOrEmpty(jpText))
                    {
                        string expectedId = ComputeTextId(jpText);
                        if (!String.Equals(textId, expectedId, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                String.Format(
                                    "Invalid BXR text_id at line {0}: expected {1}.",
                                    lineNumber,
                                    expectedId));
                        }
                    }

                    BxrTextTranslation row = new BxrTextTranslation(koText, koTextLesson);
                    BxrTextTranslation existing;
                    if (translations.TryGetValue(textId, out existing))
                    {
                        if (!existing.HasSameText(row))
                        {
                            throw new InvalidDataException("Conflicting BXR translation for text_id at line " + lineNumber.ToString() + ".");
                        }

                        continue;
                    }

                    translations[textId] = row;
                }
            }

            return translations;
        }

        public static IEnumerable<string> EnumerateTranslationTexts(IEnumerable<BxrTextTranslation> translations)
        {
            foreach (BxrTextTranslation translation in translations)
            {
                if (!String.IsNullOrEmpty(translation.KoText))
                {
                    yield return translation.KoText;
                }

                if (!String.IsNullOrEmpty(translation.KoTextLesson))
                {
                    yield return translation.KoTextLesson;
                }
            }
        }

        internal static string ComputeTextId(string jpText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(HashSalt + jpText);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(IdPrefix.Length + 16);
                builder.Append(IdPrefix);
                for (int index = 0; index < 8; index++)
                {
                    builder.Append(hash[index].ToString("X2"));
                }

                return builder.ToString();
            }
        }
    }

    internal sealed class BxrTextTranslation
    {
        public readonly string KoText;
        public readonly string KoTextLesson;

        public BxrTextTranslation(string koText, string koTextLesson)
        {
            KoText = koText ?? String.Empty;
            KoTextLesson = koTextLesson ?? String.Empty;
        }

        public bool HasSameText(BxrTextTranslation other)
        {
            return other != null
                && String.Equals(KoText, other.KoText, StringComparison.Ordinal)
                && String.Equals(KoTextLesson, other.KoTextLesson, StringComparison.Ordinal);
        }
    }
}
