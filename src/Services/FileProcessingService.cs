using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PixConvert.Models;

namespace PixConvert.Services;

/// <summary>
/// IFileProcessingService의 상세 구현 클래스입니다.
/// </summary>
public class FileProcessingService : IFileProcessingService
{
    private readonly IFileService _fileService;

    public FileProcessingService(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<FileProcessingResult> ProcessPathsAsync(
        IEnumerable<string> paths,
        int maxItemCount,
        int currentCount,
        IProgress<FileProcessingProgress>? progress = null)
    {
        var result = new FileProcessingResult();
        var rawPaths = paths.ToList();
        result.TotalPathCount = rawPaths.Count;

        // 1. 폴더 경로 분할 및 리스트 초기화
        var folders = rawPaths.Where(Directory.Exists).ToList();
        var otherPaths = rawPaths.Where(p => !Directory.Exists(p)).ToList();
        var newItems = new List<FileItem>();

        // 2. 직접 추가된 파일 처리 (Single Touch: Exists/FileInfo 없이 즉시 스트림 오픈)
        foreach (var path in otherPaths)
        {
            var item = await _fileService.CreateFileItemAsync(path);
            if (item != null)
            {
                newItems.Add(item);
            }
        }

        // 3. 폴더 내 파일 처리 (방안 1의 FileInfo 활용 + 시그니처 분석)
        if (folders.Count > 0)
        {
            var folderFiles = new List<FileInfo>();
            await Task.Run(() =>
            {
                foreach (var folderPath in folders)
                {
                    folderFiles.AddRange(_fileService.GetFilesInFolder(folderPath));
                }
            });

            int folderFileCount = folderFiles.Count;
            for (int i = 0; i < folderFileCount; i++)
            {
                var fileInfo = folderFiles[i];
                var item = _fileService.CreateFileItem(fileInfo);
                if (item != null)
                {
                    item.FileSignature = await _fileService.AnalyzeSignatureAsync(item.Path);
                    newItems.Add(item);
                }

                // 진행률 업데이트 (폴더 처리 분량에 대해)
                if (progress != null && (i % 100 == 0 || i == folderFileCount - 1))
                {
                    progress.Report(new FileProcessingProgress
                    {
                        CurrentIndex = newItems.Count, // 전체 중 현재까지 추가된 수
                        TotalCount = otherPaths.Count + folderFileCount // 예측 총량
                    });
                }
            }
        }

        // 4. 정책 검사: 최대 수량 초과 여부 (최종 취합 후 판단)
        if (currentCount + newItems.Count > maxItemCount)
        {
            result.IgnoredCount = newItems.Count;
            return result;
        }

        result.NewItems = newItems;
        return result;
    }
}
