# Mitsubishi MC Protocol / SLMP 프로젝트용 요약 스펙

## 1. 목적

이 문서는 MES가 Mitsubishi PLC 또는 Mitsubishi 호환 시뮬레이터와 통신할 때 필요한 최소 스펙을 정리한다.
이 프로젝트에서는 **이더넷 기반 통신**을 전제로 하며, 우선 지원 범위는 다음과 같다.

- 우선 지원: **SLMP 3E Frame / TCP / Binary**
- 호환 고려: MC Protocol 3E Frame
- 추후 검토: 4E Frame, ASCII, UDP, 확장 명령

실무적으로는 Mitsubishi 문서에서 **SLMP 3E/4E frame의 메시지 형식이 MC protocol의 QnA-compatible 3E/4E frame과 동일**하다고 설명하므로, 프로젝트는 둘을 분리 구현하기보다 **공통 프레이밍 + 명령 집합**으로 다루는 것이 유리하다.

## 2. 이 프로토콜이 필요한 이유

국내 현장에서는 Mitsubishi PLC가 널리 쓰이며, MES/수집기/HMI가 PLC의 디바이스 메모리(X, Y, M, D 등)를 직접 읽고 쓰는 경우가 많다.
OPC UA가 없는 장비, 또는 OPC UA 모듈이 없는 구성에서도 가장 현실적으로 붙을 수 있는 인터페이스다.

## 3. 통신 모델

- 역할
  - MES 어댑터: Client
  - PLC 또는 시뮬레이터: Server 역할의 응답 대상
- 기본 흐름
  1. MES가 요청 메시지를 보낸다.
  2. PLC가 요청을 처리한다.
  3. PLC가 응답 메시지를 반환한다.
- 성격
  - 요청/응답 기반
  - 값 변경 푸시 모델이 아니라, **MES가 polling** 하거나 상위 스케줄러가 주기적으로 읽는다.

## 4. 프로젝트 구현 범위

### 4.1 1차 범위

- 전송 방식: TCP
- 코드 방식: Binary
- 프레임: 3E Frame
- 대상 디바이스
  - Bit: X, Y, M
  - Word: D
- 지원 명령
  - Batch Read (bit/word)
  - Batch Write (bit/word)
  - Random Read (선택)
  - Random Write (선택)
- 예외 처리
  - End code 해석
  - 타임아웃
  - 재시도

### 4.2 2차 범위

- L, R, W, ZR 등 추가 디바이스
- 4E Frame
- ASCII Code
- 모듈/버퍼 메모리 접근
- 멀티 드롭/다른 네트워크 경유 접근

## 5. 프레임 구조 개요

### 5.1 3E Frame 개념

3E Frame 요청 메시지는 대략 다음 의미의 필드로 구성된다.

- Subheader
- Request destination network No.
- Request destination station No.
- Request destination module I/O No.
- Request destination multidrop station No.
- Request data length
- Monitoring timer
- Command
- Subcommand
- Request data

응답 메시지는 다음 의미의 필드로 구성된다.

- Subheader
- Request destination network No.
- Request destination station No.
- Request destination module I/O No.
- Request destination multidrop station No.
- Response data length
- End code
- Response data

즉, 이 프로토콜은 단순히 주소와 값만 보내는 것이 아니라 **대상 네트워크/스테이션/모듈**, **명령 종류**, **감시 타이머**, **응답 종료 코드**를 명시하는 구조다.

### 5.2 구현상 핵심 해석

프로젝트 관점에서 중요하게 봐야 할 것은 다음 네 가지다.

1. **어느 PLC/모듈을 대상으로 하는가**  
   네트워크 번호, 스테이션 번호, 모듈 I/O 번호, 멀티드롭 번호로 식별한다.

2. **무슨 동작을 하는가**  
   Command / Subcommand가 읽기인지 쓰기인지, bit 단위인지 word 단위인지 결정한다.

3. **얼마나 오래 기다릴 것인가**  
   Monitoring timer로 PLC 처리 제한 시간을 표현한다.

4. **성공했는가**  
   End code가 정상/오류를 결정한다.

## 6. 주소 체계

Mitsubishi는 디바이스 메모리를 논리 주소로 노출한다. 프로젝트 1차 범위에서는 다음만 다룬다.

- `X`: 입력(bit)
- `Y`: 출력(bit)
- `M`: 내부 릴레이(bit)
- `D`: 데이터 레지스터(word)

예시:
- `X0`
- `Y10`
- `M100`
- `D200`

프로젝트 내부에서는 다음과 같이 정규화한다.

```text
ProtocolAddress
- protocol: MITSUBISHI_SLMP
- area: X | Y | M | D
- offset: 정수
- unit: BIT | WORD
```

예:
- `M100` -> `{ protocol=MITSUBISHI_SLMP, area=M, offset=100, unit=BIT }`
- `D200` -> `{ protocol=MITSUBISHI_SLMP, area=D, offset=200, unit=WORD }`

## 7. 데이터 타입 매핑

프로토콜 레벨은 주로 bit와 16-bit word 중심이다. 실제 애플리케이션 값은 여러 word 조합으로 해석한다.

- Bit 디바이스
  - `bool`
- Word 디바이스
  - `int16`, `uint16`
- 2 word 조합
  - `int32`, `uint32`, `float32`
- 4 word 조합
  - `int64`, `uint64`, `double`

주의:
- 상위 타입(float, double, 32/64비트 정수)은 **프로토콜이 직접 의미를 주는 것이 아니라**, MES 측 디코더가 word 조합 방식으로 해석한다.
- word 순서와 endian 해석은 장비/프로젝트 정책으로 고정해야 한다.

권장 정책:
- 1차 구현에서는 `bool`, `int16`, `uint16`, `int32`, `uint32`, `float32`만 공식 지원한다.
- `TagDefinition`에 `wordOrder`와 `signed` 여부를 포함한다.

## 8. 읽기/쓰기 모델

### 8.1 Batch Read

연속된 주소 범위를 읽는다.

적합한 예:
- `M100 ~ M131`
- `D200 ~ D215`

장점:
- 요청 수를 줄여 성능이 좋다.
- polling 모델에 적합하다.

### 8.2 Random Read

띄엄띄엄 떨어진 주소를 한 번에 읽는다.

적합한 예:
- `M100`, `M140`, `D200`, `D500`

장점:
- 주소가 흩어진 태그를 한 번에 읽을 수 있다.

단점:
- 구현과 디코딩이 조금 더 복잡하다.

### 8.3 Write

MES에서 쓰기는 다음처럼 제한한다.

- 허용 대상
  - 시뮬레이터 메모리
  - 테스트용 내부 릴레이(M)
  - 승인된 출력(Y) 또는 데이터 레지스터(D)
- 금지 대상
  - 실제 설비 제어에 직접 연결된 민감 주소
  - 운영 모드 변경, 리셋, 정지에 해당하는 주소

## 9. 오류 처리

응답의 **End code**가 핵심이다.

- `0x0000`: 정상 처리
- 그 외: 오류

프로젝트 정책:
- End code가 0이 아니면 `ProtocolError`로 변환한다.
- 오류는 다음 범주로 정규화한다.
  - `TIMEOUT`
  - `CONNECTION_ERROR`
  - `PLC_REJECTED`
  - `INVALID_ADDRESS`
  - `INVALID_COMMAND`
  - `DECODE_ERROR`

예시 모델:

```text
ProtocolResult<T>
- success: boolean
- value: T?
- errorCode: string?
- vendorCode: string?
- message: string?
- timestamp: Instant
```

## 10. 세션/연결 정책

- TCP 연결은 태그별로 열지 않는다.
- PLC endpoint 별 connection pool 또는 long-lived connection을 유지한다.
- 한 connection에서 요청 직렬화를 기본으로 한다.
- 고성능이 필요하면 endpoint 당 worker를 두고 request queue를 운영한다.

권장값:
- connect timeout: 3s
- request timeout: 1s ~ 2s
- retry: 1~2회
- health check: 5s ~ 10s 간격

## 11. 보안 주의사항

MC/SLMP 자체는 현대적 보안 기능을 강하게 제공하는 표준 프로토콜이 아니다.
따라서 다음을 기본 원칙으로 한다.

- 인터넷 직접 노출 금지
- 생산망/사무망 분리
- VPN 또는 점프호스트 뒤에서만 접근
- 쓰기 기능은 기본 비활성화
- 허용 주소 화이트리스트 적용
- 감사 로그 남기기

## 12. 시뮬레이터 구현 가이드

이 프로젝트의 PLC 시뮬레이터는 다음 수준만 충족하면 충분하다.

### 필수
- TCP listener
- 3E Binary frame 수신
- Batch Read / Batch Write 처리
- X/Y/M/D 메모리 맵 유지
- End code 반환

### 선택
- Random Read / Random Write
- 태그 변화 스크립트
- 시계열 패턴 생성
- 디바이스 초기값 로드

### 내부 메모리 모델 예시

```text
MitsubishiMemory
- bitAreas
  - X: BitSet
  - Y: BitSet
  - M: BitSet
- wordAreas
  - D: short[]
```

## 13. MES 내부 표준 인터페이스 매핑

```text
interface PlcAdapter {
  read(tags): TagValueBatch
  write(commands): WriteResultBatch
  health(): AdapterHealth
}
```

Mitsubishi 어댑터는 내부적으로 다음을 수행한다.

1. 태그를 area/offset으로 그룹핑
2. 연속 주소는 batch read/write로 묶기
3. 응답을 공통 `TagValue`로 변환
4. `quality`, `timestamp`, `vendorCode` 부여

## 14. 구현 우선순위 결론

이 프로젝트에서는 **Mitsubishi SLMP/MC를 가장 먼저 구현**한다.
이유는 다음과 같다.

- 국내 PLC 연동 현실성과 직접성이 높다.
- OPC UA가 없는 장비에도 붙을 수 있다.
- 시뮬레이터 구현 범위를 작게 시작할 수 있다.
- MES southbound adapter 구조를 검증하기 좋다.

## 15. 참고 원문

- Mitsubishi Electric, **SLMP Reference Manual**
- Mitsubishi Electric, **MELSEC iQ-F FX5 User's Manual (SLMP)**
- Mitsubishi Electric, **MELSEC Communication Protocol Reference Manual**
- Mitsubishi Electric, **MELSEC iQ-F FX5 User's Manual (MELSEC Communication Protocol)**
