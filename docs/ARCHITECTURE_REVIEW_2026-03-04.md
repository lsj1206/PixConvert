# PixConvert 구조 평가 (객관식 리뷰)

본 문서는 현재 코드베이스와 제공된 변경 이력 텍스트를 바탕으로 작성되었습니다.

## 근거로 확인한 코드 포인트
- DI 구성, 전역 예외 처리, 로깅 진입점: `src/App.xaml.cs`
- 상태 동기화 구조(메신저, AppStatus): `src/ViewModels/ViewModelBase.cs`, `src/ViewModels/MainViewModel.cs`
- 파일 추가/정렬/필터/변환 플로우: `src/ViewModels/SidebarViewModel.cs`
- 파일 분석 성능 전략(병렬도, WMI 기반 디스크 판별): `src/Services/FileAnalyzerService.cs`
- 테스트 레이어 정합성: `PixConvert.Tests/FileServiceTests.cs`, `PixConvert.Tests/SortingServiceTests.cs`

## 핵심 관찰 요약
1. 아키텍처는 MVVM + 서비스 분리 + DI + 상태 메신저 패턴으로, 확장 가능한 뼈대를 확보함.
2. 변환 핵심 기능은 아직 시뮬레이션 루프 기반(`Task.Delay`)이며, 설정 저장/적용 경로에 TODO가 남음.
3. 성능 개선 시도는 구체적이며(병렬 분석, 드라이브 타입별 병렬도), 운영 안정성 장치는 비교적 성숙함(전역 예외, Serilog).
4. 테스트 계층은 리팩토링 이후 정합성이 깨진 흔적이 있으며(`FileService` 참조), 신뢰 가능한 회귀 안전망으로 보기 어려움.

## 단기 우선 개선
1. 테스트 컴파일 정합성 복구(`FileServiceTests`를 현재 서비스 구조에 맞게 전면 개편).
2. 변환 파이프라인 실구현(Provider/Selector + ConvertSettings 실제 저장/적용 + 실패 복구/부분 성공 정책).
3. WMI 의존 경로에 대한 플랫폼/권한 실패 시나리오 통합 테스트 및 fallback 정책 문서화.
4. Main/Sidebar 간 직접 결합을 줄이기 위한 조립 루트 정리(팩토리 또는 composition root 강화).

