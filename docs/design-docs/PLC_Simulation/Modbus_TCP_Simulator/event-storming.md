# Properties

status: verified

last_verified: 2026-04-02

# 이벤트 스토밍

## UC-001

- Command: `CreateModbusHost`, `StartModbusHost`
- Event: `ModbusHostCreated`, `ModbusHostStarted`, `ModbusHostStartFailed`
- Policy: `Program.cs`는 기본값 `127.0.0.1:1502`, `unit-id=1`을 사용하고, bind 성공 전까지 상태는 `Starting`으로 유지한다.

## UC-002

- Command: `ReceiveReadRequest`, `DecodeMbapFrame`, `ExecuteRead`, `EncodeReadResponse`
- Event: `ModbusRequestReceived`, `ModbusFrameDecoded`, `MemoryReadCompleted`, `ReadResponseSent`
- Policy: function code에 따라 area와 응답 payload packing 규칙을 분기한다.

## UC-003

- Command: `ReceiveWriteRequest`, `DecodeWritePayload`, `ExecuteClientWrite`, `EncodeWriteAck`
- Event: `ClientWriteAccepted`, `WritableMemoryUpdated`, `WriteAckSent`
- Policy: 외부 client write는 `Coil`과 `Holding Register`에만 허용하고 read-only area는 exception으로 거절한다.

## UC-004

- Command: `MapModbusException`, `EncodeExceptionResponse`
- Event: `IllegalFunctionDetected`, `IllegalDataAddressDetected`, `IllegalDataValueDetected`, `ExceptionResponseSent`
- Policy: 오류 응답은 요청 function code의 상위 비트를 켠 값으로 반환한다.

## UC-005

- Command: `GetHostHealth`
- Event: `HealthSnapshotCreated`
- Policy: 세션 수는 connection open/close 시점마다 갱신한다.

## UC-006

- Command: `ProvideReplayCsvPath`, `LoadReplayCsv`, `ParseReplayHeader`, `BuildReplayScenario`
- Event: `ReplayCsvPathProvided`, `ReplayCsvLoaded`, `ReplayHeaderParsed`, `ReplayScenarioBuilt`, `ReplayScenarioBuildFailed`
- Policy: 첫 컬럼은 반드시 `time`이어야 하고 나머지 컬럼은 모두 Modbus 주소로 해석 가능해야 하며, 각 row는 헤더와 동일한 컬럼 수를 가져야 한다.

## UC-007

- Command: `StartReplay`, `ScheduleReplayStep`, `ApplyReplayStep`, `ExecuteScenarioWrite`
- Event: `ReplayStarted`, `ReplayStepScheduled`, `ReplayStepApplied`, `ScenarioMemoryUpdated`, `ReplayCompleted`
- Policy: 같은 시각의 row는 파일 순서대로 적용하고, replay 내부 쓰기는 read-only area도 허용한다.

## UC-008

- Command: `ValidateReplaySchema`, `ValidateReplayTimeline`, `ValidateReplayValue`
- Event: `ReplaySchemaInvalid`, `ReplayTimelineInvalid`, `ReplayValueInvalid`, `ReplayRejected`
- Policy: replay 시작 전 오류를 확정하고 메모리 변경은 하지 않으며, 빈 셀과 시간 역전은 즉시 실패한다.

## UC-009

- Command: `ReceiveReadRequest`, `ExecuteRead`
- Event: `ReplayVisibleInReadResponse`
- Policy: read 시점의 최신 메모리 값이 항상 반환되어야 한다.
