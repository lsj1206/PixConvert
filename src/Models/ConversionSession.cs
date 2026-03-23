using System;
using System.Collections.Generic;
using System.IO;

namespace PixConvert.Models;

/// <summary>
/// 단일 변환 배치(Parallel.ForEachAsync 1회) 동안
/// 출력 경로 예약 상태를 보관합니다.
///
/// [설계 결정 근거]
/// - static 상태를 사용하지 않으므로 세션 간 오염이 없음
/// - ConcurrentDictionary 대신 HashSet + lock 사용:
///   "존재 확인 → 추가"의 복합 연산을 원자적으로 묶으려면
///   어차피 외부 lock이 필요하므로 단순한 HashSet + lock이 더 명확
/// - IDisposable: using 블록으로 생명주기를 강제, 수동 ClearSession() 불필요
/// </summary>
public sealed class ConversionSession : IDisposable
{
    private readonly object _gate = new();
    private readonly HashSet<string> _reserved
        = new(StringComparer.OrdinalIgnoreCase);

    // ── Skip 정책용 ────────────────────────────────────────────────────────
    /// <summary>
    /// [원자 연산: check + reserve]
    /// 세션 예약 및 디스크 양쪽 모두 비어있을 때만 예약 후 true 반환.
    /// 하나라도 존재하면 false 반환 (Skip 처리 신호).
    /// </summary>
    public bool TryReserve(string path)
    {
        lock (_gate)
        {
            if (_reserved.Contains(path) || File.Exists(path))
                return false;

            _reserved.Add(path);
            return true;
        }
    }

    // ── Overwrite 정책용 ────────────────────────────────────────────────────
    /// <summary>
    /// [원자 연산: check + reserve]
    /// 디스크 존재 여부와 무관하게 예약 등록.
    /// 세션 내 이미 예약된 경우 isCollision=true를 반환하여 로깅에 활용.
    /// </summary>
    public (string Path, bool IsCollision) ReserveForce(string path)
    {
        lock (_gate)
        {
            bool collision = !_reserved.Add(path);
            return (path, collision);
        }
    }

    // ── Suffix 정책용 ───────────────────────────────────────────────────────
    /// <summary>
    /// [원자 연산: check + reserve — Suffix 전용]
    /// lock 안에서 세션 예약과 디스크를 동시에 확인하며
    /// 사용 가능한 첫 번째 경로를 탐색 후 예약하여 반환.
    /// lock 범위 내에서 File.Exists를 호출하므로 TOCTOU 경합이 없음.
    /// </summary>
    public string FindAndReserveSuffixed(string basePath)
    {
        lock (_gate)
        {
            string dir  = Path.GetDirectoryName(basePath) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(basePath);
            string ext  = Path.GetExtension(basePath);

            // suffix 없는 원본 경로 먼저 시도
            if (!_reserved.Contains(basePath) && !File.Exists(basePath))
            {
                _reserved.Add(basePath);
                return basePath;
            }

            for (int i = 1; i <= 9999; i++)
            {
                string candidate = Path.Combine(dir, $"{name}_{i}{ext}");
                if (!_reserved.Contains(candidate) && !File.Exists(candidate))
                {
                    _reserved.Add(candidate);
                    return candidate;
                }
            }

            // 9999회 충돌 시 타임스탬프 폴백 (실질적 도달 불가)
            string fallback = Path.Combine(dir,
                $"{name}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}");
            _reserved.Add(fallback);
            return fallback;
        }
    }

    public void Dispose()
    {
        lock (_gate) _reserved.Clear();
    }
}
