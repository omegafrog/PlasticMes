# Modbus TCP 프로젝트용 요약 스펙

## 1. 목적

이 문서는 MES가 PLC, 게이트웨이, I/O 디바이스, 또는 Modbus TCP 시뮬레이터와 통신할 때 필요한 최소 스펙을 정리한다.
이 프로젝트에서는 **Modbus TCP**만 다루며, RTU/ASCII 시리얼은 1차 범위에서 제외한다.

## 2. 왜 필요한가

Modbus는 매우 오래된 산업용 통신 프로토콜이지만, 지금도 PLC, 인버터, 전력계측기, 원격 I/O, 센서 게이트웨이 등에서 광범위하게 사용된다.
국내 MES에서도 장비 직접 연동보다 **게이트웨이/서드파티 장치**를 통해 Modbus TCP로 들어오는 경우가 많다.

## 3. 통신 모델

- 역할
  - MES 어댑터: Modbus Client
  - PLC/장치/시뮬레이터: Modbus Server
- 흐름
  1. Client가 Request PDU를 전송한다.
  2. Server가 요청을 처리한다.
  3. Server가 Response PDU를 반환한다.
- 특징
  - 요청/응답 기반
  - 상태 변경 푸시가 없다.
  - 대부분 polling 기반으로 운영한다.

## 4. 포장 구조: ADU / PDU

Modbus는 **PDU(Protocol Data Unit)** 와 **ADU(Application Data Unit)** 로 생각하면 이해가 쉽다.

- PDU
  - Function Code
  - Data
- TCP에서의 ADU
  - MBAP Header (7 bytes)
  - PDU

즉 Modbus TCP는 **MBAP Header + Function Code + Data** 구조다.

## 5. MBAP Header

MBAP Header는 7 bytes이며 다음 필드로 구성된다.

- Transaction Identifier (2 bytes)
- Protocol Identifier (2 bytes, Modbus는 0)
- Length (2 bytes)
- Unit Identifier (1 byte)

### 필드 의미

#### Transaction Identifier

요청과 응답을 짝지을 때 사용한다.
서버는 보통 요청의 값을 그대로 응답에 복사한다.

#### Protocol Identifier

Modbus protocol이면 `0x0000` 이다.

#### Length

뒤따르는 바이트 수를 나타낸다.
`Unit Identifier + PDU` 길이를 포함한다.

#### Unit Identifier

직접 TCP 장치에 붙을 때는 의미가 약하다.
하지만 브리지/게이트웨이 뒤의 서브 장치를 식별할 때 중요하다.
직접 연결 장치에서는 보통 `0xFF` 또는 `0x00`을 사용한다.

## 6. 데이터 모델

Modbus는 크게 네 가지 논리 영역을 다룬다.

- Coil (읽기/쓰기 bit)
- Discrete Input (읽기 전용 bit)
- Input Register (읽기 전용 16-bit)
- Holding Register (읽기/쓰기 16-bit)

프로젝트 내부 표준 모델:

```text
ProtocolAddress
- protocol: MODBUS_TCP
- area: COIL | DISCRETE_INPUT | INPUT_REGISTER | HOLDING_REGISTER
- offset: 정수(0-based 내부 정규화)
- unit: BIT | WORD
```

중요:
- 현장 문서에는 `00001`, `10001`, `30001`, `40001` 같은 표기를 자주 쓴다.
- 하지만 프로토콜 메시지 안에서는 시작 주소가 **0-based offset**으로 들어간다.
- 따라서 프로젝트는 내부적으로 무조건 `0-based`로 정규화한다.

예:
- 문서상 `40001` -> 내부 offset `0`
- 문서상 `40010` -> 내부 offset `9`

## 7. 1차 지원 Function Code

이 프로젝트는 우선 아래만 지원한다.

- `0x01` Read Coils
- `0x02` Read Discrete Inputs
- `0x03` Read Holding Registers
- `0x04` Read Input Registers
- `0x05` Write Single Coil
- `0x06` Write Single Register
- `0x0F` Write Multiple Coils
- `0x10` Write Multiple Registers

이 범위면 대부분의 MES 수집/시험 시나리오를 커버할 수 있다.

## 8. 읽기/쓰기 예시 개념

### 8.1 Holding Register 읽기

가장 흔한 패턴이다.

예:
- 설비 상태 코드
- 생산 수량
- 온도/전류의 원시 값
- 알람 코드

### 8.2 Coil 쓰기

일반적으로 강한 제어 의미를 가질 수 있으므로 주의가 필요하다.

예:
- 시뮬레이터의 Start/Stop bit
- Ack bit
- Test mode bit

운영 기본 정책:
- 실장비 coil write는 기본 금지
- 시뮬레이터 또는 승인 주소만 허용

## 9. 데이터 타입 해석

Modbus register는 기본적으로 16-bit word다.
프로토콜 자체가 `float`, `int32`, `double`을 표준 내장 타입으로 강제하지는 않는다.
따라서 상위 타입은 **레지스터 조합 규칙**으로 해석해야 한다.

권장 매핑:
- 1 register
  - `int16`, `uint16`
- 2 registers
  - `int32`, `uint32`, `float32`
- 4 registers
  - `int64`, `uint64`, `double`

주의:
- 장비마다 word order, byte order가 다를 수 있다.
- 프로젝트는 `TagDefinition`에 아래를 강제한다.

```text
TagDefinition
- dataType
- registerCount
- byteOrder
- wordOrder
- scale
- offset
```

## 10. 예외 응답 모델

Modbus는 오류 응답 시 **Function Code의 최상위 비트가 켜진 값**을 반환한다.
즉 요청이 `0x03` 이면 오류 응답 function code는 `0x83` 이다.
그 뒤에 **Exception Code**가 따라온다.

프로젝트는 이를 다음처럼 정규화한다.

```text
ProtocolError
- category
- functionCode
- exceptionCode
- message
```

대표 예외 범주:
- Illegal Function
- Illegal Data Address
- Illegal Data Value
- Server Device Failure

## 11. 연결 및 성능 정책

- 포트: 일반적으로 TCP 502
- 장치별 persistent connection 유지
- 요청은 connection 단위로 순차 처리
- polling 대상은 register block 기준으로 병합

권장값:
- connect timeout: 3s
- read timeout: 1s
- retry: 1~2회
- poll interval: 500ms ~ 5s (태그 중요도에 따라)

성능 팁:
- 태그별로 한 번씩 읽지 말고 연속 register 범위로 batch polling 한다.
- 예를 들어 `40001`, `40002`, `40003`, `40004`는 4번 요청하지 말고 한 번에 읽는다.

## 12. 보안 주의사항

Modbus TCP는 기본적으로 보안이 약하다.
따라서 다음을 기본 원칙으로 한다.

- 폐쇄망 사용
- 인터넷 직접 노출 금지
- ACL/방화벽 적용
- 쓰기 기능 기본 비활성화
- 허용 endpoint 및 허용 register 범위 화이트리스트
- 감사 로그 기록

## 13. 시뮬레이터 구현 가이드

시뮬레이터는 다음 수준이면 충분하다.

### 필수
- TCP listener on 502 (또는 테스트 포트)
- MBAP header 파싱
- 8개 기본 function code 처리
- 내부 coil/register 메모리 유지
- exception response 생성

### 내부 메모리 예시

```text
ModbusMemory
- coils: BitSet
- discreteInputs: BitSet
- inputRegisters: short[]
- holdingRegisters: short[]
```

### 권장 추가 기능
- 랜덤 값 생성
- 시뮬레이션 시나리오 스크립트
- 값 변화 주기 설정
- read-only / writable area 정책

## 14. MES 내부 표준 인터페이스 매핑

Modbus 어댑터는 내부적으로 다음을 수행한다.

1. 태그를 area별로 그룹핑한다.
2. 연속 offset을 block read로 합친다.
3. register 결과를 타입 규칙에 따라 디코딩한다.
4. 결과를 공통 `TagValue`로 반환한다.

예시 공통 값:

```text
TagValue
- tagId
- value
- quality
- timestamp
- sourceProtocol
- sourceAddress
```

## 15. 이 프로젝트에서의 위치

Modbus TCP는 Mitsubishi MC/SLMP보다 범용성이 높고, OPC UA보다 단순하다.
따라서 다음 역할로 적합하다.

- 게이트웨이 연동
- 테스트 장치 연동
- PLC가 아닌 주변 장치 연동
- 시뮬레이터 2차 구현

즉, **Mitsubishi 직접 연동 다음 우선순위**로 두는 것이 적절하다.

## 16. 참고 원문

- Modbus Organization, **MODBUS Application Protocol Specification V1.1b3**
- Modbus Organization, **MODBUS Messaging on TCP/IP Implementation Guide V1.0b**
