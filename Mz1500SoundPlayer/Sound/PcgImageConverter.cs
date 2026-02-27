using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Mz1500SoundPlayer.Sound;

public static class PcgImageConverter
{
    /// <summary>
    /// Loads an image from the given path, resizes it to 320x200, applies Floyd-Steinberg
    /// dithering to convert to MZ-1500's 8-color digital RGB palette,
    /// and generates 24,000 bytes of PCG data.
    ///
    /// Output order (matching the Z80 loader OUT 0xE5 bank sequence):
    ///   Green plane (Bank 3): 8,000 bytes
    ///   Red plane   (Bank 2): 8,000 bytes
    ///   Blue plane  (Bank 1): 8,000 bytes
    /// </summary>
    public static byte[] ConvertImageToPcgData(string imagePath)
    {
        using var originalBitmap = SKBitmap.Decode(imagePath);
        if (originalBitmap == null)
            throw new Exception("Failed to decode image.");

        // Resize to 320x200
        using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(320, 200), SKFilterQuality.High);
        if (resizedBitmap == null)
            throw new Exception("Failed to resize image.");

        // Work with float pixel buffer for error accumulation
        float[,] rBuf = new float[200, 320]; // [y, x]
        float[,] gBuf = new float[200, 320];
        float[,] bBuf = new float[200, 320];

        for (int y = 0; y < 200; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                var c = resizedBitmap.GetPixel(x, y);
                rBuf[y, x] = c.Red / 255.0f;
                gBuf[y, x] = c.Green / 255.0f;
                bBuf[y, x] = c.Blue / 255.0f;
            }
        }

        // Resulting 1-bit plane arrays (value per pixel: 0 or 1)
        int[,] rPlane = new int[200, 320];
        int[,] gPlane = new int[200, 320];
        int[,] bPlane = new int[200, 320];

        // Floyd-Steinberg dithering for each channel independently
        FloydSteinberg(rBuf, rPlane, 200, 320);
        FloydSteinberg(gBuf, gPlane, 200, 320);
        FloydSteinberg(bBuf, bPlane, 200, 320);

        // Pack into PCG byte format: 40x25 blocks of 8x8, each byte = 8 horizontal pixels (MSB=leftmost)
        byte[] greenPlane = new byte[8000];
        byte[] redPlane   = new byte[8000];
        byte[] bluePlane  = new byte[8000];

        for (int blockRow = 0; blockRow < 25; blockRow++)
        {
            for (int blockCol = 0; blockCol < 40; blockCol++)
            {
                int blockIndex = blockRow * 40 + blockCol;
                int byteOffset = blockIndex * 8;

                for (int yInBlock = 0; yInBlock < 8; yInBlock++)
                {
                    int py = blockRow * 8 + yInBlock;
                    byte rByte = 0, gByte = 0, bByte = 0;

                    for (int xInBlock = 0; xInBlock < 8; xInBlock++)
                    {
                        int px = blockCol * 8 + xInBlock;
                        int bit = 7 - xInBlock;
                        if (rPlane[py, px] == 1) rByte |= (byte)(1 << bit);
                        if (gPlane[py, px] == 1) gByte |= (byte)(1 << bit);
                        if (bPlane[py, px] == 1) bByte |= (byte)(1 << bit);
                    }

                    greenPlane[byteOffset + yInBlock] = gByte;
                    redPlane[byteOffset + yInBlock]   = rByte;
                    bluePlane[byteOffset + yInBlock]  = bByte;
                }
            }
        }

        var result = new List<byte>(24000);
        result.AddRange(greenPlane); // Bank 3
        result.AddRange(redPlane);   // Bank 2
        result.AddRange(bluePlane);  // Bank 1

        return result.ToArray();
    }

    /// <summary>
    /// Applies Floyd-Steinberg error diffusion dithering on a single float channel plane.
    /// Values in buf are expected to be in [0.0, 1.0]; output is 0 or 1 in outPlane.
    /// </summary>
    private static void FloydSteinberg(float[,] buf, int[,] outPlane, int height, int width)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float oldVal = Math.Clamp(buf[y, x], 0f, 1f);
                int newVal = oldVal >= 0.5f ? 1 : 0;
                float error = oldVal - newVal;
                outPlane[y, x] = newVal;

                // Distribute error to neighbouring pixels:
                //               * 7/16
                //   3/16  5/16  1/16
                if (x + 1 < width)
                    buf[y, x + 1] += error * (7f / 16f);
                if (y + 1 < height)
                {
                    if (x - 1 >= 0)
                        buf[y + 1, x - 1] += error * (3f / 16f);
                    buf[y + 1, x] += error * (5f / 16f);
                    if (x + 1 < width)
                        buf[y + 1, x + 1] += error * (1f / 16f);
                }
            }
        }
    }
}
