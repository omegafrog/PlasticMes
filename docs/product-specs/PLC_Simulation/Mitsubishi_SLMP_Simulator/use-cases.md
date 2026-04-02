# Properties

status: verified

last_verified: 2026-04-02

# 유스케이스 정의

## UC-001

- 액터: 개발자 또는 테스트 엔지니어
- 액션: WinForms host가 기본 loopback endpoint(`127.0.0.1:5000`)와 기본 station/module 설정으로 simulator server를 시작한다.

## UC-002

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: X/Y/M/D에 대해 Batch Read 요청을 보내고 값을 읽는다.

## UC-003

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: 허용된 X/Y/M/D 범위에 Batch Write 요청을 보내고 값을 쓴다.

## UC-004

- 액터: MES 어댑터 또는 운영자
- 액션: 잘못된 프레임, 잘못된 주소, 연결 문제 발생 시 표준 End code와 상태로 오류를 확인한다.

## UC-005

- 액터: WinForms 호스트 또는 테스트 코드
- 액션: 시뮬레이터의 실행 상태, 포트 바인딩 상태, 연결 수를 조회하고 WinForms title 또는 테스트 assertion에 반영한다.

## UC-006

- 액터: 개발자 또는 테스트 엔지니어
- 액션: 시뮬레이터 시작 시 CSV 파일 경로를 제공하고 replay 시나리오를 로드한다.

## UC-007

- 액터: CSV replay 스케줄러
- 액션: 첫 데이터 row를 기준 시점으로 삼아 각 row의 register 값을 상대 ms 시점에 메모리에 반영한다.

## UC-008

- 액터: 개발자 또는 운영자
- 액션: `time` 컬럼 누락, 주소 파싱 실패, 시간 역전, 값 파싱 실패 같은 CSV 오류를 확인한다.

## UC-009

- 액터: MES 어댑터 또는 테스트 클라이언트
- 액션: CSV replay 중 변경된 register 값을 SLMP Batch Read로 조회한다.
