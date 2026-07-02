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

        public static Dictionary<string, string> Load(string path)
        {
            Dictionary<string, string> translations = new Dictionary<string, string>(StringComparer.Ordinal);

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
                    string jpText = JsonTranslationStore.TryReadStringProperty(line, "jp_text");
                    string koText = JsonTranslationStore.TryReadStringProperty(line, "ko_text");
                    if (String.IsNullOrEmpty(jpText) || String.IsNullOrEmpty(koText))
                    {
                        continue;
                    }

                    string expectedId = ComputeTextId(jpText);
                    if (!String.Equals(textId, expectedId, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            String.Format(
                                "Invalid BXR text_id at line {0}: expected {1}.",
                                lineNumber,
                                expectedId));
                    }

                    string existing;
                    if (translations.TryGetValue(jpText, out existing))
                    {
                        if (!String.Equals(existing, koText, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("Conflicting BXR translation for source text at line " + lineNumber.ToString() + ".");
                        }

                        continue;
                    }

                    translations[jpText] = koText;
                }
            }

            return translations;
        }

        private static string ComputeTextId(string jpText)
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
}
