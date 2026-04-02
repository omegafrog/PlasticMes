# Properties

status: verified

last_verified: 2026-04-02

# 상세 설계

## 디렉토리 구조

- 구현된 라이브러리는 `PlasticMes.ModbusSimulator/`이고, `Application`, `Contracts`, `Adapters`, `Hosting`으로 분리되어 있다.
- 실행 진입점은 `PlasticMes.ModbusSimulatorHost/Program.cs`다.
- `PlasticMesSolution/PlasticMesSolution.slnx`는 Mitsubishi simulator, Modbus simulator, Modbus host, 테스트 프로젝트를 함께 로드한다.
- `PlasticMesTest/PlasticMesTest.csproj`는 Mitsubishi와 Modbus simulator를 동시에 참조한다.
- 기존 `PlasticMes.MitsubishiSimulator`를 공용화하는 리팩터링은 이번 범위에 포함하지 않는다.

## 실제 파일 매핑

- `PlasticMes.ModbusSimulator/Application/ModbusRequestHandler.cs`: Modbus function code 분기와 response payload 인코딩
- `PlasticMes.ModbusSimulator/Application/IModbusFrameCodec.cs`: MBAP/PDU codec 포트
- `PlasticMes.ModbusSimulator/Application/IModbusMemoryStore.cs`: read, client write, scenario write 포트
- `PlasticMes.ModbusSimulator/Application/IModbusSimulatorHost.cs`: host start/stop/health 포트
- `PlasticMes.ModbusSimulator/Application/IReplayScenarioLoader.cs`: CSV 시나리오 로드 포트
- `PlasticMes.ModbusSimulator/Application/IRegisterReplayController.cs`: replay start/stop/status 포트
- `PlasticMes.ModbusSimulator/Adapters/Memory/InMemoryModbusMemoryStore.cs`: 4개 메모리 영역과 write 정책 구현
- `PlasticMes.ModbusSimulator/Adapters/Tcp/ModbusTcpFrameCodec.cs`: MBAP 검증, read/write request 파싱, exception frame 인코딩
- `PlasticMes.ModbusSimulator/Adapters/Tcp/ConnectionSessionStore.cs`: 연결 세션 개수 추적
- `PlasticMes.ModbusSimulator/Adapters/Csv/CsvReplayScenarioLoader.cs`: CSV 헤더/값 validation과 replay step 생성
- `PlasticMes.ModbusSimulator/Hosting/ModbusSimulatorHost.cs`: `TcpListener` 기반 accept loop와 request 처리
- `PlasticMes.ModbusSimulator/Hosting/RegisterReplayController.cs`: 상대 시간 replay 실행
- `PlasticMes.ModbusSimulatorHost/Program.cs`: CLI 파싱과 host/replay composition root

## 포트

- `IModbusFrameCodec`
- 시그니처: `ParseResult<ModbusRequest> Decode(ReadOnlyMemory<byte> payload)`
- 시그니처: `ReadOnlyMemory<byte> Encode(ModbusResponse response)`
- `IModbusRequestHandler`
- 시그니처: `Task<ModbusResponse> HandleAsync(ModbusRequest request, CancellationToken ct)`
- `IModbusMemoryStore`
- 시그니처: `ModbusReadResult Read(ModbusRange range)`
- 시그니처: `ModbusWriteResult WriteClient(ModbusWrite write)`
- 시그니처: `ModbusWriteResult WriteScenario(ModbusWrite write)`
- `IModbusSimulatorHost`
- 시그니처: `Task StartAsync(ModbusSimulatorHostConfig config, CancellationToken ct)`
- 시그니처: `Task StopAsync(CancellationToken ct)`
- 시그니처: `SimulatorHealthSnapshot GetHealth()`
- `IReplayScenarioLoader`
- 시그니처: `Task<ReplayScenarioLoadResult> LoadAsync(string csvPath, CancellationToken ct)`
- `IRegisterReplayController`
- 시그니처: `Task StartAsync(RegisterReplayScenario scenario, CancellationToken ct)`
- 시그니처: `Task StopAsync(CancellationToken ct)`
- 시그니처: `ReplayStatusSnapshot GetStatus()`

## 주요 DTO

- `ModbusArea { Coil, DiscreteInput, InputRegister, HoldingRegister }`
- `ModbusAddress(ModbusArea Area, int Offset)`
- `ModbusRange(ModbusAddress Start, ushort Length)`
- `ModbusWrite(ModbusRange Range, IReadOnlyList<ushort>? RegisterValues = null, IReadOnlyList<bool>? BitValues = null)`
- `ModbusRequest(ushort TransactionId, byte UnitId, byte FunctionCode, ModbusRange Range, ModbusWrite? Write = null)`
- `ModbusResponse(ushort TransactionId, byte UnitId, byte FunctionCode, ReadOnlyMemory<byte> Data, byte? ExceptionCode = null, ModbusProtocolError? Error = null)`
- `ReplayColumn(string HeaderName, ModbusAddress Address)`
- `ReplayStep(TimeSpan Offset, IReadOnlyList<ModbusWrite> Writes, int RowNumber)`
- `RegisterReplayScenario(...)`
- `ReplayScenarioLoadResult(bool IsSuccess, RegisterReplayScenario? Scenario, string? ErrorMessage)`
- `ReplayStatusSnapshot(bool IsRunning, int AppliedStepCount, int TotalStepCount, int? CurrentRowNumber, string? LastError)`
- `SimulatorHealthSnapshot(SimulatorHostState State, int Port, int SessionCount, DateTimeOffset? StartedAt, string? LastError = null)`

## 어댑터

- `Adapters/Tcp/ModbusTcpFrameCodec`: MBAP `TransactionId`, `ProtocolId=0`, `Length`, `UnitId` 검증 및 PDU 인코딩/디코딩 담당
- `Adapters/Memory/InMemoryModbusMemoryStore`: `bool[]` for coil/discrete, `ushort[]` for input/holding register, lock 기반 동시성 유지
- `Adapters/Csv/CsvReplayScenarioLoader`: 헤더를 Modbus 주소로 변환하고 `time` 정규화 후 `ModbusWrite` 목록 생성
- `Adapters/Tcp/ConnectionSessionStore`: connection open/close 기준으로 활성 세션 수를 추적
- `Hosting/ModbusSimulatorHost`: `TcpListener` accept loop, 세션 추적, frame decode, handle, encode, network write
- `Hosting/RegisterReplayController`: 상대 시간 delay 후 `WriteScenario` 호출

## CSV 규칙

- 첫 컬럼은 `time`
- 헤더는 `C00001`, `DI10001`, `IR30001`, `HR40001`
- 비트 영역 값은 `0/1/true/false`, 레지스터 값은 `ushort`
- 빈 셀, 중복 헤더, 음수 시간, 시간 역전은 즉시 실패
- 데이터 row는 헤더와 동일한 컬럼 수를 가져야 한다.
- CSV 파일은 헤더 row와 최소 1개의 data row를 포함해야 한다.

## 주요 설계 결정

- `time`은 정수 밀리초로 해석한다.
- 첫 데이터 row의 `time`을 기준 `0ms`로 정규화한다.
- CSV 헤더는 Modbus 참조 표기 기반의 명시적 형식을 사용하고 내부에서는 모두 `0-based offset`으로 정규화한다.
- replay 내부 쓰기는 read-only area도 허용하지만 외부 client write는 `Coil`, `HoldingRegister`만 허용한다.
- CSV replay와 Modbus read/write는 동일 메모리 저장소를 공유한다.
- `Program.cs`가 `Console.CancelKeyPress`를 받아 replay stop과 host stop을 함께 호출하는 종료 수명주기 조립 책임을 가진다.

## 실행 방법

- 기본 실행: `dotnet run --project PlasticMes.ModbusSimulatorHost --`
- 기본값: `--bind 127.0.0.1`, `--port 1502`, `--unit-id 1`
- CSV replay 포함 실행: `dotnet run --project PlasticMes.ModbusSimulatorHost -- --csv path/to/replay.csv`
- 전체 인자 예시: `dotnet run --project PlasticMes.ModbusSimulatorHost -- --bind 127.0.0.1 --port 1502 --unit-id 1 --csv path/to/replay.csv`

## 검증 결과

- 통과: `dotnet build PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulator.csproj -v minimal`
- 통과: `dotnet build PlasticMes.ModbusSimulatorHost/PlasticMes.ModbusSimulatorHost.csproj -v minimal`
- 통과: `dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal`

## 테스트 범위

- `PlasticMesTest/ModbusSimulatorTests.cs`: codec decode, exception response high bit, read-only area write 거절, register read payload, TCP write-then-read 및 session count 갱신
- `PlasticMesTest/ModbusRegisterReplayTests.cs`: CSV 헤더 파싱과 offset 정규화, 시간 역전 거절, replay의 read-only area 갱신, replay cancellation, replay 중 TCP read 가시성

## 구현-설계 차이

- CLI는 `--unit-id`를 받지만, 현재 `ModbusSimulatorHostConfig.UnitId`는 host 내부에서 요청 필터링 용도로 사용되지 않고 시작 로그와 구성 전달 수준에 머문다.

## 테스트 포인트

- MBAP decode/encode와 예외 응답 function code 비트 처리
- read-only / writable area 정책 분리
- CSV `time` 정규화와 동일 timestamp 순서 보장
- replay가 `Input Register`, `Discrete Input` 같은 read-only area도 내부적으로 갱신 가능한지 검증
- replay 중 read 요청이 최신 값을 반영하는지 검증
- host stop 시 replay 취소와 connection 정리가 되는지 검증
