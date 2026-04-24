# PixConvert

[English](README.md) | [한국어](README.ko.md)

> 배치 변환, 프리셋, 정지/애니메이션 이미지 혼합 작업을 지원하는 Windows용 이미지 변환기입니다.

![Platform](https://img.shields.io/badge/platform-Windows-lightgrey?style=for-the-badge)
![Framework](https://img.shields.io/badge/.NET-10.0-green?style=for-the-badge)
![Language](https://img.shields.io/badge/language-English-blue?style=for-the-badge)
![Language](https://img.shields.io/badge/language-Korean-blue?style=for-the-badge)

## 주요 특징

- 최대 `10,000`개의 이미지 파일을 일괄 변환할 수 있습니다.
- 정지 이미지와 지원되는 애니메이션 이미지를 하나의 목록에서 함께 처리합니다.
- 확장자만 보지 않고 파일 시그니처를 읽어 실제 이미지 포맷을 판별합니다.
- 확장자와 실제 포맷이 다른 파일을 확인할 수 있습니다.
- 프리셋으로 변환 설정을 재사용할 수 있습니다.
- 변환 중 CPU 사용량을 조절할 수 있습니다.
- 출력 파일 충돌 시 덮어쓰기, 건너뛰기, 접미사 추가 중 선택할 수 있습니다.
- zip 배포에 맞게 설정, 프리셋, 로그를 실행 폴더 안에서 관리합니다.

## 스크린샷

스크린샷은 추후 `assets/screenshots/` 아래에 추가할 예정입니다.

## 지원 포맷

| 유형 | 포맷 |
| --- | --- |
| 정지 이미지 | `JPEG`, `PNG`, `BMP`, `WebP`, `AVIF` |
| 애니메이션 이미지 | `GIF`, `WebP` |

## 변환 엔진 구성

PixConvert는 변환 대상에 따라 두 가지 엔진을 나누어 사용합니다.

[SkiaSharp](https://github.com/mono/skiasharp)는 Google의 2D 그래픽 엔진인 `Skia`를 기반으로 하는 `.NET` 그래픽 라이브러리입니다.

> PixConvert는 `JPEG`, `PNG`, `BMP`, 정지 `WebP` 같은 정지 이미지 변환에 SkiaSharp를 사용합니다.

`BMP`는 SkiaSharp의 일반 인코딩 경로에서 직접 저장할 수 없어 내부 `BmpEncoder`를 사용합니다. 이 인코더는 변환된 픽셀을 무압축 24-bit 비트맵으로 기록하며, 투명도가 있는 이미지는 설정된 배경색으로 합성한 뒤 저장합니다.

[NetVips](https://github.com/kleisauke/net-vips)는 고성능 이미지 처리용 C 라이브러리인 `libvips`의 `.NET` 바인딩 라이브러리입니다.

> PixConvert는 `AVIF`, 애니메이션 `GIF`, 애니메이션 `WebP`처럼 고압축 또는 멀티프레임 처리가 필요한 작업에 NetVips를 사용합니다.

`AVIF`와 애니메이션 이미지는 인코딩 옵션이 많고 처리 비용이 큰 편이라 NetVips 경로를 사용합니다. PixConvert는 NetVips를 통해 AVIF 압축 옵션과 GIF/WebP 애니메이션의 프레임 기반 저장을 처리합니다.

## 빠른 시작

1. [GitHub Releases](https://github.com/lsj1206/PixConvert/releases)에서 최신 zip 패키지를 다운로드합니다.
2. zip 파일을 쓰기 가능한 폴더에 압축 해제합니다.
3. `PixConvert.exe`를 실행합니다.
4. 변환할 파일 또는 폴더를 목록에 추가합니다.
5. 변환 프리셋을 선택하거나 설정합니다.
6. 변환을 실행하고 결과 파일을 확인합니다.

릴리스 폴더에는 `PixConvert.exe`, 외부 파일로 유지되는 `libvips-42.dll`, `LICENSE`, `THIRD-PARTY-NOTICES.md`가 함께 있어야 합니다. `libvips-42.dll`은 NetVips 경로에서 사용하는 LGPL 관련 네이티브 라이브러리라 single-file 실행 파일 밖에 의도적으로 분리해 둡니다.

## 개발 정보

- Language: C#
- Framework: .NET 10.0
- UI: WPF with [ModernWPF](https://github.com/Kinnara/ModernWPF)
- Core libraries: SkiaSharp, NetVips, CommunityToolkit.Mvvm, Serilog

소스에서 빌드:

```powershell
dotnet build src\PixConvert.csproj -c Release
```

## 라이선스

PixConvert 자체 라이선스는 [LICENSE](LICENSE)를 따릅니다.
재배포되는 서드파티 의존성 고지는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 정리되어 있습니다.
