using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixConvert.Models;

/// <summary>
/// 파일 변환 작업의 각 단계를 정의하는 상태 열거형입니다.
/// </summary>
public enum FileConvertStatus
{
    /// <summary>변환 전 대기 상태</summary>
    Pending,

    /// <summary>현재 변환이 진행 중인 상태</summary>
    Processing,

    /// <summary>변환이 성공적으로 완료된 상태</summary>
    Success,

    /// <summary>변환 도중 오류가 발생한 상태</summary>
    Error,

    /// <summary>Skip 정책으로 변환이 의도적으로 생략된 상태</summary>
    Skipped
}


/// <summary>
/// 파일 목록에 표시되는 개별 파일의 정보와 상태를 담는 데이터 모델 클래스입니다.
/// </summary>
public partial class FileItem : ObservableObject
{
    /// <summary>Path 속성의 변경을 감지하고 연쇄적 반응을 위해 별도로 선언된 저장 변수</summary>
    private string _path = default!;

    /// <summary>파일의 전체 경로를 가져오거나 설정, 파일의 기본 정보를 자동으로 분석합니다.</summary>
    public required string Path
    {
        get => _path;
        set
        {
            if (SetProperty(ref _path, value))
            {
                ParsePathInfo();
            }
        }
    }

    /// <summary>파일이 위치한 디렉터리 경로</summary>
    public string Directory { get; private set; } = string.Empty;

    /// <summary>확장자를 제외한 파일 이름</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>파일의 확장자 (점 제외, 소문자)</summary>
    public string Extension { get; private set; } = string.Empty;

    /// <summary>바이트 단위의 파일 크기</summary>
    [ObservableProperty]
    private long _size;

    /// <summary>파일 시그니처</summary>
    [ObservableProperty]
    private string _fileSignature = "-";

    /// <summary>애니메이션 포함 여부 (GIF, WebP-Ani)</summary>
    [ObservableProperty]
    private bool _isAnimation = false;

    /// <summary>파일 지원 여부 (기본값 true, 분석 성공 시 false로 변경)</summary>
    [ObservableProperty]
    private bool _isUnsupported = true;

    /// <summary>확장자와 시그니처 포맷이 불일치하는지 여부 (동의어 제외)</summary>
    public bool IsMismatch =>
        FileSignature != "-" &&
        !string.Equals(Extension, FileSignature, StringComparison.OrdinalIgnoreCase) &&
        !Synonyms.Contains((Extension, FileSignature.ToLower()));

    /// <summary>목록에 추가된 순번</summary>
    [ObservableProperty]
    private int? _addIndex;

    /// <summary>현재 파일의 변환 상태</summary>
    [ObservableProperty]
    private FileConvertStatus _status = FileConvertStatus.Pending;

    /// <summary>변환 진행률 (0~100)</summary>
    [ObservableProperty]
    private double _progress = 0;

    /// <summary>현재 변환을 처리 중인 가동 엔진 이름 (작업 중일 때만 설정됨)</summary>
    [ObservableProperty]
    private string? _processingEngine;

    /// <summary>변환 성공 후 생성된 결과 파일의 전체 경로</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputName))]
    private string? _outputPath;

    /// <summary>변환 성공 후 생성된 결과 파일의 이름</summary>
    public string OutputName => string.IsNullOrEmpty(OutputPath) ? string.Empty : System.IO.Path.GetFileName(OutputPath);

    /// <summary>변환 성공 후 생성된 결과 파일의 바이트 크기</summary>
    [ObservableProperty]
    private long _outputSize;

    /// <summary>지역화된 상태 텍스트를 가져옵니다. (안전한 제3방식)</summary>
    public string StatusText => SafeGetResource($"Status_{Status}");

    /// <summary>UI 갱신을 위해 StatusText 속성 변경 알림을 강제로 발생시킵니다.</summary>
    public void RefreshStatusText() => OnPropertyChanged(nameof(StatusText));

    /// <summary>
    /// 안전하게 애플리케이션 리소스를 조회합니다.
    /// 유닛 테스트 환경(App.Current가 null)에서도 크래시가 발생하지 않도록 설계되었습니다.
    /// </summary>
    private string SafeGetResource(string key)
    {
        if (System.Windows.Application.Current == null) return key;
        return System.Windows.Application.Current.TryFindResource(key) as string ?? key;
    }

    /// <summary>Status 변경 시 StatusText 동기화 알림</summary>
    partial void OnStatusChanged(FileConvertStatus value)
    {
        RefreshStatusText();
    }

    /// <summary>FileSignature 변경 시 IsMismatch 및 상태 갱신</summary>
    partial void OnFileSignatureChanged(string value)
    {
        OnPropertyChanged(nameof(IsMismatch));

        // 시그니처가 없으면 미지원 상태로 설정
        if (value == "-")
            IsUnsupported = true;
    }

    /// <summary>현재 Path를 기반으로 파일의 세부 구성 정보를 파싱합니다.</summary>
    private void ParsePathInfo()
    {
        Directory = System.IO.Path.GetDirectoryName(_path) ?? string.Empty;
        Name = System.IO.Path.GetFileNameWithoutExtension(_path);

        // 점(.)을 제외하고 소문자로 변환하여 저장
        string ext = System.IO.Path.GetExtension(_path);
        Extension = ext.TrimStart('.').ToLower();

        // Path 재설정 시 UI 바인딩 갱신을 위한 알림
        OnPropertyChanged(nameof(Directory));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Extension));
    }

    /// <summary>확장자 동의어 쌍 테이블 (양방향 등록).</summary>
    private static readonly HashSet<(string, string)> Synonyms =
    [
        // jpg와 jpeg는 동일 포맷의 다른 표기
        ("jpg", "jpeg"),
        ("jpeg", "jpg"),
    ];
}
