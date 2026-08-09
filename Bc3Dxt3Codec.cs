using System;
using System.Collections.Generic;

namespace ImasKoreanPatcher
{
    internal static class Bc3Dxt3Codec
    {
        public static byte[] DecodeDxt3Rgba(byte[] data, int offset, int width, int height)
        {
            byte[] rgba = new byte[checked(width * height * 4)];
            byte[] block = new byte[16];
            int source = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    Buffer.BlockCopy(data, source, block, 0, 16);
                    source += 16;
                    Swap16BitWords(block, block.Length);
                    DecodeDxt3Block(block, rgba, width, blockX, blockY);
                }
            }

            return rgba;
        }

        public static void EncodeDxt3Rgba(byte[] data, int offset, int width, int height, byte[] rgba)
        {
            if (rgba == null || rgba.Length != checked(width * height * 4))
            {
                throw new ArgumentException("RGBA data dimensions do not match the texture.", "rgba");
            }

            byte[] block = new byte[16];
            int destination = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    EncodeDxt3Block(block, rgba, width, blockX, blockY);
                    Swap16BitWords(block, block.Length);
                    Buffer.BlockCopy(block, 0, data, destination, block.Length);
                    destination += block.Length;
                }
            }
        }

        public static byte[] DecodeDxt3Alpha(byte[] data, int offset, int width, int height)
        {
            byte[] alpha = new byte[width * height];
            byte[] block = new byte[16];
            int source = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    Buffer.BlockCopy(data, source, block, 0, 16);
                    source += 16;
                    AlphaSwap16(block);

                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int alphaIndex = py * 4 + px;
                            int packed = block[alphaIndex / 2];
                            int nibble = (alphaIndex % 2) == 0 ? (packed & 0x0F) : (packed >> 4);
                            int x = blockX * 4 + px;
                            int y = blockY * 4 + py;
                            alpha[y * width + x] = (byte)(nibble * 17);
                        }
                    }
                }
            }

            return alpha;
        }

        public static void EncodeDxt3Alpha(byte[] data, int offset, int width, int height, byte[] alpha, HashSet<int> dirtyBlocks, bool patchColor)
        {
            byte[] block = new byte[16];
            int destination = offset;
            int blocksWide = width / 4;
            int blocksHigh = height / 4;

            for (int blockY = 0; blockY < blocksHigh; blockY++)
            {
                for (int blockX = 0; blockX < blocksWide; blockX++)
                {
                    int blockId = blockY * blocksWide + blockX;
                    if (dirtyBlocks == null || dirtyBlocks.Contains(blockId))
                    {
                        Buffer.BlockCopy(data, destination, block, 0, 16);
                        AlphaSwap16(block);
                        EncodeDxt3AlphaBlock(block, alpha, width, blockX, blockY);
                        if (patchColor)
                        {
                            WriteOriginalWhiteColorBlock(block);
                        }

                        AlphaSwap16(block);
                        Buffer.BlockCopy(block, 0, data, destination, 16);
                    }

                    destination += 16;
                }
            }
        }

        private static void EncodeDxt3AlphaBlock(byte[] block, byte[] alpha, int width, int blockX, int blockY)
        {
            for (int index = 0; index < 8; index++)
            {
                block[index] = 0;
            }

            for (int py = 0; py < 4; py++)
            {
                for (int px = 0; px < 4; px++)
                {
                    int alphaIndex = py * 4 + px;
                    int x = blockX * 4 + px;
                    int y = blockY * 4 + py;
                    int value = alpha[y * width + x];
                    int nibble = (int)Math.Round(Math.Max(0, Math.Min(255, value)) * 15.0 / 255.0);
                    if ((alphaIndex % 2) == 0)
                    {
                        block[alphaIndex / 2] = (byte)(block[alphaIndex / 2] | nibble);
                    }
                    else
                    {
                        block[alphaIndex / 2] = (byte)(block[alphaIndex / 2] | (nibble << 4));
                    }
                }
            }
        }

        private static void WriteOriginalWhiteColorBlock(byte[] block)
        {
            block[8] = 0xFF;
            block[9] = 0xFF;
            block[10] = 0x00;
            block[11] = 0x00;
            block[12] = 0x00;
            block[13] = 0x00;
            block[14] = 0x00;
            block[15] = 0x00;
        }

        private static void AlphaSwap16(byte[] block)
        {
            Swap16BitWords(block, 8);
        }

        private static void DecodeDxt3Block(byte[] block, byte[] rgba, int width, int blockX, int blockY)
        {
            int[] red = new int[4];
            int[] green = new int[4];
            int[] blue = new int[4];
            ushort color0 = (ushort)(block[8] | (block[9] << 8));
            ushort color1 = (ushort)(block[10] | (block[11] << 8));
            UnpackRgb565(color0, out red[0], out green[0], out blue[0]);
            UnpackRgb565(color1, out red[1], out green[1], out blue[1]);
            red[2] = (2 * red[0] + red[1]) / 3;
            green[2] = (2 * green[0] + green[1]) / 3;
            blue[2] = (2 * blue[0] + blue[1]) / 3;
            red[3] = (red[0] + 2 * red[1]) / 3;
            green[3] = (green[0] + 2 * green[1]) / 3;
            blue[3] = (blue[0] + 2 * blue[1]) / 3;

            uint colorIndices = (uint)(
                block[12] |
                (block[13] << 8) |
                (block[14] << 16) |
                (block[15] << 24));
            for (int pixelY = 0; pixelY < 4; pixelY++)
            {
                for (int pixelX = 0; pixelX < 4; pixelX++)
                {
                    int blockIndex = pixelY * 4 + pixelX;
                    int packedAlpha = block[blockIndex / 2];
                    int alphaNibble = (blockIndex % 2) == 0 ? packedAlpha & 0x0F : packedAlpha >> 4;
                    int colorIndex = (int)((colorIndices >> (blockIndex * 2)) & 0x03);
                    int x = blockX * 4 + pixelX;
                    int y = blockY * 4 + pixelY;
                    int output = (y * width + x) * 4;
                    rgba[output] = (byte)red[colorIndex];
                    rgba[output + 1] = (byte)green[colorIndex];
                    rgba[output + 2] = (byte)blue[colorIndex];
                    rgba[output + 3] = (byte)(alphaNibble * 17);
                }
            }
        }

        private static void EncodeDxt3Block(byte[] block, byte[] rgba, int width, int blockX, int blockY)
        {
            Array.Clear(block, 0, block.Length);
            int minimumRed = 255;
            int minimumGreen = 255;
            int minimumBlue = 255;
            int maximumRed = 0;
            int maximumGreen = 0;
            int maximumBlue = 0;

            for (int pixelY = 0; pixelY < 4; pixelY++)
            {
                for (int pixelX = 0; pixelX < 4; pixelX++)
                {
                    int blockIndex = pixelY * 4 + pixelX;
                    int x = blockX * 4 + pixelX;
                    int y = blockY * 4 + pixelY;
                    int source = (y * width + x) * 4;
                    int alphaNibble = (int)Math.Round(rgba[source + 3] * 15.0 / 255.0);
                    if ((blockIndex % 2) == 0)
                    {
                        block[blockIndex / 2] = (byte)alphaNibble;
                    }
                    else
                    {
                        block[blockIndex / 2] |= (byte)(alphaNibble << 4);
                    }

                    minimumRed = Math.Min(minimumRed, rgba[source]);
                    minimumGreen = Math.Min(minimumGreen, rgba[source + 1]);
                    minimumBlue = Math.Min(minimumBlue, rgba[source + 2]);
                    maximumRed = Math.Max(maximumRed, rgba[source]);
                    maximumGreen = Math.Max(maximumGreen, rgba[source + 1]);
                    maximumBlue = Math.Max(maximumBlue, rgba[source + 2]);
                }
            }

            ushort color0 = PackRgb565(maximumRed, maximumGreen, maximumBlue);
            ushort color1 = PackRgb565(minimumRed, minimumGreen, minimumBlue);
            if (color0 < color1)
            {
                ushort swap = color0;
                color0 = color1;
                color1 = swap;
            }

            block[8] = (byte)(color0 & 0xFF);
            block[9] = (byte)(color0 >> 8);
            block[10] = (byte)(color1 & 0xFF);
            block[11] = (byte)(color1 >> 8);

            int[] red = new int[4];
            int[] green = new int[4];
            int[] blue = new int[4];
            UnpackRgb565(color0, out red[0], out green[0], out blue[0]);
            UnpackRgb565(color1, out red[1], out green[1], out blue[1]);
            red[2] = (2 * red[0] + red[1]) / 3;
            green[2] = (2 * green[0] + green[1]) / 3;
            blue[2] = (2 * blue[0] + blue[1]) / 3;
            red[3] = (red[0] + 2 * red[1]) / 3;
            green[3] = (green[0] + 2 * green[1]) / 3;
            blue[3] = (blue[0] + 2 * blue[1]) / 3;

            uint colorIndices = 0;
            for (int pixelY = 0; pixelY < 4; pixelY++)
            {
                for (int pixelX = 0; pixelX < 4; pixelX++)
                {
                    int blockIndex = pixelY * 4 + pixelX;
                    int x = blockX * 4 + pixelX;
                    int y = blockY * 4 + pixelY;
                    int source = (y * width + x) * 4;
                    int bestIndex = 0;
                    int bestDistance = Int32.MaxValue;
                    for (int colorIndex = 0; colorIndex < 4; colorIndex++)
                    {
                        int redDifference = rgba[source] - red[colorIndex];
                        int greenDifference = rgba[source + 1] - green[colorIndex];
                        int blueDifference = rgba[source + 2] - blue[colorIndex];
                        int distance = redDifference * redDifference + greenDifference * greenDifference + blueDifference * blueDifference;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestIndex = colorIndex;
                        }
                    }

                    colorIndices |= (uint)(bestIndex << (blockIndex * 2));
                }
            }

            block[12] = (byte)(colorIndices & 0xFF);
            block[13] = (byte)((colorIndices >> 8) & 0xFF);
            block[14] = (byte)((colorIndices >> 16) & 0xFF);
            block[15] = (byte)((colorIndices >> 24) & 0xFF);
        }

        private static ushort PackRgb565(int red, int green, int blue)
        {
            return (ushort)(
                ((red * 31 + 127) / 255 << 11) |
                ((green * 63 + 127) / 255 << 5) |
                ((blue * 31 + 127) / 255));
        }

        private static void UnpackRgb565(ushort color, out int red, out int green, out int blue)
        {
            int red5 = (color >> 11) & 0x1F;
            int green6 = (color >> 5) & 0x3F;
            int blue5 = color & 0x1F;
            red = (red5 << 3) | (red5 >> 2);
            green = (green6 << 2) | (green6 >> 4);
            blue = (blue5 << 3) | (blue5 >> 2);
        }

        private static void Swap16BitWords(byte[] block, int length)
        {
            for (int index = 0; index < length; index += 2)
            {
                byte temp = block[index];
                block[index] = block[index + 1];
                block[index + 1] = temp;
            }
        }
    }
}
