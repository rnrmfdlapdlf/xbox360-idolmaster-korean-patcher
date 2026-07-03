using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImasKoreanPatcher
{
    internal sealed class ImageTexturePatcher
    {
        private readonly List<ImageTexturePatchEntry> entries;
        private readonly string imageAssetRoot;

        private ImageTexturePatcher(List<ImageTexturePatchEntry> entries, string imageAssetRoot)
        {
            this.entries = entries;
            this.imageAssetRoot = imageAssetRoot;
        }

        public static ImageTexturePatcher Load(string assetRoot)
        {
            string imageRoot = Path.Combine(assetRoot, "ImageTextures");
            string manifestPath = Path.Combine(imageRoot, "image_texture_manifest.jsonl");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException("Image texture manifest was not found.", manifestPath);
            }

            List<ImageTexturePatchEntry> rows = new List<ImageTexturePatchEntry>();
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

                    string id = JsonTranslationStore.TryReadStringProperty(line, "id");
                    string bna = JsonTranslationStore.TryReadStringProperty(line, "bna");
                    string entry = JsonTranslationStore.TryReadStringProperty(line, "entry");
                    string asset = JsonTranslationStore.TryReadStringProperty(line, "asset");
                    string allowAdd = JsonTranslationStore.TryReadStringProperty(line, "allow_add");
                    if (String.IsNullOrEmpty(bna) || String.IsNullOrEmpty(entry) || String.IsNullOrEmpty(asset))
                    {
                        throw new InvalidDataException("Invalid image texture manifest row at line " + lineNumber.ToString() + ".");
                    }

                    rows.Add(new ImageTexturePatchEntry(
                        id,
                        NormalizePath(bna),
                        NormalizePath(entry),
                        asset,
                        String.Equals(allowAdd, "true", StringComparison.OrdinalIgnoreCase)));
                }
            }

            return new ImageTexturePatcher(rows, imageRoot);
        }

        public ImageTexturePatchResult PatchExtractedRoot(string extractedRoot, Action<int, string> progress)
        {
            ImageTexturePatchResult result = new ImageTexturePatchResult();
            result.ManifestRows = entries.Count;
            if (entries.Count == 0)
            {
                return result;
            }

            Dictionary<string, List<ImageTexturePatchEntry>> byBna = new Dictionary<string, List<ImageTexturePatchEntry>>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.Count; index++)
            {
                ImageTexturePatchEntry entry = entries[index];
                List<ImageTexturePatchEntry> rows;
                if (!byBna.TryGetValue(entry.BnaPath, out rows))
                {
                    rows = new List<ImageTexturePatchEntry>();
                    byBna[entry.BnaPath] = rows;
                }

                rows.Add(entry);
            }

            int bnaIndex = 0;
            foreach (KeyValuePair<string, List<ImageTexturePatchEntry>> pair in byBna)
            {
                if (progress != null)
                {
                    int percent = 73 + (int)(2.0 * bnaIndex / Math.Max(1, byBna.Count));
                    progress(percent, String.Format("이미지 패치 중... {0:N0}/{1:N0}", bnaIndex + 1, byBna.Count));
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
                progress(75, String.Format("이미지 패치 완료: {0:N0}개 교체, {1:N0}개 추가", result.EntriesPatched, result.EntriesAdded));
            }

            return result;
        }

        private void PatchBnaFile(string extractedRoot, string bnaRelativePath, List<ImageTexturePatchEntry> rows, ImageTexturePatchResult result)
        {
            string bnaPath = Path.Combine(extractedRoot, bnaRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(bnaPath))
            {
                result.MissingBnaFiles++;
                return;
            }

            byte[] original = File.ReadAllBytes(bnaPath);
            if (!BnaContainer.IsBna(original))
            {
                result.Errors++;
                return;
            }

            BnaContainer bna = BnaContainer.Parse(original);
            bool changed = false;
            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                ImageTexturePatchEntry patch = rows[rowIndex];
                string assetPath = Path.Combine(imageAssetRoot, patch.AssetName);
                if (!File.Exists(assetPath))
                {
                    result.MissingAssets++;
                    continue;
                }

                byte[] assetData = File.ReadAllBytes(assetPath);
                BnaContainerEntry entry = FindEntry(bna.Entries, patch.EntryPath);
                if (entry == null)
                {
                    if (!patch.AllowAdd)
                    {
                        result.MissingEntries++;
                        continue;
                    }

                    entry = CreateEntry(bna.Entries.Count, patch.EntryPath, assetData);
                    if (entry == null)
                    {
                        result.MissingEntries++;
                        continue;
                    }

                    bna.Entries.Add(entry);
                    result.EntriesAdded++;
                    changed = true;
                    continue;
                }

                entry.Data = assetData;
                result.EntriesPatched++;
                changed = true;
            }

            if (changed)
            {
                File.WriteAllBytes(bnaPath, bna.Rebuild());
                result.BnaFilesPatched++;
            }
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

        private static BnaContainerEntry CreateEntry(int index, string entryPath, byte[] data)
        {
            string normalized = NormalizePath(entryPath);
            int slashIndex = normalized.LastIndexOf('/');
            if (slashIndex < 0 || slashIndex >= normalized.Length - 1)
            {
                return null;
            }

            string directoryName = normalized.Substring(0, slashIndex);
            string fileName = normalized.Substring(slashIndex + 1);
            if (directoryName.Length == 0 || fileName.Length == 0)
            {
                return null;
            }

            return new BnaContainerEntry(index, directoryName, fileName, normalized, data);
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private sealed class ImageTexturePatchEntry
        {
            public readonly string Id;
            public readonly string BnaPath;
            public readonly string EntryPath;
            public readonly string AssetName;
            public readonly bool AllowAdd;

            public ImageTexturePatchEntry(string id, string bnaPath, string entryPath, string assetName, bool allowAdd)
            {
                Id = id;
                BnaPath = bnaPath;
                EntryPath = entryPath;
                AssetName = assetName;
                AllowAdd = allowAdd;
            }
        }
    }
}
