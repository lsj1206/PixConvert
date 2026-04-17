using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// 파일 시스템 조작을 담당하는 서비스 클래스입니다.
/// </summary>
public class FileScannerService : IFileScannerService
{
    private readonly ILogger<FileScannerService> _logger;
    private readonly ILanguageService _languageService;

    private string GetString(string key) => _languageService.GetString(key);

    public FileScannerService(ILogger<FileScannerService> logger, ILanguageService languageService)
    {
        _logger = logger;
        _languageService = languageService;
    }

    /// <summary>
    /// 파일의 헤더(매직 넘버)를 분석하여 실제 포맷, 애니메이션 여부, 미지원 여부를 반환합니다.
    /// </summary>
    public async Task<FileSignatureResult> AnalyzeSignatureAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return Unsupported();

        try
        {
            // 32바이트로 확장하여 WebP의 상세 플래그 및 AVIF 시퀀스 확인 가능하게 함
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                return await AnalyzeSignatureAsync(fs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_File_SignatureFail"), path);
            return Unsupported();
        }
    }

    /// <summary>
    /// [Single Touch] 단일 스트림을 통해 메타데이터 조회와 시그니처 분석을 한 번에 수행합니다.
    /// </summary>
    public async Task<FileItem?> CreateFileItemAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            // 파일을 한 번만 열어서 모든 정보를 획득 (Single Touch)
            // 32바이트 헤더 읽기로 확장하여 애니메이션 여부 판별
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                long length = fs.Length;

                FileSignatureResult signature = await AnalyzeSignatureAsync(fs);

                return new FileItem
                {
                    Path = path,
                    Size = length,
                    FileSignature = signature.Format,
                    IsAnimation = signature.IsAnimation,
                    IsUnsupported = signature.IsUnsupported,
                    AddIndex = null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, GetString("Log_File_MetaFail"), path);
            return null;
        }
    }

    private static async Task<FileSignatureResult> AnalyzeSignatureAsync(Stream stream)
    {
        byte[] header = new byte[32];
        stream.Position = 0;
        int bytesRead = await ReadUpToAsync(stream, header);

        if (bytesRead < 2)
            return Unsupported();

        if (bytesRead >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
        {
            return new FileSignatureResult("JPEG", false, false);
        }

        if (bytesRead >= 8 && IsPngSignature(header))
            return await AnalyzePngAsync(stream);

        if (bytesRead >= 6 && IsGifSignature(header))
            return await AnalyzeGifAsync(stream);

        if (header[0] == 0x42 && header[1] == 0x4D)
            return new FileSignatureResult("BMP", false, false);

        if (bytesRead >= 12 && IsWebpSignature(header))
            return AnalyzeWebpHeader(header, bytesRead);

        if (bytesRead >= 12 && IsFtypBox(header))
            return await AnalyzeAvifAsync(stream);

        return Unsupported();
    }

    private static FileSignatureResult AnalyzeWebpHeader(byte[] header, int bytesRead)
    {
        if (bytesRead >= 21 &&
            header[12] == 0x56 && header[13] == 0x50 && header[14] == 0x38 && header[15] == 0x58)
        {
            bool isAnimation = (header[20] & 0x02) != 0;
            return new FileSignatureResult("WEBP", isAnimation, false);
        }

        return new FileSignatureResult("WEBP", false, false);
    }

    private static async Task<FileSignatureResult> AnalyzePngAsync(Stream stream)
    {
        stream.Position = 8;
        byte[] chunkHeader = new byte[8];

        while (await ReadExactAsync(stream, chunkHeader))
        {
            uint length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader.AsSpan(0, 4));
            string type = Encoding.ASCII.GetString(chunkHeader, 4, 4);

            if (type == "acTL")
                return new FileSignatureResult("PNG", true, true);

            if (type == "IDAT")
                return new FileSignatureResult("PNG", false, false);

            if (type == "IEND")
                return Unsupported();

            long nextPosition = stream.Position + length + 4L;
            if (nextPosition < stream.Position || nextPosition > stream.Length)
                return Unsupported();

            stream.Position = nextPosition;
        }

        return Unsupported();
    }

    private static async Task<FileSignatureResult> AnalyzeGifAsync(Stream stream)
    {
        stream.Position = 6;
        byte[] logicalScreenDescriptor = new byte[7];
        if (!await ReadExactAsync(stream, logicalScreenDescriptor))
            return Unsupported();

        if ((logicalScreenDescriptor[4] & 0x80) != 0)
        {
            int colorTableSize = 3 * (1 << ((logicalScreenDescriptor[4] & 0x07) + 1));
            if (!SkipBytes(stream, colorTableSize))
                return Unsupported();
        }

        int imageCount = 0;

        while (true)
        {
            int block = await ReadByteAsync(stream);
            switch (block)
            {
                case -1:
                    return imageCount >= 2
                        ? new FileSignatureResult("GIF", true, false)
                        : new FileSignatureResult("GIF", false, true);
                case 0x3B:
                    return imageCount >= 2
                        ? new FileSignatureResult("GIF", true, false)
                        : new FileSignatureResult("GIF", false, true);
                case 0x2C:
                    imageCount++;
                    if (imageCount >= 2)
                        return new FileSignatureResult("GIF", true, false);

                    if (!await SkipGifImageAsync(stream))
                        return Unsupported();
                    break;
                case 0x21:
                    if (await ReadByteAsync(stream) < 0 || !await SkipSubBlocksAsync(stream))
                        return Unsupported();
                    break;
                default:
                    return Unsupported();
            }
        }
    }

    private static async Task<bool> SkipGifImageAsync(Stream stream)
    {
        byte[] imageDescriptor = new byte[9];
        if (!await ReadExactAsync(stream, imageDescriptor))
            return false;

        if ((imageDescriptor[8] & 0x80) != 0)
        {
            int colorTableSize = 3 * (1 << ((imageDescriptor[8] & 0x07) + 1));
            if (!SkipBytes(stream, colorTableSize))
                return false;
        }

        if (await ReadByteAsync(stream) < 0)
            return false;

        return await SkipSubBlocksAsync(stream);
    }

    private static async Task<FileSignatureResult> AnalyzeAvifAsync(Stream stream)
    {
        stream.Position = 0;
        byte[] boxHeader = new byte[8];
        if (!await ReadExactAsync(stream, boxHeader) || Encoding.ASCII.GetString(boxHeader, 4, 4) != "ftyp")
            return Unsupported();

        uint boxSize = BinaryPrimitives.ReadUInt32BigEndian(boxHeader.AsSpan(0, 4));
        long boxEnd = boxSize == 0 ? stream.Length : boxSize;
        if (boxSize == 1 || boxEnd < 16 || boxEnd > stream.Length)
            return Unsupported();

        bool hasAvif = false;
        bool hasAvis = false;

        string majorBrand = await ReadFourCcAsync(stream);
        if (majorBrand.Length != 4)
            return Unsupported();

        hasAvif |= majorBrand == "avif";
        hasAvis |= majorBrand == "avis";

        if (!SkipBytes(stream, 4))
            return Unsupported();

        while (stream.Position + 4 <= boxEnd)
        {
            string brand = await ReadFourCcAsync(stream);
            if (brand.Length != 4)
                return Unsupported();

            hasAvif |= brand == "avif";
            hasAvis |= brand == "avis";
        }

        if (hasAvis)
            return new FileSignatureResult("AVIF", true, true);

        if (hasAvif)
            return new FileSignatureResult("AVIF", false, false);

        return Unsupported();
    }

    private static bool IsPngSignature(byte[] header) =>
        header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
        header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

    private static bool IsGifSignature(byte[] header) =>
        header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
        (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61;

    private static bool IsWebpSignature(byte[] header) =>
        header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
        header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

    private static bool IsFtypBox(byte[] header) =>
        header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70;

    private static async Task<string> ReadFourCcAsync(Stream stream)
    {
        byte[] bytes = new byte[4];
        return await ReadExactAsync(stream, bytes)
            ? Encoding.ASCII.GetString(bytes)
            : string.Empty;
    }

    private static async Task<bool> SkipSubBlocksAsync(Stream stream)
    {
        while (true)
        {
            int size = await ReadByteAsync(stream);
            if (size < 0)
                return false;
            if (size == 0)
                return true;
            if (!SkipBytes(stream, size))
                return false;
        }
    }

    private static bool SkipBytes(Stream stream, long count)
    {
        long nextPosition = stream.Position + count;
        if (nextPosition < stream.Position || nextPosition > stream.Length)
            return false;

        stream.Position = nextPosition;
        return true;
    }

    private static async Task<int> ReadByteAsync(Stream stream)
    {
        byte[] buffer = new byte[1];
        int read = await stream.ReadAsync(buffer.AsMemory(0, 1));
        return read == 1 ? buffer[0] : -1;
    }

    private static async Task<int> ReadUpToAsync(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total));
            if (read == 0)
                break;
            total += read;
        }

        return total;
    }

    private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer)
    {
        int read = await ReadUpToAsync(stream, buffer);
        return read == buffer.Length;
    }

    private static FileSignatureResult Unsupported() => new("-", false, true);


    public IEnumerable<FileInfo> GetFilesInFolder(string folderPath)
    {
        // 10만 개 이상의 파일이 있어도 List에 담지 않고 하나씩 반환하기 위해 yield return 사용
        var stack = new Stack<string>();
        stack.Push(folderPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            DirectoryInfo dirInfo;

            try
            {
                dirInfo = new DirectoryInfo(currentDir);
                if (!dirInfo.Exists) continue;
            }
            catch { continue; }

            // 1. 현재 폴더의 파일 목록 가져오기 (try-catch 외부에서 yield 하기 위해 분리)
            FileInfo[]? files = null;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, GetString("Log_File_FolderAccessFail"), currentDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, GetString("Log_File_FolderTraverseFail"), currentDir);
            }

            if (files != null)
            {
                foreach (var file in files)
                {
                    yield return file;
                }
            }

            // 2. 하위 폴더 목록 가져오기
            DirectoryInfo[]? subDirs = null;
            try
            {
                subDirs = dirInfo.GetDirectories();
            }
            catch { /* 하위 폴더 접근 실패 시 해당 경로는 건너뜀 */ }

            if (subDirs != null)
            {
                foreach (var dir in subDirs)
                {
                    stack.Push(dir.FullName);
                }
            }
        }
    }
}
