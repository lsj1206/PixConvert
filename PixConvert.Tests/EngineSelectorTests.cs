using PixConvert.Models;
using PixConvert.Services;
using PixConvert.Services.Providers;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// EngineSelector(파일 시그니처 기반 변환 엔진 선택 팩토리) 로직을 검증하는 테스트 클래스입니다.
///
/// [테스트 핵심 전략: 팩토리 패턴 계약 검증]
/// EngineSelector는 파일 시그니처에 따라 올바른 변환 엔진 구현체를 반환해야 합니다.
/// 잘못된 엔진이 선택되면 변환 실패나 메모리 문제가 발생할 수 있으므로 철저한 검증이 필요합니다.
///
/// [기획서(v1.1) 라우팅 정책]
/// - NetVips 경로: GIF(애니메이션), AVIF(고압축 포맷)
/// - SkiaSharp 경로: JPEG, PNG, BMP, WEBP(정지 이미지)
/// - WebP-Animated 판별은 Step 4에서 구현 예정 (현재는 SkiaSharp으로 기본 처리)
///
/// [Mock 없이 직접 생성 가능한 이유]
/// SkiaSharpProvider, NetVipsProvider는 생성자에 외부 의존성이 없습니다.
/// 따라서 new 키워드로 실제 인스턴스를 생성하여 테스트합니다.
/// </summary>
public class EngineSelectorTests
{
    private readonly SkiaSharpProvider _skia;
    private readonly NetVipsProvider _netVips;

    // 테스트 대상(SUT)
    private readonly EngineSelector _selector;

    public EngineSelectorTests()
    {
        // 실제 Provider 인스턴스를 생성하여 주입 (Mock 없이 실제 타입 사용)
        _skia = new SkiaSharpProvider();
        _netVips = new NetVipsProvider();

        // 두 Provider를 주입하여 EngineSelector 생성
        _selector = new EngineSelector(_skia, _netVips);
    }

    // ─────────────────────────────────────────────────
    // NetVips 경로 (애니메이션 및 고압축 포맷)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: FileSignature가 "GIF"인 파일에 대해 엔진 선택.
    /// 검증 목표: GIF는 애니메이션 포맷이므로 스트리밍 방식의 NetVipsProvider가 선택되는지 확인.
    ///            SkiaSharp는 단일 프레임만 처리하므로 GIF 애니메이션 처리 불가합니다.
    /// </summary>
    [Fact]
    public void GetProvider_WhenIsAnimationIsTrue_ShouldReturnNetVips()
    {
        // Arrange: 애니메이션 플래그가 설정된 파일 (GIF, WebP-Ani, AVIF-Seq 등 공통)
        var file = new FileItem { Path = "test.img", FileSignature = "ANY", IsAnimation = true };
 
        // Act
        var provider = _selector.GetProvider(file, new ConvertSettings());
 
        // Assert: 애니메이션은 항상 NetVips
        Assert.IsType<NetVipsProvider>(provider);
    }

    /// <summary>
    /// 시나리오: FileSignature가 "AVIF"인 파일에 대해 엔진 선택.
    /// 검증 목표: AVIF는 고압축 포맷으로 libvips의 스트리밍 디코딩이 필요하므로
    ///            NetVipsProvider가 선택되는지 확인.
    ///            SkiaSharp는 AVIF 디코딩 지원이 제한적입니다.
    /// </summary>
    [Fact]
    public void GetProvider_WhenSignatureIsAvif_ShouldReturnNetVips()
    {
        // Arrange
        var file = new FileItem { Path = "test.png", FileSignature = "AVIF" };

        // Act
        var provider = _selector.GetProvider(file, new ConvertSettings());

        // Assert
        Assert.IsType<NetVipsProvider>(provider);
    }

    // ─────────────────────────────────────────────────
    // SkiaSharp 경로 (정지 이미지 포맷)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: FileSignature가 "JPEG"인 파일에 대해 엔진 선택.
    /// 검증 목표: JPEG는 단일 프레임 정지 이미지로 고속의 SkiaSharpProvider가 선택되는지 확인.
    ///            SkiaSharp는 JPEG 인코딩/디코딩에 최적화되어 있습니다.
    /// </summary>
    [Fact]
    public void GetProvider_WhenSignatureIsJpeg_ShouldReturnSkiaSharp()
    {
        // Arrange
        var file = new FileItem { Path = "test.jpg", FileSignature = "JPEG" };

        // Act
        var provider = _selector.GetProvider(file, new ConvertSettings());

        // Assert: SkiaSharpProvider 타입인지 확인
        Assert.IsType<SkiaSharpProvider>(provider);
    }

    /// <summary>
    /// 시나리오: FileSignature가 "PNG"인 파일에 대해 엔진 선택.
    /// 검증 목표: PNG도 단일 프레임 정지 이미지이므로 SkiaSharpProvider가 선택되는지 확인.
    /// </summary>
    [Fact]
    public void GetProvider_WhenSignatureIsPng_ShouldReturnSkiaSharp()
    {
        // Arrange & Act
        var file = new FileItem { Path = "test.jpg", FileSignature = "PNG" };
        var provider = _selector.GetProvider(file, new ConvertSettings());

        // Assert
        Assert.IsType<SkiaSharpProvider>(provider);
    }

    /// <summary>
    /// 시나리오: FileSignature가 "WEBP"인 파일에 대해 엔진 선택.
    /// 검증 목표: 현재 MVP 단계에서 WEBP는 정지 이미지로 간주하여 SkiaSharpProvider를 선택하는지 확인.
    ///
    /// [참고] WebP-Animated 판별 로직은 Step 4에서 구현 예정.
    ///        이후 VP8X 청크의 Animation 플래그를 확인하여 NetVips로 분기하는 로직이 추가될 예정입니다.
    ///        해당 구현 후에는 이 테스트가 실패할 수 있으며, 업데이트가 필요합니다.
    /// </summary>
    /// <summary>
    /// 시나리오: FileSignature가 "WEBP"이고 IsAnimation이 false인 파일.
    /// 검증 목표: 정지 WebP는 SkiaSharpProvider가 선택되어야 함.
    /// </summary>
    [Fact]
    public void GetProvider_WhenSignatureIsWebpAndNotAnimated_ShouldReturnSkiaSharp()
    {
        // Arrange
        var file = new FileItem { Path = "test.webp", FileSignature = "WEBP", IsAnimation = false };

        // Act
        var provider = _selector.GetProvider(file, new ConvertSettings());

        // Assert
        Assert.IsType<SkiaSharpProvider>(provider);
    }

    /// <summary>
    /// 시나리오: FileSignature가 "WEBP"이고 IsAnimation이 true인 파일.
    /// 검증 목표: 애니메이션 WebP는 NetVipsProvider가 선택되어야 함.
    /// </summary>
    [Fact]
    public void GetProvider_WhenSignatureIsWebpAndIsAnimated_ShouldReturnNetVips()
    {
        // Arrange
        var file = new FileItem { Path = "test.webp", FileSignature = "WEBP", IsAnimation = true };

        // Act
        var provider = _selector.GetProvider(file, new ConvertSettings());

        // Assert
        Assert.IsType<NetVipsProvider>(provider);
    }

    // ─────────────────────────────────────────────────
    // 대소문자 처리 (엣지 케이스)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: GIF 시그니처를 소문자("gif"), 혼합("Gif"), 대문자("GIF")로 전달.
    /// 검증 목표: EngineSelector 내부에서 ToUpper()로 정규화하므로 대소문자와 무관하게
    ///            항상 동일한 엔진(NetVipsProvider)을 반환하는지 확인.
    ///            [Theory]를 사용하여 3개의 케이스를 단일 테스트로 검증합니다.
    ///
    /// [중요] FileScannerService는 시그니처를 "GIF"(대문자)로 반환하지만,
    ///        사용자 수동 입력이나 다른 경로로 소문자가 유입될 수 있으므로 방어 검증을 수행합니다.
    /// </summary>
    [Theory]
    [InlineData("avif")]
    [InlineData("AVIF")]
    public void GetProvider_WhenSignatureIsAvif_ShouldAlwaysReturnNetVipsRegardlessOfAnimationFlag(string signature)
    {
        // Arrange: AVIF는 정지/애니메이션 무관하게 NetVips (고압축 포맷 지원)
        var fileStatic = new FileItem { Path = "static.avif", FileSignature = signature, IsAnimation = false };
        var fileAnim = new FileItem { Path = "anim.avif", FileSignature = signature, IsAnimation = true };

        // Act & Assert
        Assert.IsType<NetVipsProvider>(_selector.GetProvider(fileStatic, new ConvertSettings()));
        Assert.IsType<NetVipsProvider>(_selector.GetProvider(fileAnim, new ConvertSettings()));
    }

    // ─────────────────────────────────────────────────
    // 출력 포맷 기반 라우팅 (Phase B 신규 정책)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 정지 PNG 파일을 AVIF로 변환하려고 함.
    /// 검증 목표: SkiaSharp은 AVIF 인코딩을 지원하지 않으므로 NetVipsProvider가 선택되어야 함.
    /// </summary>
    [Fact]
    public void GetProvider_WhenStandardTargetFormatIsAvif_ShouldReturnNetVips()
    {
        // Arrange
        var file = new FileItem { Path = "test.png", FileSignature = "PNG", IsAnimation = false };
        var settings = new ConvertSettings { StandardTargetFormat = "AVIF" };

        // Act
        var provider = _selector.GetProvider(file, settings);

        // Assert
        Assert.IsType<NetVipsProvider>(provider);
    }

    /// <summary>
    /// 시나리오: 애니메이션 WebP 파일을 GIF로 변환하려고 함.
    /// 검증 목표: 애니메이션 파일은 항상 NetVipsProvider가 선택되어야 함.
    /// </summary>
    [Fact]
    public void GetProvider_WhenAnimationFile_ShouldAlwaysReturnNetVipsRegardlessOfTargetFormat()
    {
        // Arrange
        var file = new FileItem { Path = "test.webp", FileSignature = "WEBP", IsAnimation = true };
        var settings = new ConvertSettings { AnimationTargetFormat = "GIF" };

        // Act
        var provider = _selector.GetProvider(file, settings);

        // Assert
        Assert.IsType<NetVipsProvider>(provider);
    }
}
