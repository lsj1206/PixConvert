using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using PixConvert.Models;
using PixConvert.Services;

namespace PixConvert.Views.Lists;

/// <summary>
/// 파일 목록을 표시하고 관리(드래그 앤 드롭, 삭제 등)하는 컨트롤의 코드 비하인드 클래스입니다.
/// </summary>
public partial class FileListControl : UserControl
{
    private Point _startPoint;
    private bool _isPotentialDrag;

    public static readonly DependencyProperty DeleteSelectedCommandProperty =
        DependencyProperty.Register(nameof(DeleteSelectedCommand), typeof(ICommand), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty DropFilesCommandProperty =
        DependencyProperty.Register(nameof(DropFilesCommand), typeof(ICommand), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty MoveItemsCommandProperty =
        DependencyProperty.Register(nameof(MoveItemsCommand), typeof(ICommand), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SortByColumnCommandProperty =
        DependencyProperty.Register(nameof(SortByColumnCommand), typeof(ICommand), typeof(FileListControl), new PropertyMetadata(null));

    public static readonly DependencyProperty IsMismatchFilterActiveProperty =
        DependencyProperty.Register(
            nameof(IsMismatchFilterActive),
            typeof(bool),
            typeof(FileListControl),
            new PropertyMetadata(false, OnIsMismatchFilterActiveChanged));

    public ICommand? DeleteSelectedCommand
    {
        get => (ICommand?)GetValue(DeleteSelectedCommandProperty);
        set => SetValue(DeleteSelectedCommandProperty, value);
    }

    public ICommand? DropFilesCommand
    {
        get => (ICommand?)GetValue(DropFilesCommandProperty);
        set => SetValue(DropFilesCommandProperty, value);
    }

    public ICommand? MoveItemsCommand
    {
        get => (ICommand?)GetValue(MoveItemsCommandProperty);
        set => SetValue(MoveItemsCommandProperty, value);
    }

    public ICommand? SortByColumnCommand
    {
        get => (ICommand?)GetValue(SortByColumnCommandProperty);
        set => SetValue(SortByColumnCommandProperty, value);
    }

    public bool IsMismatchFilterActive
    {
        get => (bool)GetValue(IsMismatchFilterActiveProperty);
        set => SetValue(IsMismatchFilterActiveProperty, value);
    }

    /// <summary>
    /// FileListControl의 새 인스턴스를 초기화합니다.
    /// </summary>
    public FileListControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 리스트 뷰에서 키 입력이 발생했을 때 삭제(Delete) 키 여부를 확인하여 항목을 제거합니다.
    /// </summary>
    private void ListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            ExecuteCommand(DeleteSelectedCommand, (sender as ListView)?.SelectedItems);
        }
    }

    /// <summary>
    /// 마우스 왼쪽 버튼을 눌렀을 때 드래그 앤 드롭 준비 작업을 수행합니다.
    /// </summary>
    private void FileListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(null);
        _isPotentialDrag = false;

        ListViewItem? listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
        if (listViewItem != null && listViewItem.IsSelected)
        {
            // 더블 클릭 시에는 WPF 기본 동작(수동 편집 등)을 위해 방해하지 않음
            if (e.ClickCount >= 2) return;

            // 이미 선택된 항목을 클릭한 경우 드래그 가능 상태로 설정
            _isPotentialDrag = true;
            e.Handled = true;
        }
    }

    /// <summary>
    /// 마우스 왼쪽 버튼을 뗐을 때 드래그가 발생하지 않았다면 수동으로 선택을 업데이트합니다.
    /// </summary>
    private void FileListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPotentialDrag && sender is ListView listView)
        {
            ListViewItem? listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
            if (listViewItem != null)
            {
                var item = listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
                listView.SelectedItems.Clear();
                listView.SelectedItems.Add(item);
            }
        }
        _isPotentialDrag = false;
    }

    /// <summary>
    /// 마우스가 움직일 때 일정 거리 이상 이동하면 드래그 작업을 시작합니다.
    /// </summary>
    private void FileListView_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is ListView listView)
        {
            Point mousePos = e.GetPosition(null);
            Vector diff = _startPoint - mousePos;

            // 최소 드래그 임계값 확인
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                ListViewItem? listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);

                if (listViewItem != null)
                {
                    var selectedItems = listView.SelectedItems.Cast<FileItem>().ToList();
                    var clickedItem = listView.ItemContainerGenerator.ItemFromContainer(listViewItem) as FileItem;

                    // 단일 클릭 후 즉시 드래그 시 clickedItem만 대상으로 설정
                    if (clickedItem != null && !selectedItems.Contains(clickedItem))
                    {
                        selectedItems = new List<FileItem> { clickedItem };
                    }

                    if (selectedItems.Count > 0)
                    {
                        // [데이터 보호] 필터링 중에는 순서 변경을 위한 드래그를 금지함
                        if (IsMismatchFilterActive)
                        {
                            return;
                        }

                        _isPotentialDrag = false;
                        var dragData = new DataObject("InternalMove", selectedItems);
                        DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 비주얼 트리에서 지정된 형식의 조상 요소를 찾습니다.
    /// </summary>
    private static T? FindAnchestor<T>(DependencyObject? current) where T : DependencyObject
    {
        do
        {
            if (current is T t) return t;
            current = current != null ? System.Windows.Media.VisualTreeHelper.GetParent(current) : null;
        }
        while (current != null);
        return null;
    }

    /// <summary>
    /// 비주얼 트리에서 지정된 형식의 자식 요소를 찾습니다.
    /// </summary>
    private static T? FindChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;

            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// 드래그 데이터가 컨트롤 영역을 벗어날 때 미리보기 인디케이터를 숨깁니다.
    /// </summary>
    private void FileListView_DragLeave(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 드래그 중인 데이터의 종류에 따라 드롭 가능 여부 및 시각적 효과를 제어합니다.
    /// </summary>
    private void FileListView_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Handled = true;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropIndicator.Visibility = Visibility.Collapsed;
        }
        else if (e.Data.GetDataPresent("InternalMove"))
        {
            e.Effects = DragDropEffects.Move;
            UpdateDropIndicator(e);
            HandleAutoScroll(e);
        }
        else
        {
            e.Effects = DragDropEffects.None;
            DropIndicator.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 드래그 위치가 상/하단 가장자리에 도달하면 자동으로 리스트를 스크롤합니다.
    /// </summary>
    private void HandleAutoScroll(DragEventArgs e)
    {
        var scrollViewer = FindChild<ScrollViewer>(FileListView);
        if (scrollViewer == null) return;

        double tolerance = 30;
        Point position = e.GetPosition(FileListView);

        if (position.Y < tolerance)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - 2);
        }
        else if (position.Y > FileListView.ActualHeight - tolerance)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + 2);
        }
    }

    /// <summary>
    /// 내부 아이템 이동 시 삽입될 위치를 나타내는 선(인디케이터)을 표시합니다.
    /// </summary>
    private void UpdateDropIndicator(DragEventArgs e)
    {
        ListViewItem? listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
        if (listViewItem != null)
        {
            Point point = e.GetPosition(listViewItem);
            double height = listViewItem.ActualHeight;
            Point screenPoint = listViewItem.TranslatePoint(new Point(0, 0), FileListView);

            bool isBottom = point.Y > height / 2;
            double yPos = isBottom ? screenPoint.Y + height : screenPoint.Y;

            DropIndicator.Visibility = Visibility.Visible;
            Canvas.SetTop(DropIndicator, yPos - 1);
        }
        else
        {
            if (FileListView != null && FileListView.Items.Count > 0)
            {
                var lastItem = FileListView.ItemContainerGenerator.ContainerFromIndex(FileListView.Items.Count - 1) as FrameworkElement;
                if (lastItem != null)
                {
                    Point screenPoint = lastItem.TranslatePoint(new Point(0, lastItem.ActualHeight), FileListView);
                    DropIndicator.Visibility = Visibility.Visible;
                    Canvas.SetTop(DropIndicator, screenPoint.Y - 1);
                }
            }
            else
            {
                DropIndicator.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// 외부 파일 드롭 또는 내부 아이템 이동 드롭 시 실제 데이터를 처리합니다.
    /// </summary>
    private async void FileListView_Drop(object sender, DragEventArgs e)
    {
        DropIndicator.Visibility = Visibility.Collapsed;

        // 1. 외부 탐색기 등에서 파일이 드롭된 경우
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                try
                {
                    await ExecuteDropFilesCommandAsync(files);
                }
                catch (Exception ex)
                {
                    // 비동기 처리 중 발생하는 예외를 캡처하여 로그 등에 기록하고 사용자에게 알림
                    System.Diagnostics.Debug.WriteLine($"DropFilesAsync failed: {ex.Message}");
                }
            }
        }
        // 2. 리스트 내부에서 아이템 순서 변경을 위해 드롭된 경우
        else if (e.Data.GetDataPresent("InternalMove"))
        {
            if (e.Data.GetData("InternalMove") is List<FileItem> itemsToMove)
            {
                ListViewItem? listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);
                int targetIndex = -1;
                bool isBottom = false;

                if (listViewItem != null)
                {
                    targetIndex = FileListView.ItemContainerGenerator.IndexFromContainer(listViewItem);
                    Point point = e.GetPosition(listViewItem);
                    isBottom = point.Y > listViewItem.ActualHeight / 2;
                }
                else
                {
                    targetIndex = FileListView.Items.Count;
                }

                if (targetIndex != -1)
                {
                    ExecuteCommand(MoveItemsCommand, new MoveItemsRequest(itemsToMove, targetIndex, isBottom));
                }
            }
        }
    }

    /// <summary>
    /// 리스트 뷰 헤더 클릭 시 해당 컬럼을 기준으로 정렬을 수행합니다.
    /// </summary>
    private void Header_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header)
        {
            // GridViewColumnHeader의 Tag 속성에 정의된 SortType 문자열을 가져옴
            string? sortTypeName = header.Tag as string;
            if (!string.IsNullOrEmpty(sortTypeName) &&
                Enum.TryParse(sortTypeName, out SortType sortType))
            {
                ExecuteCommand(SortByColumnCommand, sortType);
            }
        }
    }

    private static void OnIsMismatchFilterActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FileListControl control)
        {
            control.RefreshFilter();
        }
    }

    private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
    {
        e.Accepted = !IsMismatchFilterActive || e.Item is FileItem { IsMismatch: true };
    }

    private void RefreshFilter()
    {
        if (Resources["FilteredItems"] is CollectionViewSource source)
        {
            source.View?.Refresh();
        }
    }

    private static void ExecuteCommand(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private async System.Threading.Tasks.Task ExecuteDropFilesCommandAsync(string[] files)
    {
        if (DropFilesCommand is IAsyncRelayCommand<string[]> asyncCommand)
        {
            if (asyncCommand.CanExecute(files))
            {
                await asyncCommand.ExecuteAsync(files);
            }

            return;
        }

        ExecuteCommand(DropFilesCommand, files);
    }
}
