# Third-Party Notices

PixConvert 배포물에 포함되는 주요 NuGet 패키지의 라이선스 정보는 다음과 같습니다.

기준:

- `win-x64`, `framework-dependent` publish output
- `src/bin/Release/net10.0-windows/win-x64/publish/PixConvert.deps.json`

## MIT

- `CommunityToolkit.Mvvm` `8.4.2`
- `Microsoft.Extensions.DependencyInjection.Abstractions` `10.0.5`
- `Microsoft.Extensions.DependencyInjection` `10.0.5`
- `Microsoft.Extensions.Logging.Abstractions` `10.0.0`
- `Microsoft.Extensions.Logging` `10.0.0`
- `Microsoft.Extensions.Options` `10.0.0`
- `Microsoft.Extensions.Primitives` `10.0.0`
- `ModernWpfUI` `0.9.6`
- `NetVips` `3.2.0`
- `SkiaSharp` `3.119.2`
- `SkiaSharp.NativeAssets.Win32` `3.119.2`
- `System.Management` `10.0.5`

## Apache-2.0

- `Serilog` `4.3.1`
- `Serilog.Extensions.Logging` `10.0.0`
- `Serilog.Sinks.Async` `2.1.0`
- `Serilog.Sinks.File` `7.0.0`

## LGPL-3.0-or-later

- `NetVips.Native.win-x64` `8.16.1`

## Notes

- `NetVips.Native.win-x64`는 Windows x64용 `libvips` 네이티브 바이너리 패키지입니다.
- 현재 PixConvert 배포물에는 `libvips-42.dll`이 별도 파일로 포함됩니다.
- upstream `libvips` 프로젝트는 `LGPL-2.1-or-later`로 공개되어 있지만, PixConvert는 실제로 재배포하는 NuGet 패키지 `NetVips.Native.win-x64`(`LGPL-3.0-or-later`)를 기준으로 고지합니다.
- `NetVips.Native.win-x64` 패키지에는 별도 `THIRD-PARTY-NOTICES`가 포함되어 있으며, `libvips` 외에도 여러 번들 라이브러리의 고지가 함께 포함됩니다.
- 일부 패키지는 자체 `LICENSE` 또는 `THIRD-PARTY-NOTICES` 파일을 포함하며, 본 문서는 패키지 메타데이터 기준 요약입니다.

## References

- `NetVips.Native.win-x64`: <https://www.nuget.org/packages/NetVips.Native.win-x64/8.16.1>
- `NetVips`: <https://github.com/kleisauke/net-vips>
- `libvips`: <https://github.com/libvips/libvips>
