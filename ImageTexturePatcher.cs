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
            HashSet<string> targetKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    string category = JsonTranslationStore.TryReadStringProperty(line, "category");
                    string target = JsonTranslationStore.TryReadStringProperty(line, "target");
                    if (String.IsNullOrEmpty(bna) || String.IsNullOrEmpty(entry) || String.IsNullOrEmpty(asset))
                    {
                        throw new InvalidDataException("Invalid image texture manifest row at line " + lineNumber.ToString() + ".");
                    }

                    if (String.IsNullOrEmpty(category))
                    {
                        category = "legacy";
                    }
                    if (String.IsNullOrEmpty(target))
                    {
                        target = "bna";
                    }
                    if (!String.Equals(target, "bna", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "Unsupported image target '" + target + "' at manifest line " + lineNumber.ToString() + ".");
                    }

                    string normalizedBna = NormalizePath(bna);
                    string normalizedEntry = NormalizePath(entry);
                    string targetKey = normalizedBna + "|" + normalizedEntry;
                    if (!targetKeys.Add(targetKey))
                    {
                        throw new InvalidDataException(
                            "Duplicate BNA/NUT image target at manifest line " + lineNumber.ToString() + ": " +
                            normalizedBna + " :: " + normalizedEntry);
                    }
                    ValidateAssetName(asset, lineNumber);
                    rows.Add(new ImageTexturePatchEntry(
                        id,
                        category,
                        normalizedBna,
                        normalizedEntry,
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

            Dictionary<string, ImageBnaPatchGroup> byBna = new Dictionary<string, ImageBnaPatchGroup>(StringComparer.OrdinalIgnoreCase);
            List<ImageBnaPatchGroup> groups = new List<ImageBnaPatchGroup>();
            HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.Count; index++)
            {
                ImageTexturePatchEntry entry = entries[index];
                ImageBnaPatchGroup group;
                if (!byBna.TryGetValue(entry.BnaPath, out group))
                {
                    group = new ImageBnaPatchGroup(entry.BnaPath);
                    byBna[entry.BnaPath] = group;
                    groups.Add(group);
                }

                group.Add(entry);
                categories.Add(entry.Category);
            }
            result.CategoriesSeen = categories.Count;

            int bnaIndex = 0;
            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                ImageBnaPatchGroup group = groups[groupIndex];
                if (progress != null)
                {
                    int percent = 73 + (int)(2.0 * bnaIndex / Math.Max(1, groups.Count));
                    progress(
                        percent,
                        String.Format(
                            "이미지 패치 중... {0:N0}/{1:N0} [{2}]",
                            bnaIndex + 1,
                            groups.Count,
                            String.Join(", ", group.Categories.ToArray())));
                }

                bnaIndex++;
                try
                {
                    PatchBnaFile(extractedRoot, group.BnaPath, group.Rows, result);
                }
                catch (Exception exception)
                {
                    throw new InvalidDataException(
                        "Image patch failed for BNA '" + group.BnaPath + "' (categories: " +
                        String.Join(", ", group.Categories.ToArray()) + ").",
                        exception);
                }
            }

            if (progress != null)
            {
                progress(
                    75,
                    String.Format(
                        "이미지 패치 완료: {0:N0}개 분류, {1:N0}개 교체, {2:N0}개 추가",
                        result.CategoriesSeen,
                        result.EntriesPatched,
                        result.EntriesAdded));
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
                string assetPath = ResolveAssetPath(patch.AssetName);
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

        private static void ValidateAssetName(string assetName, int lineNumber)
        {
            string normalized = NormalizePath(assetName);
            if (String.IsNullOrEmpty(normalized) || Path.IsPathRooted(assetName) || normalized.IndexOf(':') >= 0)
            {
                throw new InvalidDataException("Invalid image asset path at manifest line " + lineNumber.ToString() + ".");
            }

            string[] segments = normalized.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == ".." || segments[index] == ".")
                {
                    throw new InvalidDataException("Unsafe image asset path at manifest line " + lineNumber.ToString() + ".");
                }
            }
        }

        private string ResolveAssetPath(string assetName)
        {
            string root = Path.GetFullPath(imageAssetRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string relative = NormalizePath(assetName).Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(root, relative));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Image asset path escapes the asset root: " + assetName);
            }

            return fullPath;
        }

        private sealed class ImageBnaPatchGroup
        {
            private readonly HashSet<string> categorySet;

            public readonly string BnaPath;
            public readonly List<ImageTexturePatchEntry> Rows;
            public readonly List<string> Categories;

            public ImageBnaPatchGroup(string bnaPath)
            {
                BnaPath = bnaPath;
                Rows = new List<ImageTexturePatchEntry>();
                Categories = new List<string>();
                categorySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public void Add(ImageTexturePatchEntry entry)
            {
                Rows.Add(entry);
                if (categorySet.Add(entry.Category))
                {
                    Categories.Add(entry.Category);
                }
            }
        }

        private sealed class ImageTexturePatchEntry
        {
            public readonly string Id;
            public readonly string Category;
            public readonly string BnaPath;
            public readonly string EntryPath;
            public readonly string AssetName;
            public readonly bool AllowAdd;

            public ImageTexturePatchEntry(string id, string category, string bnaPath, string entryPath, string assetName, bool allowAdd)
            {
                Id = id;
                Category = category;
                BnaPath = bnaPath;
                EntryPath = entryPath;
                AssetName = assetName;
                AllowAdd = allowAdd;
            }
        }
    }
}
