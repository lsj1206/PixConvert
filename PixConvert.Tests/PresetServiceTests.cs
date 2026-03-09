using PixConvert.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using PixConvert.Models;
using PixConvert.Services;
using System.IO;
using System.Linq;

namespace PixConvert.Tests;

/// <summary>
/// PresetService(변환 프리셋 설정 관리) 로직을 검증하는 테스트 클래스입니다.
///
/// [테스트 범위]
/// 1. 파일 구조 유효성 검증 (ValidPresetFile): 프리셋 리스트 초기화, 기본값 복원
/// 2. 데이터 유효성 검증 (ValidPresetData): 품질 범위, 포맷 문자열 허용 여부
/// 3. CRUD 연산 (AddPreset, RemovePreset, RenamePreset, CopyPreset)
///
/// [Mock 구성]
/// - ILogger: 로그 출력을 억제 (실제 파일에 기록되지 않음)
/// - ILanguageService: GetString(key)이 key 자체를 반환하도록 설정
///   → 실제 XAML 리소스 없이도 로그 키 기반 로직을 테스트 가능하게 합니다.
/// </summary>
public class PresetServiceTests
{
    private readonly Mock<ILogger<PresetService>> _loggerMock;
    private readonly Mock<ILanguageService> _languageMock;
    private readonly Mock<ISnackbarService> _snackbarMock;

    // 테스트 대상(SUT)
    private readonly PresetService _presetService;

    public PresetServiceTests()
    {
        _loggerMock = new Mock<ILogger<PresetService>>();
        _languageMock = new Mock<ILanguageService>();
        _snackbarMock = new Mock<ISnackbarService>();

        // GetString()이 키 문자열을 그대로 반환하게 설정.
        // 실제 로그 메시지를 검증하지 않아도 로직 흐름 검증에 영향을 주지 않습니다.
        _languageMock.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);

        // PresetService는 생성 시 내부적으로 설정 파일 경로를 결정합니다.
        // 파일 I/O 없이도 ValidPreset* 메서드와 CRUD 메서드는 테스트 가능합니다.
        _presetService = new PresetService(_loggerMock.Object, _languageMock.Object, _snackbarMock.Object);
    }

    // ─────────────────────────────────────────────────
    // ValidPresetFile 테스트 (파일 구조 무결성 검증)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: Config.Presets가 null인 상태(파일 손상 시뮬레이션).
    /// 검증 목표: null Presets를 새 리스트로 초기화하고, 기본 프리셋을 자동 추가하며,
    ///            "구조가 수정되었음"을 나타내는 false를 반환하는지 확인.
    ///            → 설정 파일이 손상되어도 앱이 기본값으로 복구되어 실행되어야 합니다.
    /// </summary>
    [Fact]
    public void ValidPresetFile_WhenPresetsIsNull_ShouldCreateListAndReturnFalse()
    {
        // Arrange: 손상된 상태 시뮬레이션 (null! = null 허용 강제)
        _presetService.Config.Presets = null!;

        // Act: 구조 무결성 검증 실행
        var result = _presetService.ValidPresetFile();

        // Assert: 수정이 발생했으므로 false 반환
        Assert.False(result);
        // Assert: Presets가 null이 아닌 새 리스트로 초기화되었는지 확인
        Assert.NotNull(_presetService.Config.Presets);
        // Assert: 기본 프리셋 1개("Preset_1")가 자동 생성되었는지 확인
        Assert.Single(_presetService.Config.Presets);
        // Assert: LastSelectedPresetName이 기본 프리셋으로 복원되었는지 확인
        Assert.Equal("Preset_1", _presetService.Config.LastSelectedPresetName);
    }

    /// <summary>
    /// 시나리오: Presets 리스트는 존재하지만 비어있는 상태(모든 프리셋 삭제된 경우).
    /// 검증 목표: 빈 리스트에 기본 프리셋을 자동 추가하고, false를 반환하는지 확인.
    ///            → 프리셋이 0개이면 변환 설정이 불가능하므로 기본 프리셋을 강제 생성해야 합니다.
    /// </summary>
    [Fact]
    public void ValidPresetFile_WhenPresetsIsEmpty_ShouldAddDefaultPresetAndReturnFalse()
    {
        // Arrange: 프리셋 리스트를 완전히 빈 상태로 만들기
        _presetService.Config.Presets.Clear();

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert: 수정(기본 프리셋 추가)이 발생했으므로 false 반환
        Assert.False(result);
        // Assert: 빈 리스트에 기본 프리셋 1개가 추가되었는지 확인
        Assert.Single(_presetService.Config.Presets);
        // Assert: 추가된 기본 프리셋이 LastSelected로 설정되었는지 확인
        Assert.Equal("Preset_1", _presetService.Config.LastSelectedPresetName);
    }

    /// <summary>
    /// 시나리오: LastSelectedPresetName이 프리셋 목록에 존재하지 않는 경우.
    ///            (예: 사용자가 수동으로 settings.json을 편집하여 이름을 잘못 변경한 경우)
    /// 검증 목표: 존재하지 않는 프리셋명을 목록의 첫 번째 프리셋명으로 자동 복구하는지 확인.
    /// </summary>
    [Fact]
    public void ValidPresetFile_WhenLastSelectedPresetNameNotFound_ShouldUpdateAndReturnFalse()
    {
        // Arrange: 유효한 프리셋은 "MyPreset"인데 LastSelected는 존재하지 않는 "NonExistentPreset"
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "MyPreset" });
        _presetService.Config.LastSelectedPresetName = "NonExistentPreset"; // 잘못된 값

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert: 복구가 발생했으므로 false 반환
        Assert.False(result);
        // Assert: LastSelectedPresetName이 존재하는 첫 번째 프리셋("MyPreset")으로 정정되었는지 확인
        Assert.Equal("MyPreset", _presetService.Config.LastSelectedPresetName);
    }

    /// <summary>
    /// 시나리오: 프리셋 목록에 유효한 프리셋이 있고 LastSelectedPresetName도 정확히 일치.
    /// 검증 목표: 모든 구조가 정상일 때 수정 없이 true를 반환하는지 확인.
    ///            → "수정이 없었다 = 파일을 다시 저장할 필요 없다"는 의미입니다.
    /// </summary>
    [Fact]
    public void ValidPresetFile_WhenConfigIsValid_ShouldReturnTrue()
    {
        // Arrange: 완전히 정상적인 상태 구성
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "ValidPreset" });
        _presetService.Config.LastSelectedPresetName = "ValidPreset"; // 일치

        // Act
        var result = _presetService.ValidPresetFile();

        // Assert: 수정 없음 → true 반환
        Assert.True(result);
    }

    // ─────────────────────────────────────────────────
    // ValidPresetData 테스트 (프리셋 데이터 값 유효성 검증)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: Quality 값을 다양한 범위로 설정하여 유효성 검사 통과 여부를 확인.
    /// 검증 목표: 품질(Quality)은 1~100 범위만 허용하며, 경계값(0, 101)은 거부하는지 확인.
    ///            [Theory] + [InlineData]를 사용하여 하나의 테스트 메서드로 3가지 케이스를 검증합니다.
    /// </summary>
    [Theory]
    [InlineData(0, false)]   // 최솟값 미만 → 유효하지 않음
    [InlineData(101, false)] // 최댓값 초과 → 유효하지 않음
    [InlineData(50, true)]   // 정상 범위(1~100) → 유효함
    public void ValidPresetData_ShouldValidateQuality(int quality, bool expected)
    {
        // Arrange: 테스트할 품질 값을 가진 프리셋 설정
        _presetService.Config.Presets.Clear();
        var settings = new ConvertSettings { Quality = quality };
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = settings });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act: 데이터 유효성 검증 실행 (out 매개변수로 에러 키도 함께 반환)
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert: 예상값과 실제 반환값이 일치하는지 확인
        Assert.Equal(expected, result);
        // Assert: 유효하지 않을 때만 에러 키가 설정되는지 확인
        if (!expected)
            Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
        else
            Assert.True(string.IsNullOrEmpty(errorKey));
    }

    /// <summary>
    /// 시나리오: StandardTargetFormat에 다양한 포맷 문자열을 설정하여 허용/거부 여부 확인.
    /// 검증 목표: 지원 포맷(JPEG, AVIF 등)만 허용하고, 빈 문자열이나 미지원 포맷("INVALID")은
    ///            거부하는 화이트리스트 방식의 유효성 검사가 작동하는지 확인.
    /// </summary>
    [Theory]
    [InlineData("INVALID", false)] // 지원하지 않는 포맷 → 거부
    [InlineData("JPEG", true)]     // 정상 지원 포맷 → 허용
    [InlineData("Avif", true)]     // 대소문자 혼합도 허용 (대소문자 무시 비교)
    [InlineData("", false)]        // 빈 문자열 → 거부
    public void ValidPresetData_ShouldValidateStandardTargetFormat(string format, bool expected)
    {
        // Arrange: 테스트할 포맷 문자열로 프리셋 구성 (AnimationTargetFormat은 유효값 "GIF" 고정)
        _presetService.Config.Presets.Clear();
        var settings = new ConvertSettings { StandardTargetFormat = format, AnimationTargetFormat = "GIF" };
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = settings });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// 시나리오: 프리셋의 Settings 객체 자체가 null인 경우.
    /// 검증 목표: Settings가 null이면 즉시 false를 반환하고 에러 키를 설정하는지 확인.
    ///            → NullReferenceException 없이 안전하게 처리되는 방어 코드를 검증합니다.
    /// </summary>
    [Fact]
    public void ValidPresetData_WhenSettingsIsNull_ShouldReturnFalse()
    {
        // Arrange: Settings가 null인 손상된 프리셋
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "TestPreset", Settings = null! });
        _presetService.Config.LastSelectedPresetName = "TestPreset";

        // Act
        var result = _presetService.ValidPresetData(out string errorKey);

        // Assert: null Settings → 즉시 false 반환
        Assert.False(result);
        // Assert: 에러 키가 올바르게 설정되었는지 확인
        Assert.Equal("Msg_Error_ConfigInvalid", errorKey);
    }

    // ─────────────────────────────────────────────────
    // CRUD 테스트 (AddPreset, RemovePreset, RenamePreset, CopyPreset)
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 시나리오: 새로운 이름으로 프리셋 추가.
    /// 검증 목표: AddPreset 호출 후 프리셋 수가 1 증가하고, 새 프리셋이 목록에 존재하는지 확인.
    /// </summary>
    [Fact]
    public void AddPreset_WhenNewName_ShouldIncreaseCount()
    {
        // Arrange: 현재 프리셋 수 기록 (기본 초기화 후 Preset_1이 있을 수 있음)
        int before = _presetService.Config.Presets.Count;

        // Act: 새 이름으로 프리셋 추가
        _presetService.AddPreset("NewPreset", new ConvertSettings());

        // Assert: 수가 1 증가했는지 확인
        Assert.Equal(before + 1, _presetService.Config.Presets.Count);
        // Assert: 추가된 프리셋이 목록에서 찾아지는지 확인
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "NewPreset");
    }

    /// <summary>
    /// 시나리오: 이미 존재하는 이름으로 프리셋 추가 시도.
    /// 검증 목표: 중복 이름은 추가를 거부하여 프리셋 수가 변경되지 않는지 확인.
    ///            → 같은 이름의 프리셋이 2개 생기는 것을 방지합니다.
    /// </summary>
    [Fact]
    public void AddPreset_WhenDuplicateName_ShouldNotAdd()
    {
        // Arrange: "Existing" 이름의 프리셋 1개만 있는 상태
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Existing" });
        int before = _presetService.Config.Presets.Count; // = 1

        // Act: 동일한 이름으로 추가 시도
        _presetService.AddPreset("Existing", new ConvertSettings());

        // Assert: 중복이므로 수 변화 없음 (여전히 1개)
        Assert.Equal(before, _presetService.Config.Presets.Count);
    }

    /// <summary>
    /// 시나리오: 목록에 존재하는 프리셋을 이름으로 제거.
    /// 검증 목표: 지정한 프리셋이 목록에서 사라지고, 나머지 프리셋은 유지되는지 확인.
    /// </summary>
    [Fact]
    public void RemovePreset_WhenExists_ShouldDecreaseCount()
    {
        // Arrange: 2개의 프리셋이 있는 상태
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "ToRemove" });   // 삭제 대상
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Remaining" }); // 남아야 할 것

        // Act: "ToRemove" 프리셋 제거
        _presetService.RemovePreset("ToRemove");

        // Assert: "Remaining"만 남아 1개인지 확인
        Assert.Equal(1, _presetService.Config.Presets.Count);
        // Assert: 지정한 프리셋이 실제로 목록에서 사라졌는지 추가 확인
        Assert.DoesNotContain(_presetService.Config.Presets, p => p.Name == "ToRemove");
    }

    /// <summary>
    /// 시나리오: 존재하지 않는 이름으로 RemovePreset 호출.
    /// 검증 목표: 없는 이름을 제거해도 예외(Exception)가 발생하지 않고 안전하게 종료되는지 확인.
    ///            Record.Exception()을 사용하여 예외 발생 여부를 포착합니다.
    /// </summary>
    [Fact]
    public void RemovePreset_WhenNotExists_ShouldNotThrow()
    {
        // Arrange: "Only" 프리셋 1개만 있는 상태
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Only" });

        // Act & Assert: 존재하지 않는 이름 제거 시 예외 없이 통과해야 함
        //   Record.Exception()은 람다 내부에서 예외가 발생했을 때 그 예외 객체를 반환합니다.
        //   예외가 없으면 null을 반환합니다.
        var ex = Record.Exception(() => _presetService.RemovePreset("NonExistent"));
        Assert.Null(ex); // 예외가 없어야 함

        // Assert: 기존 목록은 변경되지 않고 그대로 1개 유지
        Assert.Single(_presetService.Config.Presets);
    }

    /// <summary>
    /// 시나리오: 존재하는 프리셋의 이름을 새 이름으로 변경.
    /// 검증 목표: 변경 후 새 이름이 목록에 존재하고, 기존 이름은 사라졌는지 확인.
    /// </summary>
    [Fact]
    public void RenamePreset_WhenValid_ShouldChangeName()
    {
        // Arrange: "OldName" 프리셋을 현재 선택 상태로 준비
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "OldName" });
        _presetService.Config.LastSelectedPresetName = "OldName";

        // Act: "OldName" → "NewName"으로 변경
        _presetService.RenamePreset("OldName", "NewName");

        // Assert: "NewName"이 목록에 추가되었는지 확인
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "NewName");
        // Assert: "OldName"이 목록에서 삭제되었는지 확인
        Assert.DoesNotContain(_presetService.Config.Presets, p => p.Name == "OldName");
    }

    /// <summary>
    /// 시나리오: 현재 선택된(LastSelected) 프리셋의 이름을 변경.
    /// 검증 목표: 프리셋 이름이 변경될 때 LastSelectedPresetName도 자동으로 동기화되는지 확인.
    ///            → 이름 변경 후에도 "현재 선택된 프리셋"이 올바르게 인식되어야 합니다.
    /// </summary>
    [Fact]
    public void RenamePreset_WhenSameAsLastSelected_ShouldUpdateLastSelected()
    {
        // Arrange: 현재 선택된 프리셋 이름이 "Active"
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "Active" });
        _presetService.Config.LastSelectedPresetName = "Active";

        // Act: "Active" → "ActiveRenamed"으로 이름 변경
        _presetService.RenamePreset("Active", "ActiveRenamed");

        // Assert: LastSelectedPresetName도 함께 "ActiveRenamed"로 업데이트되었는지 확인
        Assert.Equal("ActiveRenamed", _presetService.Config.LastSelectedPresetName);
    }

    /// <summary>
    /// 시나리오: 이미 존재하는 이름으로 RenamePreset 시도.
    /// 검증 목표: 중복 이름으로의 변경은 거부되어 원래 이름("A")이 그대로 유지되는지 확인.
    ///            → 이름 충돌로 인한 프리셋 덮어쓰기를 방지합니다.
    /// </summary>
    [Fact]
    public void RenamePreset_WhenNewNameAlreadyExists_ShouldNotRename()
    {
        // Arrange: "A"와 "B" 두 개의 프리셋이 있는 상태
        _presetService.Config.Presets.Clear();
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "A" });
        _presetService.Config.Presets.Add(new ConvertPreset { Name = "B" });

        // Act: "A"를 이미 존재하는 "B"로 변경 시도
        _presetService.RenamePreset("A", "B");

        // Assert: "A"는 변경 거부로 인해 여전히 목록에 존재해야 함
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "A");
    }

    /// <summary>
    /// 시나리오: 기존 프리셋을 새 이름으로 복사(CopyPreset).
    /// 검증 목표: 복사 후 프리셋 수가 1 증가하고, 새 이름의 프리셋이 목록에 추가되는지 확인.
    /// </summary>
    [Fact]
    public void CopyPreset_WhenValid_ShouldAddNewPreset()
    {
        // Arrange: "Original" 프리셋 1개 준비 (Quality=90)
        _presetService.Config.Presets.Clear();
        var original = new ConvertPreset { Name = "Original", Settings = new ConvertSettings { Quality = 90 } };
        _presetService.Config.Presets.Add(original);

        // Act: "Original"을 "Original_Copy"라는 이름으로 복사
        _presetService.CopyPreset("Original", "Original_Copy");

        // Assert: 복사 후 총 2개가 되었는지 확인
        Assert.Equal(2, _presetService.Config.Presets.Count);
        // Assert: 복사된 프리셋이 목록에 존재하는지 확인
        Assert.Contains(_presetService.Config.Presets, p => p.Name == "Original_Copy");
    }

    /// <summary>
    /// 시나리오: 복사된 프리셋의 원본 객체 변경 후 복사본 영향도 확인.
    /// 검증 목표: CopyPreset이 Shallow Copy가 아닌 Deep Copy(값 독립 복사)를 수행하는지 확인.
    ///            원본의 Settings.Quality를 변경해도 복사본은 원래 값을 그대로 유지해야 합니다.
    ///            → 복사본 변경이 원본에 영향을 주는 참조 공유 버그를 방지합니다.
    /// </summary>
    [Fact]
    public void CopyPreset_ShouldCreateDeepCopy_OriginalChangeDoesNotAffectCopy()
    {
        // Arrange: Quality=80인 "Source" 프리셋 준비
        _presetService.Config.Presets.Clear();
        var original = new ConvertPreset { Name = "Source", Settings = new ConvertSettings { Quality = 80 } };
        _presetService.Config.Presets.Add(original);

        // "Dest"라는 이름으로 복사 (이 시점에서 복사본의 Quality = 80)
        _presetService.CopyPreset("Source", "Dest");

        // Act: 원본의 품질을 80 → 50으로 변경
        original.Settings.Quality = 50;

        // Assert: 복사본("Dest")의 Quality는 여전히 초기값 80이어야 함 (Deep Copy 검증)
        //   만약 Shallow Copy였다면 복사본도 50으로 바뀌었을 것입니다.
        var copy = _presetService.Config.Presets.First(p => p.Name == "Dest");
        Assert.Equal(80, copy.Settings.Quality);
    }
}
