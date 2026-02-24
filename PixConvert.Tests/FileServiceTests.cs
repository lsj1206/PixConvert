using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq; // 외부 인터페이스를 '가짜(Mock)'로 흉내 내기 위한 라이브러리
using PixConvert.Services;
using Xunit;

namespace PixConvert.Tests;

/// <summary>
/// FileService(파일 I/O 및 시그니처 분석) 로직을 검증하는 테스트 클래스입니다.
/// IDisposable을 상속받아 테스트가 끝난 후 찌꺼기 파일(임시 폴더)들을 청소(Dispose)하는 역할을 갖습니다.
/// </summary>
public class FileServiceTests : IDisposable
{
    private readonly FileService _fileService;

    // 로깅(Serilog) 기능을 흉내 내기 위한 "가짜 로거" 객체입니다.
    // 만약 진짜 로거를 넣으면 테스트할 때마다 쓸데없는 로그 파일이 하드에 쌓이기 때문에 이렇게 가짜 대역을 씁니다.
    private readonly Mock<ILogger<FileService>> _mockLogger;
    private readonly Mock<ILanguageService> _mockLanguage;
    // 테스트 파일이 만들어질 임시 쓰레기통 폴더 경로입니다.
    private readonly string _tempDirectory;

    public FileServiceTests()
    {
        // 1. Mock(대역) 배우를 캐스팅합니다.
        _mockLogger = new Mock<ILogger<FileService>>();
        _mockLanguage = new Mock<ILanguageService>();

        // 언어 서비스가 문자열 키를 받으면 그대로 반환하도록 세팅 (로그 템플릿 검증 유지)
        _mockLanguage.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        // 2. 대역 배우(Logger, LanguageService)를 넣어서, 오로지 "FileService"만 순수하게 테스트되도록 환경을 격리합니다.
        _fileService = new FileService(_mockLogger.Object, _mockLanguage.Object);

        // 3. 테스트끼리 파일이 엉키지 않게 무작위(Guid) 이름의 임시 폴더를 C드라이브 Temp 영역에 생성합니다.
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PixConvertTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    // Task를 반환하는 이유는 FileService의 메서드가 await(비동기) 방식이기 때문입니다.
    [Fact]
    public async Task AnalyzeSignatureAsync_GivenRealJpgByteHeader_ShouldReturnJpg()
    {
        // Arrange (준비)
        string fakePngPath = Path.Combine(_tempDirectory, "fake_image.png"); // 확장자는 png인데
        byte[] jpgBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00 }; // 진짜 알맹이는 JPG인 바이트를 파일에 씁니다.
        File.WriteAllBytes(fakePngPath, jpgBytes);

        // Act (실행)
        // 우리가 만든 서비스가 과연 속임수에 안 넘어가고 진짜 정보를 뱉어내는지 확인합니다.
        string result = await _fileService.AnalyzeSignatureAsync(fakePngPath);

        // Assert (검증)
        // 파일의 '이름'이 아니라 '바이트 헤더'를 읽었으므로 반드시 "JPEG"가 나와야 합니다.
        Assert.Equal("JPEG", result);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenFakeExtensionButRealPngHeader_ShouldReturnPng()
    {
        // Arrange (준비)
        string fakeJpgPath = Path.Combine(_tempDirectory, "fake_image.jpg"); // 이번엔 반대로 속입니다.
        byte[] pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG의 고유 매직 넘버
        File.WriteAllBytes(fakeJpgPath, pngBytes);

        // Act (실행)
        string result = await _fileService.AnalyzeSignatureAsync(fakeJpgPath);

        // Assert (검증)
        Assert.Equal("PNG", result);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenEmptyFile_ShouldReturnDash()
    {
        // Arrange
        // 만약 헤더 바이트조차 없는 0 byte짜리 빈 깡통 파일을 넣으면 우리 앱이 뻗을까요?
        string emptyPath = Path.Combine(_tempDirectory, "empty.txt");
        File.WriteAllText(emptyPath, "");

        // Act
        string result = await _fileService.AnalyzeSignatureAsync(emptyPath);

        // Assert
        // 뻗는(에러가 나는) 대신 안전하게 알 수 없다는 표식인 "-" 를 반환해야 훌륭한 로직입니다.
        Assert.Equal("-", result);
    }

    [Fact]
    public async Task AnalyzeSignatureAsync_GivenNonExistentFile_ShouldReturnDashAndLogError()
    {
        // Act
        // 아예 세상에 없는 존재하지 않는 폴더의 경로를 찌릅니다.
        string result = await _fileService.AnalyzeSignatureAsync("C:\\NonExistentPath\\Fake.jpg");

        // Assert 1: 에러를 내지 않고 안전하게 "-" 기호를 띄우는지 확인합니다.
        Assert.Equal("-", result);

        // Assert 2 (Moq의 강력한 기능):
        // 가짜 대역인 _mockLogger가 "Log 메서드를 Error 수준(LogLevel.Error)으로 최소 한 번(Times.Once) 호출받았는가?"를 취조합니다.
        // 이것으로 FileService 로직 안의 "catch 영역"이 정상 작동했음을 수학적으로 입증할 수 있습니다.
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error), // 에러 레벨로
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),                     // 어떤 이유든 Exception 객체와 함께
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once); // 단 1번 호출되었는가?
    }

    /// <summary>
    /// 단위 테스트 프레임워크가 1개의 테스트(Fact)를 끝낼 때마다 이 함수를 호출해 줍니다.
    /// 여기서 우리가 하드디스크 C드라이브에 만들어뒀던 쓰레기 폴더들을 삭제해 방청소를 합니다.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
