using System;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixConvert.Models;

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

    /// <summary>파일 생성 일시</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>파일 최종 수정 일시</summary>
    [ObservableProperty]
    private DateTime? modifiedDate;

    /// <summary>바이트 단위의 파일 크기</summary>
    [ObservableProperty]
    private long size;

    /// <summary>읽기 쉬운 단위로 변환된 파일 크기</summary>
    [ObservableProperty]
    private string displaySize = string.Empty;

    /// <summary>파일 시그니처</summary>
    [ObservableProperty]
    private string fileSignature = "-";

    /// <summary>파일 시스템의 실제 아이콘 이미지</summary>
    [ObservableProperty]
    private ImageSource? icon;

    /// <summary>목록에 추가된 순번</summary>
    [ObservableProperty]
    private int? addIndex;

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
}
