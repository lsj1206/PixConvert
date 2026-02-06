using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace PixConvert.Services;

/// <summary>
/// Windows Shell API를 사용하여 시스템 아이콘을 추출하고 캐싱하는 서비스 클래스입니다.
/// </summary>
public class IconService : IIconService
{
    private readonly ConcurrentDictionary<string, ImageSource> _iconCache = new();

    // Win32 API 정의
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_SMALLICON = 0x1;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public ImageSource GetIcon(string path)
    {
        // 1. 확장자 추출 (폴더인 경우 별도 처리)
        string extension = Path.GetExtension(path).ToLower();
        if (string.IsNullOrEmpty(extension))
        {
            // 경로가 실제로 존재하고 폴더인지 확인 (성능을 위해 단순 체크)
            extension = Directory.Exists(path) ? "[Folder]" : "[Unknown]";
        }

        // 2. 캐시 확인 및 반환
        return _iconCache.GetOrAdd(extension, _ => ExtractIconFromShell(path));
    }

    private ImageSource ExtractIconFromShell(string path)
    {
        SHFILEINFO shinfo = new SHFILEINFO();

        // SHGFI_USEFILEATTRIBUTES를 사용하면 파일이 실제로 존재하지 않아도 확장자만으로 아이콘을 가져올 수 있음
        // 이는 네트워크 경로나 대량 파일 처리 시 성능상 유리함
        IntPtr hImgSmall = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);

        if (shinfo.hIcon == IntPtr.Zero)
        {
            return null!; // 실패 시 null 반환 (XAML에서 기본값 처리 가능)
        }

        try
        {
            // Icon 객체를 BitmapSource로 변환
            var bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                shinfo.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmapSource.Freeze(); // 크로스 스레드 안정성을 위해 프리즈
            return bitmapSource;
        }
        finally
        {
            // 핸들 해제 필수
            DestroyIcon(shinfo.hIcon);
        }
    }
}
