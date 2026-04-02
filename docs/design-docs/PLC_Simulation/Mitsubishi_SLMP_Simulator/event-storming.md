# Properties

status: verified

last_verified: 2026-04-02

# 이벤트 스토밍

## UC-001

- Command: `CreateSimulatorHost`, `FormShown`, `StartSimulatorHost`
- Event: `SimulatorHostCreated`, `SimulatorHostStarted`, `SimulatorHostStartFailed`
- Policy: `Program`에서 host를 조립한 뒤 `Form1`의 `Shown` 이벤트에서 기본 loopback 설정으로 host 시작을 시도한다.

## UC-002

- Command: `ReceiveBatchReadRequest`, `ParseSlmpFrame`, `ExecuteBatchRead`, `SendBatchReadResponse`
- Event: `SlmpRequestReceived`, `SlmpFrameParsed`, `BatchReadCompleted`, `BatchReadResponseSent`
- Policy: `SlmpRequestReceived` 후 3E Binary 형식이 아니면 `UC-004` 오류 흐름으로 전환한다.

## UC-003

- Command: `ReceiveBatchWriteRequest`, `ParseSlmpFrame`, `ExecuteBatchWrite`, `SendBatchWriteResponse`
- Event: `BatchWriteAccepted`, `DeviceMemoryUpdated`, `BatchWriteResponseSent`
- Policy: 쓰기 대상이 허용 범위를 벗어나면 메모리 반영 없이 오류 응답을 반환한다.

## UC-004

- Command: `MapProtocolError`, `SendErrorResponse`, `RecordConnectionFailure`
- Event: `InvalidFrameDetected`, `InvalidAddressDetected`, `ProtocolErrorMapped`, `ErrorResponseSent`, `ConnectionClosed`
- Policy: 파싱 실패나 주소 초과가 발생하면 정상 이벤트를 발행하지 않고 즉시 오류 응답을 생성한다.

## UC-005

- Command: `GetSimulatorHealth`, `UpdateWindowTitle`
- Event: `SimulatorHealthSnapshotCreated`
- Policy: 호스트 시작 전이면 상태를 `Stopped`, 포트 바인딩 완료 후면 `Running`으로 노출하고 WinForms window title에 상태와 포트를 반영한다.

## UC-006

- Command: `ProvideReplayCsvPath`, `LoadReplayCsv`, `ParseReplayColumns`, `BuildReplayScenario`
- Event: `ReplayCsvPathProvided`, `ReplayCsvLoaded`, `ReplayColumnsParsed`, `ReplayScenarioBuilt`, `ReplayScenarioBuildFailed`
- Policy: `time` 컬럼 존재 여부를 먼저 검증하고, 나머지 컬럼은 모두 register 주소로 해석한다.

## UC-007

- Command: `StartReplay`, `ScheduleReplayStep`, `ApplyReplayStep`, `WriteReplayRegisters`
- Event: `ReplayStarted`, `ReplayStepScheduled`, `ReplayStepApplied`, `ReplayRegistersUpdated`, `ReplayCompleted`
- Policy: `firstDataRow.time`을 기준 0ms로 정규화하고, 같은 시각의 row는 파일 순서대로 적용한다.

## UC-008

- Command: `ValidateReplaySchema`, `ValidateReplayTimeline`, `ValidateReplayValue`
- Event: `ReplaySchemaInvalid`, `ReplayTimelineInvalid`, `ReplayValueInvalid`, `ReplayRejected`
- Policy: replay 시작 전에 오류를 확정하고, `IDeviceMemoryStore`는 변경하지 않는다.

## UC-009

- Command: `ReceiveBatchReadRequest`, `ExecuteBatchRead`
- Event: `BatchReadCompleted`, `ReplayReflectedInReadResponse`
- Policy: replay와 SLMP 요청은 같은 memory store를 공유해야 하며, 읽기 시점의 최신 값이 반환되어야 한다.
