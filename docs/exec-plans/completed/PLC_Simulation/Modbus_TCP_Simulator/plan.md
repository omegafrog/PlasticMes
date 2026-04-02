# Properties

owner: Codex

status: completed

last_verified: 2026-04-02

completed_at: 2026-04-02

title: Modbus TCP Simulator Timed CSV Register Replay 실행 계획

parent_docs:
- [index.md](../../index.md)

domain: PLC Simulation / Modbus TCP Simulator

# 프롬프트 원본

## Task Summary

- 목표: `Modbus TCP`를 따르는 PLC 시뮬레이터를 추가하고, CSV의 `time` 컬럼 기준으로 시작 시점부터 경과한 시간에 맞춰 메모리 값을 row 값으로 반영하는 기능까지 설계한다.
- 현재 기준 아키텍처: 저장소에 `ARCHITECTURE.md`는 없으므로, 기존 `PlasticMes.MitsubishiSimulator`의 프로젝트 분리 방식과 `docs/` 문서를 설계 기준으로 사용한다.
- 전달 형태: 이번 응답은 실행 agent에 넘길 수 있는 계획만 포함하며, 코드 수정은 하지 않았다.

## Assumption

- `time`은 정수 밀리초다. 첫 데이터 row의 `time`을 기준 `0ms`로 정규화한다.
- CSV 헤더는 Modbus 참조 표기 기반의 명시적 형식 `C00001`, `DI10001`, `IR30001`, `HR40001`을 사용하고, 내부에서는 모두 `0-based offset`으로 정규화한다.
- 기존 `PlasticMesSolution`은 Mitsubishi 전용 조립 코드에 가깝기 때문에, 1차 구현은 별도 `Modbus` 전용 라이브러리와 실행 호스트를 추가하는 방향으로 잡는다.

# Missing Inputs

- 현재 계획 산출물 기준으로 추가 누락 입력은 없다.

# 상세 계획

## 1. 도메인 경계 선정

- [domain-boundary.md](../../../../product-specs/PLC_Simulation/Modbus_TCP_Simulator/domain-boundary.md)

## 2. 유스케이스 정의

- [use-cases.md](../../../../product-specs/PLC_Simulation/Modbus_TCP_Simulator/use-cases.md)

## 3. 이벤트 스토밍

- [event-storming.md](../../../../design-docs/PLC_Simulation/Modbus_TCP_Simulator/event-storming.md)

## 4. 상세 설계

- [detailed-design.md](../../../../design-docs/PLC_Simulation/Modbus_TCP_Simulator/detailed-design.md)

## 5. 구현

- [x] `docs/` 아래에 `Modbus TCP Simulator`용 도메인 경계, 유스케이스, 이벤트 스토밍, 상세 설계, 실행 계획 문서를 먼저 만든다.
- [x] 새 라이브러리 `PlasticMes.ModbusSimulator`와 실행 호스트 `PlasticMes.ModbusSimulatorHost` 프로젝트를 추가하고 솔루션 참조를 연결한다.
- [x] `Contracts`에 Modbus area, address, range, write, request, response, error, health, replay DTO를 정의한다.
- [x] `Application`에 frame codec, request handler, memory store, host, replay loader/controller 포트를 정의한다.
- [x] `Adapters/Memory`에 4개 Modbus 메모리 영역을 유지하는 `InMemoryModbusMemoryStore`를 구현한다.
- [x] `Adapters/Tcp`에 `ModbusTcpFrameCodec`과 connection session store를 구현한다.
- [x] `Application` 또는 `Hosting`에 8개 기본 function code를 처리하는 request handler를 구현한다.
- [x] `Adapters/Csv`에 CSV 헤더 파서와 value parser를 구현하고, replay scenario 생성 시점에 전체 validation을 끝낸다.
- [x] `Hosting`에 `RegisterReplayController`를 구현하고 host 종료 시 replay도 함께 중단되도록 연결한다.
- [x] `PlasticMes.ModbusSimulatorHost`에 CLI 인자 `--bind`, `--port`, `--unit-id`, `--csv`를 해석하는 composition root를 만든다.
- [x] `PlasticMesTest`에 codec, handler, store, replay, host integration 테스트를 추가한다.
- [x] 문서 인덱스와 실행 계획 상태를 실제 코드 변경 내용과 동기화한다.

## 6. 검증

- 명령: `dotnet build PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulator.csproj -v minimal`
- 기대 결과: Modbus library가 경고와 오류 없이 컴파일된다.
- 실패 시 되돌아갈 설계 포인트: 계약 타입 경계, request/response DTO 방향, 메모리 저장소 인터페이스 분리 방식
- 명령: `dotnet build PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/PlasticMes.ModbusSimulatorHost.csproj -v minimal`
- 기대 결과: 실행 호스트가 Modbus library를 조립하고 기본 포트와 CSV 인자를 해석할 수 있다.
- 실패 시 되돌아갈 설계 포인트: 별도 host 프로젝트 유지 여부, 설정 객체 책임, CLI 파싱 범위
- 명령: `dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal`
- 기대 결과: codec, exception response, read/write, replay, TCP integration 테스트가 모두 통과한다.
- 실패 시 되돌아갈 설계 포인트: MBAP length 계산, coil bit packing 규칙, read-only area write 정책, replay 취소 및 시간 제어
- 명령: `python scripts/validate_docs.py`
- 기대 결과: 새 문서와 인덱스 링크가 모두 유효하다.
- 실패 시 되돌아갈 설계 포인트: 문서 경로, 필수 헤더, 상태 메타데이터, 인덱스 연결
- 린트 가정: 저장소에 별도 lint 명령이 문서화되어 있지 않으므로 `dotnet build`의 analyzers와 컴파일 경고를 1차 lint gate로 사용한다.
- 기대 결과: 분석기 경고가 남지 않거나, 남는 경우 설계상 의도와 일치해야 한다.
- 실패 시 되돌아갈 설계 포인트: nullable 처리, API surface, 프로젝트 target/framework 선택

## 7. 문서화

- 생성: [domain-boundary.md](../../../../product-specs/PLC_Simulation/Modbus_TCP_Simulator/domain-boundary.md)
- 생성: [use-cases.md](../../../../product-specs/PLC_Simulation/Modbus_TCP_Simulator/use-cases.md)
- 생성: [event-storming.md](../../../../design-docs/PLC_Simulation/Modbus_TCP_Simulator/event-storming.md)
- 생성: [detailed-design.md](../../../../design-docs/PLC_Simulation/Modbus_TCP_Simulator/detailed-design.md)
- 생성: [plan.md](plan.md)
- 수정: [docs/product-specs/index.md](../../../../product-specs/index.md)
- 수정: [docs/design-docs/index.md](../../../../design-docs/index.md)
- 수정: [docs/exec-plans/active/index.md](../../index.md)

# Out of Scope

- `Modbus RTU/ASCII`
- TLS
- 브리지 뒤 다중 Unit 라우팅
- CSV live reload
- loop, pause, seek
- 빈 셀을 "변경 없음"으로 해석하는 sparse semantics
- `int32/float` 조합 타입

# 구현 반영 결과

## 반영된 프로젝트와 솔루션 연결

- 신규 라이브러리: `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulator.csproj`
- 신규 실행 진입점: `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/PlasticMes.ModbusSimulatorHost.csproj`
- 솔루션 연결: `PlasticMesSolution/PlasticMesSolution.slnx`가 Mitsubishi simulator, Modbus simulator, Modbus host, 테스트 프로젝트를 함께 포함한다.
- 테스트 참조 확장: `PlasticMesTest/PlasticMesTest.csproj`가 Mitsubishi와 Modbus simulator를 동시에 참조한다.

## 실제 구현 파일 기준 책임

- `PlasticMes.ModbusSimulator/Application/ModbusRequestHandler.cs`: `0x01`, `0x02`, `0x03`, `0x04`, `0x05`, `0x06`, `0x0F`, `0x10`을 처리하고 오류를 Modbus exception response로 변환한다.
- `PlasticMes.ModbusSimulator/Adapters/Memory/InMemoryModbusMemoryStore.cs`: `Coil`, `DiscreteInput`, `InputRegister`, `HoldingRegister` 메모리와 client/scenario write 정책을 분리한다.
- `PlasticMes.ModbusSimulator/Adapters/Tcp/ModbusTcpFrameCodec.cs`: MBAP 검증, read/write payload decode, exception response 상위 비트 인코딩을 담당한다.
- `PlasticMes.ModbusSimulator/Adapters/Csv/CsvReplayScenarioLoader.cs`: `time` 첫 컬럼 강제, 헤더 주소 파싱, `0-based offset` 정규화, 시간 역전 및 빈 셀 validation을 담당한다.
- `PlasticMes.ModbusSimulator/Hosting/RegisterReplayController.cs`: 상대 시간 delay 후 scenario write를 적용하고 replay status를 유지한다.
- `PlasticMes.ModbusSimulator/Hosting/ModbusSimulatorHost.cs`: `TcpListener` accept loop, connection session count, frame read/decode/handle/encode/write를 담당한다.
- `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/Program.cs`: `--bind`, `--port`, `--unit-id`, `--csv`를 파싱하고 host 종료 시 replay도 함께 정리하는 composition root다.

## 검증 결과

- 통과: `dotnet build PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulator.csproj -v minimal`
- 통과: `dotnet build PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/PlasticMes.ModbusSimulatorHost.csproj -v minimal`
- 통과: `dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal`

## 남아 있는 확인 사항

- 현재 작업 디렉터리 `/mnt/e/workspace/PlasticMes`는 Git 루트가 아니며 실제 `.git` 디렉터리는 `PlasticMesSolution/` 아래에 있다.
- `--unit-id`는 CLI 입력과 시작 로그에는 반영되지만, 현재 host 구성값으로 들어온 `UnitId`를 기준으로 요청을 필터링하지는 않는다.

# Output Files

- 계획 대상 신규 프로젝트: `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulator.csproj`
- 계획 대상 신규 프로젝트: `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/PlasticMes.ModbusSimulatorHost.csproj`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Application/*`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Contracts/*`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Adapters/Tcp/ModbusTcpFrameCodec.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Adapters/Tcp/ConnectionSessionStore.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Adapters/Memory/InMemoryModbusMemoryStore.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Adapters/Csv/CsvReplayScenarioLoader.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Hosting/ModbusSimulatorHost.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/Hosting/RegisterReplayController.cs`
- 계획 대상 신규 코드: `PlasticMes.ModbusSimulator/PlasticMes.ModbusSimulatorHost/Program.cs`
- 계획 대상 신규 테스트: `PlasticMesTest/ModbusSimulatorTests.cs`
- 계획 대상 신규 테스트: `PlasticMesTest/ModbusRegisterReplayTests.cs`
- 계획 대상 신규 문서: `docs/product-specs/PLC_Simulation/Modbus_TCP_Simulator/*`
- 계획 대상 신규 문서: `docs/design-docs/PLC_Simulation/Modbus_TCP_Simulator/*`
- 계획 대상 신규 문서: `docs/exec-plans/active/PLC_Simulation/Modbus_TCP_Simulator/plan.md`
