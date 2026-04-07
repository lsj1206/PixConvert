using System;
using System.IO;
using System.Threading.Tasks;
using SkiaSharp;

namespace PixConvert.Services.Providers;

internal static class BmpEncoder
{
    /// <summary>
    /// SKImage.Encode가 BMP를 지원하지 않으므로(null 반환), BMP 이진 포맷을 직접 기록합니다.
    /// Bgra8888 픽셀 순서(Windows x64 기본값)가 BMP의 BGR 순서와 일치하므로
    /// 바이트 변환 없이 B, G, R 순으로 직접 읽을 수 있습니다.
    /// </summary>
    internal static async Task SaveAsync(SKBitmap src, string outputPath)
    {
        // PlatformColorType(Bgra8888)으로 강제 변환하여 바이트 순서를 고정
        bool wasCopied = src.ColorType != SKImageInfo.PlatformColorType;
        SKBitmap bmp = wasCopied ? src.Copy(SKImageInfo.PlatformColorType) : src;

        try
        {
            int w          = bmp.Width;
            int h          = bmp.Height;
            int rowBytes24 = ((w * 3 + 3) / 4) * 4;  // 4바이트 패딩
            int pixelSize  = rowBytes24 * h;
            int fileSize   = 54 + pixelSize;           // 14 + 40 + pixel data

            await using var fs = new FileStream(
                outputPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);

            // BinaryWriter는 fs를 소유하지 않도록 설정
            using var bw = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true);

            // ── BITMAPFILEHEADER (14 bytes) ─────────────────────────────
            bw.Write(new byte[] { (byte)'B', (byte)'M' }); // 시그니처
            bw.Write(fileSize);                             // bfSize
            bw.Write(0);                                    // bfReserved
            bw.Write(54);                                   // bfOffBits

            // ── BITMAPINFOHEADER (40 bytes) ─────────────────────────────
            bw.Write(40);         // biSize
            bw.Write(w);          // biWidth
            bw.Write(h);          // biHeight (양수 = bottom-up)
            bw.Write((short)1);   // biPlanes
            bw.Write((short)24);  // biBitCount (24-bit RGB)
            bw.Write(0);          // biCompression (BI_RGB)
            bw.Write(pixelSize);  // biSizeImage
            bw.Write(2835);       // biXPelsPerMeter (~72 DPI)
            bw.Write(2835);       // biYPelsPerMeter
            bw.Write(0);          // biClrUsed
            bw.Write(0);          // biClrImportant
            bw.Flush();

            // ── 픽셀 데이터 (bottom-to-top, BGR, 행 패딩) ──────────────
            int    srcStride = bmp.RowBytes;         // 원본 행 바이트 수 (w * 4)
            byte[] rowBuf    = new byte[rowBytes24]; // 출력 행 버퍼 (zero-initialized = 패딩 포함)

            for (int y = h - 1; y >= 0; y--)            // bottom-to-top
            {
                CopyRow(y, rowBuf, bmp, srcStride, w);
                // rowBuf의 나머지(패딩)는 0으로 초기화된 상태 유지
                await fs.WriteAsync(rowBuf, 0, rowBytes24);
            }
        }
        finally
        {
            if (wasCopied) bmp.Dispose();
        }

        // ReadOnlySpan(ref struct)은 await 지점을 넘나들 수 없으므로 별도 로컬 함수로 분리
        static void CopyRow(int y, byte[] buffer, SKBitmap bitmap, int stride, int width)
        {
            ReadOnlySpan<byte> span = bitmap.GetPixelSpan();
            int srcRow = y * stride;
            int dstOff = 0;
            for (int x = 0; x < width; x++)
            {
                int src4 = srcRow + x * 4;
                buffer[dstOff++] = span[src4];     // B
                buffer[dstOff++] = span[src4 + 1]; // G
                buffer[dstOff++] = span[src4 + 2]; // R
            }
        }
    }
}
