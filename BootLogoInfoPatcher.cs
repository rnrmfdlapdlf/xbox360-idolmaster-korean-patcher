using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ImasKoreanPatcher
{
    internal static class BootLogoInfoPatcher
    {
        private const string BootBnaRelativePath = "root/scene/boot/boot.bna";
        private const string ProjectLogoEntryPath = "root/scene/boot/common/project_logo.nut";
        private const string NtscLogoEntryPath = "root/scene/boot/common/swg_logo_pro_imas_ntsc.nut";
        private const string FontRelativePath = "Fonts/title_Medium.ttf";
        private const string CreditText = "by Gideon";
        private const int ProjectLogoCanvasWidth = 816;
        private const int ProjectLogoBaseHeight = 248;
        private const int ProjectLogoCanvasHeight = 512;
        private const int ProjectLogoContentTop = 64;
        private const float ProjectTextTop = 456.0f;
        private const float ProjectMainFontSize = 20.0f;
        private const float ProjectCreditFontSize = 17.0f;
        private const float ProjectLineGap = 4.0f;
        private const float ProjectBottomMargin = 8.0f;
        private const float ScreenMainFontSize = 16.0f;
        private const float ScreenCreditFontSize = 14.0f;
        private const float PreferredBlockTop = 400.0f;
        private const float BottomMargin = 30.0f;
        private const float ScreenLineGap = 3.0f;
        private const float ScreenClearPaddingX = 18.0f;
        private const float ScreenClearPaddingY = 6.0f;
        private static readonly Color PatchTextColor = Color.FromArgb(0x22, 0x22, 0x22);

        public static BootLogoInfoPatchResult PatchExtractedRoot(string extractedRoot, string assetRoot, Action<int, string> progress)
        {
            BootLogoInfoPatchResult result = new BootLogoInfoPatchResult();
            result.VersionText = BuildVersionText();

            Report(progress, 76, "리소스 처리 중...");

            string bnaPath = Path.Combine(extractedRoot, BootBnaRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(bnaPath))
            {
                result.TargetBnaFound = false;
                return result;
            }

            result.TargetBnaFound = true;
            BnaContainer bna = BnaContainer.Parse(File.ReadAllBytes(bnaPath));
            string fontPath = Path.Combine(assetRoot, FontRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fontPath))
            {
                throw new FileNotFoundException("Required font was not found.", fontPath);
            }

            bool changed = false;
            changed |= PatchEntry(bna, ProjectLogoEntryPath, fontPath, result, BootLogoPatchMode.ProjectLogoCanvas);
            changed |= PatchEntry(bna, NtscLogoEntryPath, fontPath, result, BootLogoPatchMode.NtscScreen);

            if (result.TargetEntriesSeen == 0)
            {
                result.TargetEntryFound = false;
                return result;
            }

            result.TargetEntryFound = true;
            if (changed)
            {
                File.WriteAllBytes(bnaPath, bna.Rebuild());
            }

            if (result.EntriesPatched == 0 && result.EntriesAlreadyPatched > 0)
            {
                result.AlreadyPatched = true;
                Report(progress, 77, "리소스 이미 적용됨");
            }
            else
            {
                Report(progress, 77, "리소스 처리 완료");
            }

            return result;
        }

        private static bool PatchEntry(BnaContainer bna, string entryPath, string fontPath, BootLogoInfoPatchResult result, BootLogoPatchMode mode)
        {
            BnaContainerEntry entry = FindEntry(bna.Entries, entryPath);
            if (entry == null)
            {
                return false;
            }

            result.TargetEntriesSeen++;
            byte[] patchedNut = PatchNut(entry.Data, result.VersionText, CreditText, fontPath, mode);
            if (BytesEqual(entry.Data, patchedNut))
            {
                result.EntriesAlreadyPatched++;
                return false;
            }

            entry.Data = patchedNut;
            result.Changed = true;
            result.EntriesPatched++;
            return true;
        }

        private static BnaContainerEntry FindEntry(IList<BnaContainerEntry> entries, string entryPath)
        {
            string normalized = NormalizePath(entryPath);
            for (int index = 0; index < entries.Count; index++)
            {
                if (String.Equals(entries[index].Path, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return entries[index];
                }
            }

            return null;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').Trim('/');
        }

        private static byte[] PatchNut(byte[] nutBytes, string versionText, string creditText, string fontPath, BootLogoPatchMode mode)
        {
            if (ReadU16(nutBytes, 0x22) != 19)
            {
                throw new InvalidDataException("Unsupported texture pixel type.");
            }

            NutTexture texture = NutTexture.Parse(nutBytes);
            NutTexturePage page = texture.GetPage(0);
            if (page == null)
            {
                throw new InvalidDataException("Required texture page was not found.");
            }

            using (Bitmap bitmap = ReadArgb32Page(nutBytes, page))
            {
                if (mode == BootLogoPatchMode.ProjectLogoCanvas)
                {
                    using (Bitmap canvas = CreateProjectLogoCanvas(bitmap))
                    {
                        DrawProjectLogoPatchInfo(canvas, versionText, creditText, fontPath);
                        return BuildArgb32Nut(nutBytes, page.DataOffset, canvas);
                    }
                }

                byte[] output = new byte[nutBytes.Length];
                Buffer.BlockCopy(nutBytes, 0, output, 0, nutBytes.Length);
                DrawScreenPatchInfo(bitmap, versionText, creditText, fontPath);
                WriteArgb32Page(output, page, bitmap);
                return output;
            }
        }

        private static Bitmap CreateProjectLogoCanvas(Bitmap source)
        {
            Bitmap canvas = new Bitmap(ProjectLogoCanvasWidth, ProjectLogoCanvasHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.Clear(Color.White);
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                int copyWidth = Math.Min(source.Width, ProjectLogoCanvasWidth);
                int sourceTop = GetProjectLogoSourceTop(source);
                int copyHeight = Math.Min(source.Height - sourceTop, ProjectLogoBaseHeight);
                if (copyWidth > 0 && copyHeight > 0)
                {
                    Rectangle destination = new Rectangle(0, ProjectLogoContentTop, copyWidth, copyHeight);
                    Rectangle sourceRect = new Rectangle(0, sourceTop, copyWidth, copyHeight);
                    graphics.DrawImage(source, destination, sourceRect, GraphicsUnit.Pixel);
                }
            }

            return canvas;
        }

        private static int GetProjectLogoSourceTop(Bitmap source)
        {
            if (source.Height <= ProjectLogoBaseHeight)
            {
                return 0;
            }

            int searchBottom = Math.Min(source.Height, ProjectLogoCanvasHeight - 120);
            int threshold = Math.Max(8, source.Width / 120);
            for (int y = 0; y < searchBottom; y++)
            {
                int darkPixels = 0;
                for (int x = 0; x < source.Width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    if (pixel.A != 0 && (pixel.R < 245 || pixel.G < 245 || pixel.B < 245))
                    {
                        darkPixels++;
                    }
                }

                if (darkPixels >= threshold)
                {
                    return y >= ProjectLogoContentTop / 2
                        ? Math.Min(ProjectLogoContentTop, source.Height - ProjectLogoBaseHeight)
                        : 0;
                }
            }

            return 0;
        }

        private static void DrawProjectLogoPatchInfo(Bitmap bitmap, string versionText, string creditText, string fontPath)
        {
            using (PrivateFontCollection fonts = LoadFont(fontPath))
            using (Font mainFont = CreateFont(fonts, ProjectMainFontSize))
            using (Font creditFont = CreateFont(fonts, ProjectCreditFontSize))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    SizeF mainSize = graphics.MeasureString(versionText, mainFont);
                    SizeF creditSize = graphics.MeasureString(creditText, creditFont);
                    float blockHeight = mainSize.Height + ProjectLineGap + creditSize.Height;
                    float blockWidth = Math.Max(mainSize.Width, creditSize.Width);
                    float top = Math.Min(ProjectTextTop, bitmap.Height - blockHeight - ProjectBottomMargin);
                    if (top < 0.0f)
                    {
                        top = 0.0f;
                    }

                    ClearTextArea(graphics, bitmap.Width, bitmap.Height, top, blockWidth, blockHeight);
                    DrawTextBlock(graphics, bitmap.Width, top, versionText, creditText, mainFont, creditFont, ProjectLineGap);
                }
            }
        }

        private static void DrawScreenPatchInfo(Bitmap bitmap, string versionText, string creditText, string fontPath)
        {
            using (PrivateFontCollection fonts = LoadFont(fontPath))
            using (Font mainFont = CreateFont(fonts, ScreenMainFontSize))
            using (Font creditFont = CreateFont(fonts, ScreenCreditFontSize))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    SizeF mainSize = graphics.MeasureString(versionText, mainFont);
                    SizeF creditSize = graphics.MeasureString(creditText, creditFont);
                    float blockHeight = mainSize.Height + ScreenLineGap + creditSize.Height;
                    float blockWidth = Math.Max(mainSize.Width, creditSize.Width);
                    float top = Math.Min(PreferredBlockTop, bitmap.Height - blockHeight - BottomMargin);
                    if (top < 0.0f)
                    {
                        top = 0.0f;
                    }

                    ClearTextArea(graphics, bitmap.Width, bitmap.Height, top, blockWidth, blockHeight);
                    DrawTextBlock(graphics, bitmap.Width, top, versionText, creditText, mainFont, creditFont, ScreenLineGap);
                }
            }
        }

        private static void ClearTextArea(Graphics graphics, int width, int height, float top, float blockWidth, float blockHeight)
        {
            float left = (width - blockWidth) / 2.0f - ScreenClearPaddingX;
            float clearTop = top - ScreenClearPaddingY;
            float clearWidth = blockWidth + ScreenClearPaddingX * 2.0f;
            float clearHeight = blockHeight + ScreenClearPaddingY * 2.0f;
            RectangleF rect = RectangleF.Intersect(
                new RectangleF(left, clearTop, clearWidth, clearHeight),
                new RectangleF(0.0f, 0.0f, width, height));
            graphics.FillRectangle(Brushes.White, rect);
        }

        private static void DrawTextBlock(Graphics graphics, int width, float top, string versionText, string creditText, Font mainFont, Font creditFont, float lineGap)
        {
            using (Brush brush = new SolidBrush(PatchTextColor))
            using (StringFormat format = new StringFormat())
            {
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;

                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Near;
                format.FormatFlags |= StringFormatFlags.NoClip;

                SizeF mainSize = graphics.MeasureString(versionText, mainFont);
                SizeF creditSize = graphics.MeasureString(creditText, creditFont);
                RectangleF mainRect = new RectangleF(0.0f, top, width, mainSize.Height + 2.0f);
                RectangleF creditRect = new RectangleF(0.0f, top + mainSize.Height + lineGap, width, creditSize.Height + 2.0f);
                graphics.DrawString(versionText, mainFont, brush, mainRect, format);
                graphics.DrawString(creditText, creditFont, brush, creditRect, format);
            }
        }

        private static Bitmap ReadArgb32Page(byte[] data, NutTexturePage page)
        {
            Bitmap bitmap = new Bitmap(page.Width, page.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            int source = page.DataOffset;
            for (int y = 0; y < page.Height; y++)
            {
                for (int x = 0; x < page.Width; x++)
                {
                    byte a = data[source];
                    byte r = data[source + 1];
                    byte g = data[source + 2];
                    byte b = data[source + 3];
                    bitmap.SetPixel(x, y, Color.FromArgb(a, r, g, b));
                    source += 4;
                }
            }

            return bitmap;
        }

        private static void WriteArgb32Page(byte[] data, NutTexturePage page, Bitmap bitmap)
        {
            if (bitmap.Width != page.Width || bitmap.Height != page.Height)
            {
                throw new InvalidDataException("Texture dimensions changed unexpectedly.");
            }

            int destination = page.DataOffset;
            for (int y = 0; y < page.Height; y++)
            {
                for (int x = 0; x < page.Width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    data[destination] = pixel.A;
                    data[destination + 1] = pixel.R;
                    data[destination + 2] = pixel.G;
                    data[destination + 3] = pixel.B;
                    destination += 4;
                }
            }
        }

        private static byte[] BuildArgb32Nut(byte[] sourceNut, int dataOffset, Bitmap bitmap)
        {
            int dataBytes = checked(bitmap.Width * bitmap.Height * 4);
            byte[] output = new byte[checked(dataOffset + dataBytes)];
            Buffer.BlockCopy(sourceNut, 0, output, 0, dataOffset);
            WriteU32(output, 0x10, checked((uint)(dataBytes + dataOffset - 0x10)));
            WriteU32(output, 0x18, checked((uint)dataBytes));
            WriteU16(output, 0x24, bitmap.Width);
            WriteU16(output, 0x26, bitmap.Height);

            NutTexturePage outputPage = new NutTexturePage(0, dataOffset, dataBytes, bitmap.Width, bitmap.Height);
            WriteArgb32Page(output, outputPage, bitmap);
            return output;
        }

        private static PrivateFontCollection LoadFont(string fontPath)
        {
            PrivateFontCollection collection = new PrivateFontCollection();
            collection.AddFontFile(fontPath);
            if (collection.Families.Length == 0)
            {
                collection.Dispose();
                throw new InvalidOperationException("Required font did not expose any font families.");
            }

            return collection;
        }

        private static Font CreateFont(PrivateFontCollection fonts, float size)
        {
            FontFamily family = fonts.Families[0];
            FontStyle style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
            return new Font(family, size, style, GraphicsUnit.Pixel);
        }

        private static string BuildVersionText()
        {
            DateTime buildDate = DateTime.UtcNow;
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                if (File.Exists(assemblyPath))
                {
                    buildDate = ReadBuildDateUtc(assemblyPath);
                }
            }
            catch
            {
            }

            return "한글패치 v" + buildDate.ToString("yyMMdd", CultureInfo.InvariantCulture);
        }

        private static DateTime ReadBuildDateUtc(string assemblyPath)
        {
            DateTime fallback = File.GetLastWriteTimeUtc(assemblyPath);
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length < 0x40)
                {
                    return fallback;
                }

                stream.Position = 0x3C;
                int peHeaderOffset = reader.ReadInt32();
                if (peHeaderOffset < 0 || peHeaderOffset + 12 > stream.Length)
                {
                    return fallback;
                }

                stream.Position = peHeaderOffset;
                if (reader.ReadUInt32() != 0x00004550U)
                {
                    return fallback;
                }

                stream.Position = peHeaderOffset + 8;
                uint seconds = reader.ReadUInt32();
                if (seconds == 0)
                {
                    return fallback;
                }

                return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            }
        }

        private static int ReadU16(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static void WriteU16(byte[] data, int offset, int value)
        {
            data[offset] = (byte)((value >> 8) & 0xFF);
            data[offset + 1] = (byte)(value & 0xFF);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)((value >> 24) & 0xFF);
            data[offset + 1] = (byte)((value >> 16) & 0xFF);
            data[offset + 2] = (byte)((value >> 8) & 0xFF);
            data[offset + 3] = (byte)(value & 0xFF);
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
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

        private static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null)
            {
                progress(percent, message);
            }
        }
    }

    internal enum BootLogoPatchMode
    {
        ProjectLogoCanvas,
        NtscScreen
    }

    internal sealed class BootLogoInfoPatchResult
    {
        public bool TargetBnaFound;
        public bool TargetEntryFound;
        public bool Changed;
        public bool AlreadyPatched;
        public int TargetEntriesSeen;
        public int EntriesPatched;
        public int EntriesAlreadyPatched;
        public string VersionText;
    }
}
