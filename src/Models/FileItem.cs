using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PixConvert.Models;

/// <summary>
/// 파일 목록에 표시되는 개별 파일의 정보와 상태를 담는 데이터 모델 클래스입니다.
/// </summary>
public partial class FileItem : ObservableObject
{
    private string _path = default!;

    /// <summary>
    /// 파일의 전체 경로를 가져오거나 설정합니다.
    /// 설정 시 파일의 기본 정보(디렉터리, 이름, 확장자 등)를 자동으로 분석합니다.
    /// </summary>
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
    public string BaseName { get; private set; } = string.Empty;

    /// <summary>파일의 확장자 (점 포함)</summary>
    public string BaseExtension { get; private set; } = string.Empty;

    /// <summary>바이트 단위의 파일 크기</summary>
    public long Size { get; set; }

    /// <summary>파일 생성 일시</summary>
    public DateTime? CreatedDate { get; set; }

    /// <summary>파일 최종 수정 일시</summary>
    public DateTime? ModifiedDate { get; set; }

    /// <summary>목록에 추가된 순번</summary>
    [ObservableProperty]
    private int? addIndex;

    /// <summary>목록 화면에 표시될 파일 이름 (확장자 포함 여부에 따라 변동)</summary>
    [ObservableProperty]
    private string displayBaseName = string.Empty;

    /// <summary>읽기 쉬운 단위로 변환된 파일 크기 (예: 12 KB)</summary>
    public string DisplaySize { get; set; } = string.Empty;

    private bool _lastShowExtension = false;

    /// <summary>
    /// 현재 Path를 기반으로 파일의 세부 구성 정보를 파싱합니다.
    /// </summary>
    private void ParsePathInfo()
    {
        Directory = System.IO.Path.GetDirectoryName(_path) ?? string.Empty;
        BaseName = System.IO.Path.GetFileNameWithoutExtension(_path);
        BaseExtension = System.IO.Path.GetExtension(_path);

        UpdateDisplay(_lastShowExtension);
    }

    /// <summary>
    /// 확장자 표시 여부 설정에 따라 화면에 보여줄 파일 이름을 갱신합니다.
    /// </summary>
    /// <param name="showExtension">true면 확장자를 포함하고, false면 제외함</param>
    public void UpdateDisplay(bool showExtension)
    {
        _lastShowExtension = showExtension;
        DisplayBaseName = showExtension ? BaseName + BaseExtension : BaseName;
    }
}
