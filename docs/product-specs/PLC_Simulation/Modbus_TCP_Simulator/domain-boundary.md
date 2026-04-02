# Properties

status: verified

last_verified: 2026-04-02

# 도메인 경계 선정

## 도메인

- `PLC Simulation / Modbus TCP Simulator`

## 하위 기능 경계

- `Timed CSV Register Replay`

## 책임

- MBAP Header와 PDU를 파싱하고 Modbus TCP 요청과 응답을 처리한다.
- `Coil`, `Discrete Input`, `Input Register`, `Holding Register` 메모리 영역을 유지한다.
- CSV 시나리오를 로드하고 `time` 기준 상대 스케줄로 메모리를 갱신한다.
- CSV replay와 Modbus read/write가 같은 메모리 저장소를 공유하도록 유지한다.
- `PlasticMes.ModbusSimulator` 라이브러리와 `PlasticMes.ModbusSimulatorHost` 실행 진입점을 분리해 Mitsubishi 구현과 조립 경계를 분리한다.

## 포함 범위

- `0x01`, `0x02`, `0x03`, `0x04`, `0x05`, `0x06`, `0x0F`, `0x10` 지원
- MBAP Header 검증과 Modbus exception response 처리
- CSV 검증, 시간 정규화, TCP host
- `time` 기준 상대 시간 replay
- read/write와 replay가 동일 메모리 저장소를 공유하는 구조
- `--bind`, `--port`, `--unit-id`, `--csv` CLI 인자를 통한 host 조립
- 테스트와 실행 계획 수립

## 제외 범위

- `Modbus RTU/ASCII`
- TLS
- 브리지 뒤 다중 Unit 라우팅
- CSV live reload
- loop, pause, seek
- 빈 셀을 "변경 없음"으로 해석하는 sparse semantics
- `int32/float` 조합 타입
