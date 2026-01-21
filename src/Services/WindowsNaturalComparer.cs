using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PixConvert.Services;

/// <summary>
/// 윈도우 탐색기와 동일한 방식(자연 정렬)으로 문자열을 비교하는 비교자 클래스입니다.
/// 숫자 문자열을 단순 텍스트가 아닌 수치로서 인식하여 정렬합니다. (예: "1", "2", "10" 순서)
/// </summary>
public class WindowsNaturalComparer : IComparer<string>
{
    // Windows 시스템 라이브러리(shlwapi.dll)의 논리적 문자열 비교 함수를 활용합니다.
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    /// <summary>
    /// 두 문자열을 시스템 API를 사용하여 자연스러운 순서로 비교합니다.
    /// </summary>
    /// <param name="x">비교할 첫 번째 문자열</param>
    /// <param name="y">비교할 두 번째 문자열</param>
    /// <returns>비교 결과 (음수: x가 작음, 0: 같음, 양수: x가 큼)</returns>
    public int Compare(string? x, string? y)
    {
        // 널(Null) 참조에 대한 기본 처리
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        // 시스템 API 호출
        return StrCmpLogicalW(x, y);
    }
}
