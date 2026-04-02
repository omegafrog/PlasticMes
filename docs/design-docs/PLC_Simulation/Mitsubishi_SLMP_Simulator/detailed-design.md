# Properties

status: verified

last_verified: 2026-04-02

# 상세 설계

## 디렉토리 구조

- 기존 라이브러리 `PlasticMes.MitsubishiSimulator/` 아래 구조를 유지한다.
- replay 확장 포인트는 `Application`, `Contracts`, `Adapters/Csv`, `Hosting`에 추가한다.
- 기존 `PlasticMesSolution/PlasticMesSolution.csproj`는 composition root 역할만 유지하며 `Program.cs`, `Form1.cs`에서 replay wiring만 담당한다.

## 포트

- 기존 유지: `IDeviceMemoryStore`, `ISlmpRequestHandler`, `IMitsubishiSimulatorHost`
- 신규: `IReplayScenarioLoader`
- 시그니처: `Task<ReplayScenarioLoadResult> LoadAsync(string csvPath, CancellationToken ct)`
- 신규: `IRegisterReplayController`
- 시그니처: `Task StartAsync(RegisterReplayScenario scenario, CancellationToken ct)`
- 시그니처: `Task StopAsync(CancellationToken ct)`
- 시그니처: `ReplayStatusSnapshot GetStatus()`

## 주요 인터페이스 시그니처 외 DTO

- `ReplayRegisterColumn(string HeaderName, DeviceAddress Address)`
- `ReplayStep(TimeSpan Offset, IReadOnlyList<DeviceWrite> Writes, int RowNumber)`
- `RegisterReplayScenario(string SourcePath, IReadOnlyList<ReplayRegisterColumn> Columns, IReadOnlyList<ReplayStep> Steps)`
- `ReplayScenarioLoadResult(bool IsSuccess, RegisterReplayScenario? Scenario, string? ErrorMessage)`
- `ReplayStatusSnapshot(bool IsRunning, int AppliedStepCount, int TotalStepCount, int? CurrentRowNumber, string? LastError)`

## 어댑터

- 신규 CSV 어댑터: `Adapters/Csv/CsvReplayScenarioLoader`
- 책임: CSV 파일 파싱, 헤더 검증, `time` 정규화, `DeviceWrite` 목록 생성
- 신규 replay 어댑터: `Hosting/RegisterReplayController`
- 책임: `TimeProvider` 또는 `Task.Delay` 기반으로 상대 시각까지 대기 후 `IDeviceMemoryStore.Write` 호출
- 기존 메모리 어댑터 유지: `Adapters/Memory/InMemoryDeviceMemoryStore`
- 정책: replay와 SLMP write가 동시에 접근하므로 현재 lock 기반 동시성 모델을 그대로 재사용한다.

## 주요 설계 결정

- `MitsubishiSimulatorHost`에는 CSV 파싱 책임을 넣지 않는다. TCP host는 네트워크 수명주기만 유지한다.
- CSV loader는 각 비-`time` 컬럼을 단건 `DeviceWrite`로 변환한다. 1차 구현은 range 병합 최적화를 하지 않는다.
- replay controller는 각 row를 절대 wall-clock이 아니라 상대 delay로 적용한다.
- 첫 row 적용 후 다음 row는 `nextOffset - currentOffset` 만큼 대기한다.
- replay controller는 취소 시 즉시 종료되어야 하며, host stop 시 함께 정리되어야 한다.
- 빈 셀은 허용하지 않고 replay 시작 전 validation error로 처리한다.

## UI / Composition

- `Program.cs`에서 shared `InMemoryDeviceMemoryStore`를 만들고 `SlmpRequestHandler`와 replay controller에 함께 주입한다.
- `Form1.cs`는 host 시작 후 CSV replay를 시작하거나, 최소한 replay 실패 상태를 title 또는 메시지로 노출한다.
- 입력 UI가 확정되지 않았으므로 1차는 파일 경로 주입 방식만 설계한다.

## 테스트 포인트

- 동적 컬럼 헤더 `M100`, `D200` 파싱 성공
- `time` 누락 시 로드 실패
- 첫 data row 기준 시간 정규화 검증
- 시간 역전 또는 음수 시간 reject
- 20ms 간격 replay 후 read 결과가 기대값으로 바뀌는지 검증
- replay 취소 후 추가 row가 적용되지 않는지 검증
- 기존 `UC-001`부터 `UC-005`까지 회귀 테스트 유지
