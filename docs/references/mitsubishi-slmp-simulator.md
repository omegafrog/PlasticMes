# Mitsubishi SLMP Simulator 구현 스펙 및 사용법

## 1. 목적

이 문서는 저장소에 구현된 Mitsubishi SLMP simulator의 실제 동작 범위와 사용 방법을 정리한다.
벤더 원문 규격의 대체 문서가 아니라, 현재 코드 기준의 프로젝트용 운영/개발 참고서다.

관련 코드:
- `PlasticMes.MitsubishiSimulator/`
- `PlasticMesSolution/`
- `PlasticMesTest/`

함께 봐야 할 문서:
- [mitsubishi-slmp-mc.md](mitsubishi-slmp-mc.md)
- [상세 설계](../design-docs/PLC_Simulation/Mitsubishi_SLMP_Simulator/detailed-design.md)
- [실행 계획](../exec-plans/active/PLC_Simulation/Mitsubishi_SLMP_Simulator/plan.md)

## 2. 현재 구현 범위

현재 구현은 다음 범위만 지원한다.

- 전송: TCP
- 프레임: SLMP 3E Binary
- 디바이스: `X`, `Y`, `M`, `D`
- 읽기: Batch Read
- 쓰기: Batch Write
- 메모리 저장소: 인메모리
- 호스트: TCP listener + 세션 수 추적 + start/stop lifecycle

현재 구현하지 않은 범위:

- 4E Frame
- ASCII
- UDP
- Random Read / Random Write
- 초기값 파일 로드
- 운영용 UI
- MES 공통 southbound adapter 통합

## 3. 프로젝트 구조

### 3.1 라이브러리

`PlasticMes.MitsubishiSimulator/` 아래 구현된다.

- `Contracts`: DTO, enum, 상태 모델
- `Application`: 포트 인터페이스와 request handler
- `Adapters/Memory`: 인메모리 디바이스 저장소
- `Adapters/Tcp`: 3E codec, 세션 저장소
- `Hosting`: TCP host

핵심 타입:

- `SimulatorHostConfig`
- `DeviceAddress`
- `DeviceRange`
- `DeviceWrite`
- `SlmpRequest`
- `SlmpResponse`
- `ProtocolError`
- `SimulatorHealthSnapshot`
- `IMitsubishiSimulatorHost`
- `IDeviceMemoryStore`
- `ISlmpFrameCodec`

### 3.2 WinForms host

`PlasticMesSolution/`은 composition root 역할만 맡는다.

- `Program.cs`: simulator host 조립
- `Form1.cs`: form `Shown` 시점 start, `FormClosed` 시점 stop

기본 실행 설정:

- bind address: `127.0.0.1`
- port: `5000`
- network no: `0x00`
- station no: `0xFF`
- module I/O no: `0x03FF`
- multidrop no: `0x00`
- request timeout: `1s`

## 4. 프로토콜 동작 요약

### 4.1 지원 명령

- Batch Read: `0x0401`
- Batch Write: `0x1401`

### 4.2 지원 subcommand

- word unit: `0x0000`
- bit unit: `0x0001`

해석 규칙:

- `D`는 word access만 허용한다.
- `X`, `Y`, `M`은 bit access로 처리한다.
- bit read 응답은 요청 subcommand에 따라 packed nibble 또는 word bit payload로 직렬화된다.

### 4.3 오류 처리

주요 end code 매핑:

- `0x0000`: 정상
- `0xC059`: unsupported command
- `0xC051`: invalid address/range
- `0xC06F`: decode error
- `0xCF00`: connection error용 내부 분류 코드

## 5. 메모리 모델

기본 저장소는 `InMemoryDeviceMemoryStore`다.

- `X`: bit array
- `Y`: bit array
- `M`: bit array
- `D`: `ushort` array

기본 용량:

- bit area당 `4096`
- `D` word `4096`

검증 규칙:

- 길이 `0` 금지
- 음수 offset 금지
- 범위 초과 금지
- `D`에 대한 bit access 금지
- `X/Y/M`에 대한 word access 금지

## 6. 사용 방법

### 6.1 WinForms 앱으로 실행

1. `PlasticMesSolution` 프로젝트를 실행한다.
2. `Form1`이 표시되면 `Shown` 이벤트에서 simulator host가 자동으로 시작된다.
3. title bar에서 host 상태와 포트를 확인한다.
4. 창을 닫으면 host가 중지된다.

프로젝트 단위 빌드 명령:

```bash
dotnet build PlasticMesSolution/PlasticMesSolution.csproj -v minimal
```

### 6.2 코드에서 직접 host 사용

```csharp
var memoryStore = new InMemoryDeviceMemoryStore();
var codec = new Slmp3EFrameCodec();
var handler = new SlmpRequestHandler(memoryStore);
var sessions = new ConnectionSessionStore();
var host = new MitsubishiSimulatorHost(codec, handler, sessions);

await host.StartAsync(
    new SimulatorHostConfig(
        IPAddress.Loopback,
        5000,
        0x00,
        0xFF,
        0x03FF,
        0x00,
        TimeSpan.FromSeconds(1)),
    CancellationToken.None);
```

중지:

```csharp
await host.StopAsync(CancellationToken.None);
```

상태 확인:

```csharp
var health = host.GetHealth();
```

## 7. 테스트 클라이언트 기준 사용 예

테스트 예시는 [SlmpSimulatorTests.cs](../../PlasticMesTest/SlmpSimulatorTests.cs)에 있다.

현재 검증된 흐름:

- bit batch read frame decode
- bit batch read payload serialization
- out-of-range access rejection
- TCP write 후 read round-trip

테스트 실행:

```bash
dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal
```

## 8. 구현 확인 상태

이 환경에서 확인된 결과:

- `dotnet build PlasticMesSolution/PlasticMesSolution.csproj -v minimal`: 성공
- `dotnet test PlasticMesTest/PlasticMesTest.csproj -v minimal`: 성공, 4 tests passed

제한 사항:

- `dotnet build PlasticMesSolution/PlasticMesSolution.slnx`는 이 환경에서 solution-level SDK/workload resolver 문제로 실패했다.
- `api.nuget.org` 접근 제한 때문에 restore 시 `NU1900` warning이 출력될 수 있다.

## 9. 다음 확장 후보

- 4E Frame
- ASCII codec
- Random Read / Random Write
- 초기값 파일 로더
- 태그 패턴 생성기
- host configuration 외부화
- 운영 UI
