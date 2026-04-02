# Properties

status: verified

last_verified: 2026-04-02

# 유스케이스 정의

## UC-001

- 액터: 개발자 또는 테스트 엔지니어
- 액션: `PlasticMes.ModbusSimulatorHost`를 `--bind`, `--port`, `--unit-id`, `--csv` 인자로 시작하고 기본값 `127.0.0.1:1502`, `unit-id=1`을 사용할 수 있다.

## UC-002

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: `Read Coils`, `Read Discrete Inputs`, `Read Holding Registers`, `Read Input Registers` 요청으로 현재 값을 읽는다.

## UC-003

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: `Write Single Coil`, `Write Multiple Coils`, `Write Single Register`, `Write Multiple Registers` 요청으로 쓰기 가능한 영역 값을 변경한다.

## UC-004

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: 지원하지 않는 function code, 잘못된 주소, 잘못된 payload에 대해 Modbus exception response를 확인한다.

## UC-005

- 액터: 운영자 또는 테스트 코드
- 액션: 실행 상태, 포트, 연결 수, 마지막 오류를 조회한다.

## UC-006

- 액터: 개발자 또는 테스트 엔지니어
- 액션: simulator 시작 시 `--csv`로 CSV 파일 경로를 제공하고 replay 시나리오를 로드한다.

## UC-007

- 액터: CSV replay 스케줄러
- 액션: 각 row의 `time`에 따라 해당 시점의 메모리 값을 row 값으로 반영한다.

## UC-008

- 액터: 개발자 또는 운영자
- 액션: `time` 누락, 헤더 중복, 주소 파싱 실패, 컬럼 수 불일치, 빈 셀, 시간 역전, 값 파싱 실패 같은 CSV 오류를 확인한다.

## UC-009

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: replay 중 변경된 값을 Modbus read 요청으로 즉시 조회한다.
