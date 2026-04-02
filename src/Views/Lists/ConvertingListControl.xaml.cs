using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using PixConvert.Models;

namespace PixConvert.Views.Lists;

/// <summary>
/// 변환 중인 파일 현황을 표시하기 위한 전용 리스트 뷰입니다.
/// </summary>
public partial class ConvertingListControl : UserControl
{
    public ConvertingListControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 지원되지 않는(Unsupported) 아이템을 리스트 뷰에서 숨깁니다.
    /// </summary>
    private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
    {
        if (e.Item is FileItem item)
        {
            e.Accepted = item.Status != FileConvertStatus.Unsupported;
        }
    }

    /// <summary>
    /// 성성된 파일명 텍스트(하이퍼링크)를 클릭했을 때 파일 탐색기를 띄우고 해당 파일을 선택 상태로 만듭니다.
    /// </summary>
    private void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink hyperlink && hyperlink.DataContext is FileItem item)
        {
            if (!string.IsNullOrEmpty(item.OutputPath) && System.IO.File.Exists(item.OutputPath))
            {
                try
                {
                    // /select 파라미터를 사용하면 탐색기가 열리면서 해당 파일을 지정/하이라이트 해줍니다.
                    Process.Start("explorer.exe", $"/select,\"{item.OutputPath}\"");
                }
                catch (Exception ex)
                {
                    // 파일 접근 권한 등 오류 처리
                    Debug.WriteLine($"Failed to open explorer: {ex.Message}");
                }
            }
        }
    }
}
