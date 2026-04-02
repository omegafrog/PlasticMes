# Properties

owner: Codex

status: verified

last_verified: 2026-04-02

title: Mitsubishi SLMP Simulator Timed CSV Register Replay 실행 계획

parent_docs:
- [index.md](../../index.md)

domain: PLC Simulation / Mitsubishi SLMP Simulator

# 프롬프트 원본

## Task Summary

- 목표는 기존 미츠비시 PLC 시뮬레이터가 CSV를 읽어 자신의 register 값을 시간 순서대로 바꾸도록 확장하는 것이다.
- 현재 구현의 핵심 경로는 `PlasticMes.MitsubishiSimulator/Adapters/Memory/InMemoryDeviceMemoryStore.cs`, `PlasticMes.MitsubishiSimulator/Hosting/MitsubishiSimulatorHost.cs`, `PlasticMesSolution/Program.cs`, `PlasticMesSolution/Form1.cs`, `PlasticMesTest/SlmpSimulatorTests.cs`다.
- 현재 응답은 계획 산출물이며, 코드 수정은 수행하지 않았다.

## Assumption

- 저장소에 `ARCHITECTURE.md`는 없다. 설계 기준은 기존 미츠비시 시뮬레이터 문서와 현재 코드 구조로 제한한다.
- `time` 외 컬럼 헤더는 register 주소 문자열로 해석한다. 예: `X0`, `Y10`, `M100`, `D200`.
- 각 데이터 row는 해당 시점에 적용할 register snapshot이다. 실제 적용 시각은 `row.time - firstDataRow.time` ms 로 계산한다.
- 첫 데이터 row의 `time` 값이 `0`이 아니어도 즉시 적용한다.
- 빈 셀의 의미는 명시되지 않았으므로 1차 구현은 빈 셀을 허용하지 않고 validation error로 처리한다.
- CSV 파일 전달 방식은 미정이다. 1차 구현은 composition root에서 경로를 주입하는 방식으로 제한하고, 별도 운영 UI는 범위 밖으로 둔다.

# Missing Inputs

- 현재 계획 산출물 기준으로 추가 누락 입력은 없다.
- 다만 `빈 셀 처리 방식`과 `CSV 경로 전달 방식`이 위 가정과 다르면 구현 agent로 넘기기 전에 두 항목만 먼저 확정해야 한다.

# 상세 계획

## 1. 도메인 경계 선정

- [domain-boundary.md](../../../../product-specs/PLC_Simulation/Mitsubishi_SLMP_Simulator/domain-boundary.md)

## 2. 유스케이스 정의

- [use-cases.md](../../../../product-specs/PLC_Simulation/Mitsubishi_SLMP_Simulator/use-cases.md)

## 3. 이벤트 스토밍

- [event-storming.md](../../../../design-docs/PLC_Simulation/Mitsubishi_SLMP_Simulator/event-storming.md)

## 4. 상세 설계

- [detailed-design.md](../../../../design-docs/PLC_Simulation/Mitsubishi_SLMP_Simulator/detailed-design.md)

## 5. 구현

- [ ] `PlasticMes.MitsubishiSimulator/Application`에 replay 로더/컨트롤러 포트를 추가한다.
- [ ] `PlasticMes.MitsubishiSimulator/Contracts`에 replay scenario, step, status DTO를 추가한다.
- [ ] `PlasticMes.MitsubishiSimulator/Adapters/Csv`에 CSV 파서 및 register address 변환기를 추가한다.
- [ ] `PlasticMes.MitsubishiSimulator/Hosting`에 상대 ms 기준 replay controller를 추가한다.
- [ ] `PlasticMesSolution/Program.cs`에서 shared memory store와 replay 조립 구성을 반영한다.
- [ ] `PlasticMesSolution/Form1.cs`에 replay 시작/중지 수명주기와 오류 노출을 연결한다.
- [ ] `PlasticMesTest/SlmpSimulatorTests.cs`를 확장하고 CSV/replay 전용 테스트 파일을 추가한다.
- [ ] 기존 `UC-001`부터 `UC-005`까지의 동작을 깨지 않는 회귀 테스트를 유지한다.

## 6. 검증

- 명령: `dotnet build PlasticMes.MitsubishiSimulator/PlasticMes.MitsubishiSimulator.csproj -v minimal`
- 기대 결과: replay 관련 신규 타입과 어댑터가 library 단위에서 컴파일된다.
- 실패 시 되돌아갈 설계 포인트: 포트 위치, DTO 참조 방향, `Hosting`과 `Application` 간 의존성 누수
- 명령: `dotnet build PlasticMesSolution/PlasticMesSolution.csproj -v minimal`
- 기대 결과: WinForms composition root가 replay controller를 포함해 빌드된다.
- 실패 시 되돌아갈 설계 포인트: `Program` 조립 방식, `Form1` lifecycle 연결, Windows 전용 코드와 library 경계
- 명령: `dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal`
- 기대 결과: 기존 SLMP 테스트와 신규 CSV replay 테스트가 모두 통과한다.
- 실패 시 되돌아갈 설계 포인트: `time` 정규화, row 적용 순서, cancellation, shared memory visibility
- 명령: `python3 .agents/skills/docs-verify/scripts/run.py`
- 기대 결과: 문서 링크와 필수 섹션 검증이 통과한다.
- 실패 시 되돌아갈 설계 포인트: 문서 경로, 인덱스 링크, 상태 메타데이터
- 검증 루프:
- 테스트가 flaky하면 실제 시간 대기 대신 제어 가능한 시간 추상화 사용 여부를 상세 설계로 되돌린다.
- CSV 파싱 오류가 반복되면 헤더 문법과 값 파싱 정책을 먼저 재정의한다.

## 7. 문서화

- 수정: [domain-boundary.md](../../../../product-specs/PLC_Simulation/Mitsubishi_SLMP_Simulator/domain-boundary.md)
- 수정: [use-cases.md](../../../../product-specs/PLC_Simulation/Mitsubishi_SLMP_Simulator/use-cases.md)
- 수정: [event-storming.md](../../../../design-docs/PLC_Simulation/Mitsubishi_SLMP_Simulator/event-storming.md)
- 수정: [detailed-design.md](../../../../design-docs/PLC_Simulation/Mitsubishi_SLMP_Simulator/detailed-design.md)
- 수정: [plan.md](plan.md)
- 후속 동기화 검토: [mitsubishi-slmp-simulator.md](../../../../references/mitsubishi-slmp-simulator.md)

# Out of Scope

- CSV 파일 선택용 고급 WinForms UI
- 빈 셀을 "변경 없음"으로 해석하는 sparse row semantics
- `int32`, `float32` 같은 multi-word 타입 조합
- replay loop, pause/resume, seek
- 여러 CSV를 동시에 합성 재생하는 기능

# Output Files

- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Application/IReplayScenarioLoader.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Application/IRegisterReplayController.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Contracts/RegisterReplayScenario.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Contracts/ReplayStep.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Contracts/ReplayStatusSnapshot.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Adapters/Csv/CsvReplayScenarioLoader.cs`
- 계획 대상 신규 코드 파일: `PlasticMes.MitsubishiSimulator/Hosting/RegisterReplayController.cs`
- 계획 대상 수정 코드 파일: `PlasticMesSolution/Program.cs`
- 계획 대상 수정 코드 파일: `PlasticMesSolution/Form1.cs`
- 계획 대상 수정 코드 파일: `PlasticMesTest/SlmpSimulatorTests.cs`
