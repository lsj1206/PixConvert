using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PixConvert.Services;
using System.Collections.ObjectModel;

namespace PixConvert.ViewModels;

/// <summary>
/// 변환 설정(대상 포맷, 품질, 덮어쓰기 등) 상태를 관리하는 뷰모델입니다.
/// </summary>
public partial class ConvertSettingViewModel : ViewModelBase
{
    [ObservableProperty] private string _targetExtension = "png";
    [ObservableProperty] private int _quality = 100;
    [ObservableProperty] private bool _overwrite = false;

    /// <summary>
    /// 지원되는 변환 확장자 목록입니다.
    /// </summary>
    public ObservableCollection<string> SupportedExtensions { get; } =
    [
        "png", "jpg", "jpeg", "webp", "bmp", "tif", "tiff"
    ];

    public ConvertSettingViewModel(ILanguageService languageService, ILogger<ConvertSettingViewModel> logger)
        : base(languageService, logger)
    {
    }
}
