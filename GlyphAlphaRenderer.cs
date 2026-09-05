using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ImasKoreanPatcher
{
    internal sealed class GlyphAlphaRenderer : IDisposable
    {
        private readonly PrivateFontCollection fontCollection;
        private readonly FontFamily fontFamily;
        private readonly float fontSize;

        public GlyphAlphaRenderer(string fontPath, float fontSize)
        {
            fontCollection = new PrivateFontCollection();
            fontCollection.AddFontFile(fontPath);
            if (fontCollection.Families.Length == 0)
            {
                throw new InvalidOperationException("Font file did not expose any font families.");
            }

            fontFamily = fontCollection.Families[0];
            this.fontSize = fontSize;
        }

        public byte[] RenderCellAtBaseline(char ch, int width, int height, int xAdjust, int baselineY)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
                format.FormatFlags |= StringFormatFlags.NoClip;
                FontStyle style = fontFamily.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold;
                path.AddString(ch.ToString(), fontFamily, (int)style, fontSize, new PointF(0, 0), format);

                RectangleF bounds = path.GetBounds();
                Matrix matrix = new Matrix();
                float x = (float)Math.Floor((width - bounds.Width) / 2.0f) - bounds.Left + xAdjust;
                float emHeight = fontFamily.GetEmHeight(style);
                float ascent = fontFamily.GetCellAscent(style);
                float baselineFromOrigin = fontSize * ascent / emHeight;
                float y = baselineY - baselineFromOrigin;
                matrix.Translate(x, y);
                path.Transform(matrix);

                using (Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    graphics.FillPath(Brushes.White, path);
                    return BitmapToAlpha(bitmap);
                }
            }
        }

        public static byte[] RenderCenteredTilde(int width, int height)
        {
            const int scale = 8;
            int scaledWidth = width * scale;
            int scaledHeight = height * scale;

            using (Bitmap large = new Bitmap(scaledWidth, scaledHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(large))
            using (Pen pen = new Pen(Color.White, 2.2f * scale))
            {
                graphics.Clear(Color.Transparent);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                float x0 = 1.5f;
                float x1 = Math.Min(width - 2.0f, 17.0f);
                float centerY = height * 0.40f;
                const float amplitude = 2.8f;
                int steps = Math.Max(12, (int)((x1 - x0) * 2.0f));
                PointF[] points = new PointF[steps + 1];
                for (int step = 0; step <= steps; step++)
                {
                    double t = (double)step / steps;
                    float x = x0 + (x1 - x0) * (float)t;
                    float y = centerY + (float)Math.Sin(t * Math.PI * 2.0) * amplitude;
                    points[step] = new PointF(x * scale, y * scale);
                }

                graphics.DrawLines(pen, points);

                using (Bitmap cell = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Graphics cellGraphics = Graphics.FromImage(cell))
                {
                    cellGraphics.Clear(Color.Transparent);
                    cellGraphics.CompositingMode = CompositingMode.SourceCopy;
                    cellGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    cellGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    cellGraphics.DrawImage(large, new Rectangle(0, 0, width, height));
                    return BitmapToAlpha(cell);
                }
            }
        }

        public void Dispose()
        {
            fontCollection.Dispose();
        }

        private static byte[] BitmapToAlpha(Bitmap bitmap)
        {
            byte[] alpha = new byte[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    alpha[y * bitmap.Width + x] = bitmap.GetPixel(x, y).A;
                }
            }

            return alpha;
        }
    }
}
