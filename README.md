# PixConvert

> Windows용 이미지 변환 앱입니다.
> 여러 이미지를 한 번에 불러와 프리셋 기반으로 변환하는 기능을 제공합니다.

![Platform](https://img.shields.io/badge/platform-Windows-lightgrey?style=for-the-badge)
![Framework](https://img.shields.io/badge/.NET-10.0-green?style=for-the-badge)
![Language](https://img.shields.io/badge/language-Korean-blue?style=for-the-badge)
![Language](https://img.shields.io/badge/language-English-blue?style=for-the-badge)

## 주요 기능 및 특징

- 단순하고 직관적인 사용구조를 통해 복잡함을 줄였습니다.
  - 직관적이고 단순한 UI로 구성되어 있습니다.
  - 범용성이 높은 포맷 위주로 지원하고 있습니다.
  - 중요 옵션만 사용하는 설계로 복잡함을 줄였습니다.
- 최대 *10000*개의 이미지 파일에 대한 일괄 변환이 가능합니다.
- **CPU 사용량을 제어**해서 변환을 하면서 다른 작업이 가능합니다.
- 정지 이미지와 애니메이션 이미지를 하나의 목록에서 한 번에 변환 가능합니다.
- **프리셋 기반**으로 작동해 변환 옵션을 재사용 가능합니다.
- 실제 포맷 기반 작동 및 포맷/확장자 불일치 파일 정보 제공합니다.
- 영어 및 한국어를 지원합니다.

- 지원 포맷 목록
  - 일반 이미지: `JPEG`, `PNG`, `BMP`, `WebP`, `AVIF`
  - 애니메이션 이미지: `GIF`, `WebP`

### 변환 엔진 구성

PixConvert는 변환 대상에 따라 두 가지 엔진을 나누어 사용합니다.

[SkiaSharp](https://github.com/mono/skiasharp)는 Google의 2D 그래픽 엔진인 `Skia`를 기반으로 하는 `.NET` 그래픽 라이브러리입니다.

> 정지 이미지 처리에 적합한 엔진으로, `JPEG`, `PNG`, `BMP`, 정지 `WebP` 변환을 담당합니다.

`BMP`는 SkiaSharp의 일반 인코딩 경로에서 직접 저장할 수 없어 내부 `BmpEncoder`를 사용합니다. 이 인코더는 변환된 픽셀을 무압축 24-bit 비트맵으로 기록하며, 투명도가 있는 이미지는 설정된 배경색으로 합성한 뒤 저장합니다.

[NetVips](https://github.com/kleisauke/net-vips)는 고성능 이미지 처리용 C 라이브러리인 `libvips`의 `.NET` 바인딩 라이브러리입니다.

> 고압축 포맷과 멀티프레임 처리에 적합한 엔진으로, `AVIF`, 애니메이션 `GIF`, 애니메이션 `WebP` 변환을 담당합니다.

`AVIF`와 애니메이션 이미지는 인코딩 옵션이 많고 처리 비용이 큰 편이라 NetVips 경로를 사용합니다. PixConvert는 NetVips를 통해 AVIF의 압축 옵션과 GIF/WebP 애니메이션의 프레임 기반 저장을 처리합니다.

## 시작하기

1. 다운로드: [GitHub Releases]()
   - 릴리스 파일 형식: `PixConvert v1.0.0.exe`
   - 별도 설치 과정이 필요없는 파일 배포.
2. 원하는 파일을 목록에 추가합니다.
3. 변환 설정에서 변환 옵션 프리셋을 설정합니다.
4. 변환을 실행합니다.
5. 완료 후 결과를 확인합니다.

## 패치노트

1. v1.0.0 (2026.04.)
   - 첫 릴리스 버전

## 개발 정보

- Language: C#
- Framework: .NET 10.0
- UI Library: [ModernWPF](https://github.com/Kinnara/ModernWPF)
- IDE: `Antigravity`, `VSCode`, `Visual Studio`
- AI: `Gemini`, `Claude`, `Codex`

---

- 개발자: lsj1206
- 기간: 2026.02 ~ 2026.04

## 라이선스(License)

이 프로젝트 자체의 라이선스는 [LICENSE](LICENSE) 파일을 따릅니다.
배포 산출물에 포함되는 서드파티 라이브러리 고지는 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)에 정리했습니다.
