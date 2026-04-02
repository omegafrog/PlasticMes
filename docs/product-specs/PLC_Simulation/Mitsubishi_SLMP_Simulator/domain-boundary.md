# Properties

status: verified

last_verified: 2026-04-02

# 도메인 경계 선정

## 도메인

- `PLC Simulation / Mitsubishi SLMP Simulator`

## 하위 기능 경계

- `Timed CSV Register Replay`

## 책임

- SLMP 3E Frame / TCP / Binary 요청을 수신한다.
- CSV 헤더를 register 주소로 해석하고 replay 시나리오를 로드한다.
- `time` 기준 상대 ms 스케줄을 계산해 같은 메모리 저장소에 register snapshot을 반영한다.
- replay 중에도 SLMP Batch Read/Batch Write가 동일 저장소를 통해 최신 상태를 공유하도록 유지한다.

## 포함 범위

- CSV 헤더를 register 주소로 해석
- `time` 기준 상대 ms 스케줄 계산
- 계산된 시각에 `IDeviceMemoryStore`를 통해 register 반영
- SLMP read/write와 동일 메모리 저장소 공유
- 잘못된 CSV에 대한 즉시 검증 실패
- 기존 TCP listener 및 연결 수명주기 유지
- 기존 3E Binary frame 파싱/인코딩 유지
- 기존 X/Y/M/D 메모리 모델 유지

## 제외 범위

- 4E/ASCII/UDP
- Random Read/Write
- CSV live reload
- 반복 재생(loop), 배속 재생, 일시정지 UI
- `bool/int32/float` 같은 상위 타입 조합 해석
- 빈 셀을 "변경 없음"으로 해석하는 sparse row semantics
