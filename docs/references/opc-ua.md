# OPC UA 프로젝트용 요약 스펙

## 1. 목적

이 문서는 MES가 OPC UA 서버와 연동할 때 필요한 핵심 개념과 구현 범위를 정리한다.
OPC UA는 Mitsubishi MC/SLMP나 Modbus TCP처럼 단순 레지스터 읽기 중심의 프로토콜이 아니라, **정보 모델 + 서비스 + 보안 + 세션**을 함께 제공하는 산업 표준이다.

이 프로젝트에서는 **MES가 OPC UA Client** 역할을 수행하는 경우를 우선 범위로 잡는다.

## 2. 왜 필요한가

OPC UA는 벤더 중립적인 산업 데이터 교환 표준이다.
PLC 자체가 OPC UA 서버를 제공할 수도 있고, 별도의 게이트웨이/미들웨어가 OPC UA 서버를 제공할 수도 있다.
따라서 MES 관점에서는 다음 둘 다 상대할 수 있어야 한다.

- PLC 내장 OPC UA Server
- 외부 OPC UA Gateway/Server

## 3. 핵심 개념

OPC UA는 단순히 값만 읽는 규격이 아니다. 다음이 함께 묶여 있다.

- AddressSpace
- Node
- Attribute
- Method
- Session
- Subscription
- Security
- Information Model

즉, OPC UA 서버는 단순 레지스터 맵이 아니라 **탐색 가능한 주소 공간(AddressSpace)** 을 제공하고, 클라이언트는 그 안의 노드를 Browse / Read / Write / Subscribe 한다.

## 4. 클라이언트/서버 모델

이 프로젝트의 기본 전제는 다음과 같다.

- MES: OPC UA Client
- PLC 또는 게이트웨이: OPC UA Server

기본 흐름:
1. Endpoint discovery
2. SecureChannel 생성
3. Session 생성
4. Node browse
5. Read / Write / Subscription 등록
6. Publish 응답 수신

따라서 OPC UA는 단순 polling 프로토콜이 아니라, 세션과 구독이 있는 **상태성 프로토콜**로 보는 것이 맞다.

## 5. 프로젝트 구현 범위

### 5.1 1차 범위

- Client만 구현
- Endpoint 연결
- Browse
- Read
- Write
- Subscription / MonitoredItem
- 기본 인증서 검증
- NodeId 기반 태그 매핑

### 5.2 제외 범위

- OPC UA Server 직접 구현
- PubSub
- Historical Access
- Alarms & Conditions 고급 기능
- Complex custom information model 생성
- Reverse Connect 등 고급 배포 시나리오

## 6. AddressSpace 모델

OPC UA 서버는 AddressSpace 안에 Node를 배치한다.
대표적으로 다음 노드 클래스가 중요하다.

- Object
- Variable
- Method
- DataType
- ReferenceType
- ObjectType
- VariableType
- View

MES 입장에서는 우선 **Variable Node**가 가장 중요하다.
대부분의 실시간 설비 값은 Variable Node의 `Value` Attribute에서 읽는다.

예시:
- `/Line1/Press01/Status`
- `/Line1/Press01/Counter`
- `/Line1/Press01/AlarmCode`

실제 구현에서는 경로 문자열 대신 보통 `NodeId`를 사용한다.

## 7. 읽기 모델

### 7.1 Browse

서버 구조를 탐색한다.
처음 연동 시 태그 사전을 자동 생성하거나, 디버깅/운영 도구에서 유용하다.

### 7.2 Read

지정된 NodeId의 Attribute 값을 읽는다.
가장 흔한 대상은 `Value` Attribute다.

### 7.3 Write

허용된 Variable Node에 값을 쓴다.
하지만 운영 환경에서는 보수적으로 제한해야 한다.

## 8. Subscription 모델

OPC UA의 큰 장점은 **Subscription + MonitoredItem** 이다.
즉, MES가 서버에 구독을 걸어두면 값 변경을 주기적으로 밀어받을 수 있다.

중요 포인트:
- 변화 감지는 server-side monitored item이 담당한다.
- 실제 전송은 publish cycle에 맞춰 온다.
- 값, 상태코드, 소스 타임스탬프를 함께 받을 수 있다.

프로젝트 권장 정책:
- 자주 변하는 실시간 태그는 Subscription 사용
- 저빈도 태그나 설정성 태그는 On-demand Read 사용

## 9. 데이터 타입

OPC UA는 단순 coil/register보다 훨씬 풍부한 타입 시스템을 가진다.
프로젝트 1차 범위는 아래만 공식 지원한다.

- Boolean
- SByte / Byte
- Int16 / UInt16
- Int32 / UInt32
- Int64 / UInt64
- Float / Double
- String
- DateTime

추후:
- Enum
- Structure / ExtensionObject
- Array
- LocalizedText

프로젝트 공통 정규화 예시:

```text
TagValue
- tagId
- nodeId
- dataType
- value
- statusCode
- sourceTimestamp
- serverTimestamp
- quality
```

## 10. 보안 모델

OPC UA는 MC/SLMP, Modbus TCP와 달리 보안이 스펙의 핵심 일부다.
주요 보안 요소:

- Application Instance Certificate
- SecureChannel
- Session
- User Identity Token
- Message Security
- Signing / Encryption

프로젝트 정책:
- 개발 환경에서도 가능한 한 보안 모드 없는 연결을 기본으로 삼지 않는다.
- 최소한 서버 인증서 검증 옵션을 제공한다.
- self-signed certificate 허용 여부를 환경설정으로 분리한다.

## 11. 오류 처리

OPC UA는 단순 exception code 하나만 보는 구조가 아니라, 서비스 결과와 노드별 `StatusCode`를 함께 봐야 한다.

오류 범주:
- endpoint discovery 실패
- secure channel 실패
- session 생성 실패
- browse 실패
- read/write 실패
- monitored item 생성 실패
- publish timeout
- bad status code

프로젝트 표준 오류 모델:

```text
ProtocolError
- category
- statusCode
- message
- endpoint
- nodeId
```

## 12. 연결 및 세션 정책

- endpoint 단위 long-lived session 유지
- subscription은 기능별로 분리
  - fast tags
  - slow tags
  - alarm-like tags
- reconnect 전략 필요
- session 재생성 후 monitored item 재등록 필요

권장값 예시:
- reconnect backoff: 1s, 2s, 5s, 10s
- publish interval: 250ms ~ 1000ms
- keep-alive count: 서버 정책에 맞춤
- sampling interval: 태그 중요도에 따라 차등

## 13. 시뮬레이터/테스트 전략

OPC UA는 직접 서버를 구현하기보다 기존 샘플 서버나 상용 시뮬레이터를 활용하는 편이 낫다.
이 프로젝트에서는 다음 두 방식이 가능하다.

### 방식 A: 실제 OPC UA 서버에 연결
- PLC 내장 OPC UA 서버
- 게이트웨이 서버

### 방식 B: 테스트용 OPC UA 서버 사용
- 샘플 서버
- 산업용 시뮬레이터
- 자체 테스트 서버

하지만 MES 본체는 어디에 연결하든 동일하게 **Client** 역할만 수행하면 된다.

## 14. MES 내부 표준 인터페이스 매핑

OPC UA 어댑터는 내부적으로 다음을 수행한다.

1. endpoint connect
2. session establish
3. node browse 또는 사전 정의된 node map 로드
4. read/write/subscribe 수행
5. 상태코드와 타임스탬프를 공통 모델로 정규화

예시:

```text
OpcUaTagBinding
- tagId
- endpointUrl
- nodeId
- attributeId (= Value 기본)
- samplingInterval
- subscriptionGroup
```

## 15. 이 프로젝트에서의 위치

OPC UA는 가장 범용적이고 구조적으로 깔끔하지만, 초기 구현 난이도가 세 프로토콜 중 가장 높다.
따라서 프로젝트 우선순위는 다음이 적절하다.

1. Mitsubishi MC/SLMP
2. Modbus TCP
3. OPC UA

다만 장기적으로는 OPC UA가 **가장 표준적인 northbound/southbound 연결점**이 될 가능성이 크므로, 내부 도메인 모델은 OPC UA를 수용할 수 있게 설계하는 것이 좋다.

## 16. 참고 원문

- OPC Foundation, **OPC 10000-1: UA Part 1: Overview and Concepts**
- OPC Foundation, **OPC 10000-2: UA Part 2: Security**
- OPC Foundation, **OPC 10000-4: UA Part 4: Services**
- OPC Foundation, **OPC 10000-6: UA Part 6: Mappings**
- OPC Foundation, **OPC Unified Architecture** overview brochure
