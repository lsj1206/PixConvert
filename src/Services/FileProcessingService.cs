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

        // 1. 파일과 폴더 경로 분리 및 폴더 내 파일 추출
        var files = rawPaths.Where(File.Exists).ToList();
        var folders = rawPaths.Where(Directory.Exists).ToList();
        var finalPaths = new List<string>(files);

        if (folders.Count > 0)
        {
            await Task.Run(() =>
            {
                foreach (var folderPath in folders)
                {
                    finalPaths.AddRange(_fileService.GetFilesInFolder(folderPath));
                }
            });
        }

        int addCount = finalPaths.Count;

        // 2. 정책 검사: 최대 수량 초과 여부
        if (currentCount + addCount > maxItemCount)
        {
            result.IgnoredCount = addCount;
            return result; // 결과는 비어있고 무시된 개수만 기록
        }

        if (addCount == 0) return result;

        // 3. FileItem 객체 생성 및 시그니처 분석 (비동기 루프)
        var newItems = new List<FileItem>(addCount);
        for (int i = 0; i < addCount; i++)
        {
            var item = _fileService.CreateFileItem(finalPaths[i]);
            if (item != null)
            {
                // 실시간 시그니처 분석 및 변수 할당
                item.FileSignature = await _fileService.AnalyzeSignatureAsync(item.Path);
                newItems.Add(item);
            }

            // 진행률 업데이트 (100개 단위 또는 마지막)
            if (progress != null && (i % 100 == 0 || i == addCount - 1))
            {
                progress.Report(new FileProcessingProgress
                {
                    CurrentIndex = i + 1,
                    TotalCount = addCount
                });
            }
        }

        result.NewItems = newItems;
        return result;
    }
}
