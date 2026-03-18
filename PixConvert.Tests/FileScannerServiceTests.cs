using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// FileScannerService(파일 I/O 및 시그니처 분석) 로직을 검증하는 테스트 클래스입니다.
///
/// [테스트 핵심 전략: 격리(Isolation)]
/// FileScannerService는 ILogger, ILanguageService 두 가지 외부 의존성을 가집니다.
/// 이 의존성들을 Moq 라이브러리로 만든 '가짜(Mock) 객체'로 교체하여,
/// 오직 FileScannerService 자체 로직만 순수하게 단위 테스트할 수 있게 합니다.
///
/// [IDisposable 패턴]
/// 각 테스트는 실제 파일을 디스크에 쓰고 읽으므로, 테스트 완료 후 Dispose()를
/// 호출하여 임시 폴더를 자동 삭제합니다 (xUnit이 자동 호출).
/// </summary>
public class FileScannerServiceTests : IDisposable
{
    private readonly FileScannerService _fileScannerService;

    // Moq로 생성한 '가짜 로거'. 실제 파일에 로그를 쓰지 않으며,
    // 나중에 Verify() 메서드로 "이 로거가 호출되었는가?"를 검사하는 탐정 역할을 합니다.
    private readonly Mock<ILogger<FileScannerService>> _mockLogger;
    private readonly Mock<ILanguageService> _mockLanguage;

    // 테스트마다 독립된 임시 디렉터리. Guid로 충돌 없는 고유 경로를 보장합니다.
    private readonly string _tempDirectory;

    public FileScannerServiceTests()
    {
        // --- 1. 가짜 의존성(Mock) 준비 ---
        _mockLogger = new Mock<ILogger<FileScannerService>>();
        _mockLanguage = new Mock<ILanguageService>();

        // 언어 서비스가 어떤 키를 받아도 그 키 문자열 자체를 반환하도록 설정.
        // 실제 XAML 리소스 없이도 로그 포맷 검증에 영향을 주지 않습니다.
        _mockLanguage.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        // --- 2. 테스트 대상(SUT) 생성 ---
        // 가짜 의존성을 주입하여 외부 환경(파일시스템, 로그)과 완전히 격리된 인스턴스를 생성합니다.
        _fileScannerService = new FileScannerService(_mockLogger.Object, _mockLanguage.Object);

        // --- 3. 파일 쓰기용 임시 디렉터리 생성 ---
        // Path.GetTempPath()는 OS의 임시 폴더(C:\Users\...\AppData\Local\Temp)를 반환합니다.
        // Guid를 붙여 테스트 병렬 실행 시에도 충돌을 방지합니다.
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixConvertTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    // ─────────────────────────────────────────────────
    // 기존 핵심 시나리오 테스트 (JPEG / PNG / 빈 파일 / 존재하지 않는 파일)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 확장자는 .png지만 실제 바이트 헤더는 JPEG인 "위장 파일".
    /// 검증 목표: FileScannerService가 확장자가 아닌 바이트 헤더를 기준으로 판별하는지 확인.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenRealJpgByteHeader_ShouldReturnJpg()
    {
        // Arrange: .png 확장자의 파일에 JPEG 매직 바이트(FF D8 FF)를 기록
        string fakePngPath = Path.Combine(_tempDirectory, "fake_image.png");
        byte[] jpgBytes = [0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00];
        File.WriteAllBytes(fakePngPath, jpgBytes);

        // Act: 위장 파일을 시그니처 분석에 투입
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(fakePngPath);

        // Assert: 확장자(.png)에 속지 않고 바이트 헤더 기반으로 "JPEG"를 반환해야 함
        Assert.Equal("JPEG", result);
        Assert.False(isAnim);
    }

    /// <summary>
    /// 시나리오: 확장자는 .jpg지만 실제 바이트 헤더는 PNG인 "역방향 위장 파일".
    /// 검증 목표: 양방향으로 바이트 헤더 우선 판별이 적용되는지 확인.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenFakeExtensionButRealPngHeader_ShouldReturnPng()
    {
        // Arrange: .jpg 확장자의 파일에 PNG 매직 시그니처(89 50 4E 47 0D 0A 1A 0A) 기록
        string fakeJpgPath = Path.Combine(_tempDirectory, "fake_image.jpg");
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        File.WriteAllBytes(fakeJpgPath, pngBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(fakeJpgPath);

        // Assert: 확장자(.jpg)에 속지 않고 "PNG"를 반환해야 함
        Assert.Equal("PNG", result);
        Assert.False(isAnim);
    }

    /// <summary>
    /// 시나리오: 헤더 바이트가 전혀 없는 0 바이트짜리 빈 파일.
    /// 검증 목표: 비어있는 파일을 읽어도 예외가 발생하지 않고 "-"(미지원/미식별)을 반환하는지 확인.
    ///            → 방어적 프로그래밍(Defensive Programming) 검증
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenEmptyFile_ShouldReturnDash()
    {
        // Arrange: 내용이 없는 빈 파일 생성
        string emptyPath = Path.Combine(_tempDirectory, "empty.txt");
        File.WriteAllText(emptyPath, "");

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(emptyPath);

        // Assert: 예외(Exception) 없이 "-"를 반환하여 안전한 폴백(Safe Fallback)을 보장해야 함
        Assert.Equal("-", result);
        Assert.False(isAnim);
    }

    /// <summary>
    /// 시나리오: 존재하지 않는 경로를 전달.
    /// 검증 목표 1: 예외가 아닌 "-"를 반환하는 방어 로직 확인.
    /// 검증 목표 2: [가장 중요] Moq Verify()를 사용하여 catch 블록 내부에서
    ///              LogError가 정확히 1회 호출되었음을 수학적으로 입증.
    ///              → "에러가 감지되면 반드시 로그를 남긴다"는 계약(Contract) 검증.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenNonExistentFile_ShouldReturnDashAndLogError()
    {
        // Act: 세상에 존재하지 않는 경로로 호출
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync("C:\\NonExistentPath\\Fake.jpg");

        // Assert 1: 예외 없이 안전하게 "-" 반환 확인
        Assert.Equal("-", result);
        Assert.False(isAnim);

        // Assert 2: Mock 객체가 Error 수준 로그를 정확히 1번 호출받았는지 검증.
        //   - It.Is<LogLevel>(l => l == LogLevel.Error): 반드시 Error 레벨이어야 함
        //   - It.IsAny<Exception>(): 어떤 예외든 상관없이 예외 객체가 전달되어야 함
        //   - Times.Once: 1번 이상도, 이하도 아닌 정확히 1번이어야 함
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    // ─────────────────────────────────────────────────
    // 지원 포맷 시그니처 테스트: JPEG, PNG, BMP, WEBP, AVIF, GIF
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: GIF89a 포맷 바이트 헤더를 가진 파일 (확장자는 .png로 위장).
    /// 검증 목표: GIF 매직 바이트(47 49 46 38)를 정확히 판별하는지 확인.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenGifHeader_ShouldReturnGif()
    {
        // Arrange: GIF89a 시그니처 = "GIF89a"의 ASCII 코드
        string path = Path.Combine(_tempDirectory, "fake.png");
        byte[] gifBytes = [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]; // GIF89a
        File.WriteAllBytes(path, gifBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert: 확장자(.png)가 아닌 바이트 기반으로 "GIF"를 반환해야 함
        Assert.Equal("GIF", result);
        Assert.True(isAnim); // GIF89a는 애니메이션 가능
    }

    /// <summary>
    /// 시나리오: BMP 포맷 바이트 헤더를 가진 파일 (확장자는 .jpg로 위장).
    /// 검증 목표: BMP 매직 바이트 "BM"(42 4D)을 정확히 판별하는지 확인.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenBmpHeader_ShouldReturnBmp()
    {
        // Arrange: BMP 시그니처 = "BM" (ASCII 0x42, 0x4D)
        string path = Path.Combine(_tempDirectory, "fake.jpg");
        byte[] bmpBytes = [0x42, 0x4D, 0x00, 0x00, 0x00, 0x00];
        File.WriteAllBytes(path, bmpBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert
        Assert.Equal("BMP", result);
        Assert.False(isAnim);
    }

    /// <summary>
    /// 시나리오: WebP 포맷 바이트 헤더를 가진 파일.
    /// 검증 목표: WebP의 복합 헤더 구조(RIFF....WEBP, 12바이트)를 올바르게 판별하고
    ///            ConvertSettingViewModel 기준의 대문자 "WEBP"로 반환하는지 확인.
    /// WebP 포맷 구조:
    ///   바이트 0~3  : "RIFF"  (52 49 46 46) → 컨테이너 포맷임을 알림
    ///   바이트 4~7  : 파일 크기 (임의)
    ///   바이트 8~11 : "WEBP"  (57 45 42 50) → 이 값으로 WebP임을 최종 식별
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenWebpHeader_ShouldReturnWebp()
    {
        // Arrange: RIFF....WEBP 구조의 최소 16바이트 데이터
        string path = Path.Combine(_tempDirectory, "fake.jpg");
        byte[] webpBytes = [
            0x52, 0x49, 0x46, 0x46, // RIFF
            0x00, 0x00, 0x00, 0x00, // 파일 크기 (임의)
            0x57, 0x45, 0x42, 0x50, // WEBP
            0x00, 0x00, 0x00, 0x00  // 추가 패딩
        ];
        File.WriteAllBytes(path, webpBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert
        Assert.Equal("WEBP", result);
        Assert.False(isAnim); // 기본 WEBP 헤더는 정지 이미지로 판별
    }

    /// <summary>
    /// 시나리오: 애니메이션 비트가 켜진 WebP VP8X 헤더를 가진 파일.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAnimatedWebpHeader_ShouldReturnWebpAndIsAnimationTrue()
    {
        // Arrange
        string path = Path.Combine(_tempDirectory, "animated.webp");
        byte[] webpBytes = new byte[32];
        // RIFF
        webpBytes[0] = 0x52; webpBytes[1] = 0x49; webpBytes[2] = 0x46; webpBytes[3] = 0x46;
        // WEBP
        webpBytes[8] = 0x57; webpBytes[9] = 0x45; webpBytes[10] = 0x42; webpBytes[11] = 0x50;
        // VP8X
        webpBytes[12] = 0x56; webpBytes[13] = 0x50; webpBytes[14] = 0x38; webpBytes[15] = 0x58;
        // Flags (Offset 20), Animation bit (0x02)
        webpBytes[20] = 0x02; 
        File.WriteAllBytes(path, webpBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert
        Assert.Equal("WEBP", result);
        Assert.True(isAnim);
    }

    /// <summary>
    /// 시나리오: AVIF 포맷 바이트 헤더(ftyp avif)를 가진 파일.
    /// 검증 목표: ISO Base Media File Format 기반의 AVIF 헤더를 판별하는지 확인.
    /// AVIF 포맷 구조:
    ///   바이트 0~3  : 박스 크기 (임의)
    ///   바이트 4~7  : "ftyp"  (66 74 79 70) → ISOBMFF 파일 타입 박스
    ///   바이트 8~11 : "avif"  (61 76 69 66) → AVIF 정지 이미지 식별자
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAvifHeader_ShouldReturnAvif()
    {
        // Arrange: 16바이트 버퍼에 AVIF 헤더를 수동으로 구성
        string path = Path.Combine(_tempDirectory, "fake.png");
        byte[] avifBytes = new byte[16];
        // ftyp 박스 타입 마커
        avifBytes[4] = 0x66; avifBytes[5] = 0x74; avifBytes[6] = 0x79; avifBytes[7] = 0x70; // "ftyp"
        // AVIF 식별자
        avifBytes[8] = 0x61; avifBytes[9] = 0x76; avifBytes[10] = 0x69; avifBytes[11] = 0x66; // "avif"
        File.WriteAllBytes(path, avifBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert
        Assert.Equal("AVIF", result);
        Assert.False(isAnim);
    }

    /// <summary>
    /// 시나리오: AVIF Sequence 포맷 헤더(ftyp avis)를 가진 파일. (애니메이션 AVIF)
    /// 검증 목표: 바이트 11이 's'(73 = avis)인 경우도 "AVIF"로 통일 판별하는지 확인.
    ///            avis(Animated VI deo Sequence)는 avif의 애니메이션 확장 포맷이므로
    ///            동일한 AVIF 그룹으로 처리해야 합니다.
    /// </summary>
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenAvisHeader_ShouldReturnAvif()
    {
        // Arrange: 'avif'(0x66)가 아닌 'avis'(0x73)로 끝나는 헤더
        string path = Path.Combine(_tempDirectory, "fake_avis.png");
        byte[] avisBytes = new byte[16];
        avisBytes[4] = 0x66; avisBytes[5] = 0x74; avisBytes[6] = 0x79; avisBytes[7] = 0x70; // "ftyp"
        avisBytes[8] = 0x61; avisBytes[9] = 0x76; avisBytes[10] = 0x69; avisBytes[11] = 0x73; // "avis" (s=0x73)
        File.WriteAllBytes(path, avisBytes);

        // Act
        var (result, isAnim) = await _fileScannerService.AnalyzeSignatureAsync(path);

        // Assert: avis도 AVIF 그룹으로 통일 처리 + 애니메이션 판별 확인
        Assert.Equal("AVIF", result);
        Assert.True(isAnim);
    }

    /// <summary>
    /// 시나리오: 실제 JPEG 바이트를 가진 유효한 파일을 CreateFileItemAsync()에 전달.
    /// 검증 목표: 크기(Size)와 시그니처(FileSignature)를 단 한 번의 파일 I/O로 동시에 추출하는지 확인.
    ///            이는 AnalyzeSignatureAsync()와 달리 FileItem 모델 객체를 반환하여
    ///            파일 메타데이터 전체를 한 번에 확보하는 "Single Touch" 패턴을 검증합니다.
    /// </summary>
    [Fact]
    public async Task CreateFileItemAsync_GivenValidJpegFile_ShouldReturnItemWithSizeAndSignature()
    {
        // Arrange: 실제 JFIF 헤더를 가진 최소 크기의 JPEG 파일 생성
        string path = Path.Combine(_tempDirectory, "real.jpg");
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01];
        File.WriteAllBytes(path, jpegBytes);

        // Act: FileItem 모델 객체 생성 (크기 + 시그니처를 한 번의 호출로 획득)
        var item = await _fileScannerService.CreateFileItemAsync(path);

        // Assert 1: null이 아닌 유효한 FileItem이 반환되어야 함
        Assert.NotNull(item);
        // Assert 2: Path가 올바르게 설정되었는지 확인
        Assert.Equal(path, item.Path);
        // Assert 3: 파일 바이트 수(Size)가 실제로 쓴 바이트 수와 정확히 일치해야 함
        Assert.Equal(jpegBytes.Length, item.Size);
        // Assert 4: 바이트 헤더 기반으로 시그니처가 "JPEG"로 판별되어야 함
        Assert.Equal("JPEG", item.FileSignature);
        // Assert 5: JPEG는 애니메이션이 아님 확인
        Assert.False(item.IsAnimation);
    }

    /// <summary>
    /// xUnit이 각 테스트 케이스 완료 후 자동으로 호출하는 정리(Cleanup) 메서드.
    /// 테스트 중 디스크에 생성한 임시 파일/폴더를 삭제하여 테스트 후 잔여물이 남지 않게 합니다.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
