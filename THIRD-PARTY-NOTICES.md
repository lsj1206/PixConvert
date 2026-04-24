# Third-Party Notices

This file covers the PixConvert `v1.0.0` Windows `win-x64` release package.

PixConvert itself is licensed under the MIT License. See [LICENSE](LICENSE).

## Release Basis

- Target runtime: `win-x64`
- Distribution type: self-contained single-file application plus one external native library
- Main executable: `PixConvert.exe`
- External native library: `libvips-42.dll`

`libvips-42.dll` is intentionally distributed as a separate file next to `PixConvert.exe`. It comes from `NetVips.Native.win-x64` and is kept outside the single-file executable so users can replace the LGPL-related native library with a compatible build.

## License Inventory

### MIT

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

### Apache-2.0

- `Serilog` `4.3.1`
- `Serilog.Extensions.Logging` `10.0.0`
- `Serilog.Sinks.Async` `2.1.0`
- `Serilog.Sinks.File` `7.0.0`

### LGPL-3.0-or-later

- `NetVips.Native.win-x64` `8.16.1`

## NetVips.Native.win-x64

- NuGet package: <https://www.nuget.org/packages/NetVips.Native.win-x64/8.16.1>
- Project URL: <https://kleisauke.github.io/net-vips>
- Repository: <https://github.com/kleisauke/net-vips>
- Package license expression: `LGPL-3.0-or-later`
- Redistributed file: `libvips-42.dll`

The `NetVips.Native.win-x64` package includes its own third-party notice file. The notice below is copied from the installed NuGet package `NetVips.Native.win-x64` `8.16.1`.

## NetVips.Native.win-x64 Bundled Notices

# Third-party notices

This software contains third-party libraries
used under the terms of the following licences:

| Library       | Used under the terms of                                                                                   |
|---------------|-----------------------------------------------------------------------------------------------------------|
| aom           | BSD 2-Clause + [Alliance for Open Media Patent License 1.0](https://aomedia.org/license/patent-license/)  |
| cairo         | Mozilla Public License 2.0                                                                                |
| cgif          | MIT Licence                                                                                               |
| expat         | MIT Licence                                                                                               |
| fontconfig    | [fontconfig Licence](https://gitlab.freedesktop.org/fontconfig/fontconfig/blob/main/COPYING) (BSD-like)   |
| freetype      | [freetype Licence](https://git.savannah.gnu.org/cgit/freetype/freetype2.git/tree/docs/FTL.TXT) (BSD-like) |
| fribidi       | LGPLv3                                                                                                    |
| glib          | LGPLv3                                                                                                    |
| harfbuzz      | MIT Licence                                                                                               |
| highway       | Apache-2.0 License, BSD 3-Clause                                                                          |
| lcms          | MIT Licence                                                                                               |
| libarchive    | BSD 2-Clause                                                                                              |
| libexif       | LGPLv3                                                                                                    |
| libffi        | MIT Licence                                                                                               |
| libheif       | LGPLv3                                                                                                    |
| libimagequant | [BSD 2-Clause](https://github.com/lovell/libimagequant/blob/main/COPYRIGHT)                               |
| libnsgif      | MIT Licence                                                                                               |
| libpng        | [libpng License](https://github.com/pnggroup/libpng/blob/master/LICENSE)                                  |
| librsvg       | LGPLv3                                                                                                    |
| libspng       | [BSD 2-Clause, libpng License](https://github.com/randy408/libspng/blob/master/LICENSE)                   |
| libtiff       | [libtiff License](https://gitlab.com/libtiff/libtiff/blob/master/LICENSE.md) (BSD-like)                   |
| libvips       | LGPLv3                                                                                                    |
| libwebp       | New BSD License                                                                                           |
| libxml2       | MIT Licence                                                                                               |
| mozjpeg       | [zlib License, IJG License, BSD-3-Clause](https://github.com/mozilla/mozjpeg/blob/master/LICENSE.md)      |
| pango         | LGPLv3                                                                                                    |
| pixman        | MIT Licence                                                                                               |
| proxy-libintl | LGPLv3                                                                                                    |
| zlib-ng       | [zlib Licence](https://github.com/zlib-ng/zlib-ng/blob/develop/LICENSE.md)                                |

Use of libraries under the terms of the LGPLv3 is via the
"any later version" clause of the LGPLv2 or LGPLv2.1.

Please report any errors or omissions via
https://github.com/lovell/sharp-libvips/issues/new or
https://github.com/kleisauke/libvips-packaging/issues/new
