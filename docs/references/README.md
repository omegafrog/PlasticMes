# MES Southbound Protocol Specs

이 문서는 MES 사이드 프로젝트에서 남향(southbound) 장비 연동을 위해 우선 지원할 3가지 프로토콜의 프로젝트용 요약 스펙 문서 모음이다.

대상 프로토콜:
- Mitsubishi MC Protocol / SLMP
- Modbus TCP
- OPC UA

문서 목적:
- PLC/게이트웨이/시뮬레이터와 통신하기 위한 최소 공통 이해를 정리한다.
- 프로젝트 구현 범위를 명확히 한다.
- 실제 코드 작성 전에 주소 체계, 세션 모델, 읽기/쓰기 방식, 오류 처리, 보안 포인트를 합의한다.

문서 목록:
- [`mitsubishi-slmp-mc.md`](mitsubishi-slmp-mc.md)
- [`mitsubishi-slmp-simulator.md`](mitsubishi-slmp-simulator.md)
- [`modbus-tcp.md`](modbus-tcp.md)
- [`opc-ua.md`](opc-ua.md)

권장 구현 순서:
1. Mitsubishi MC/SLMP 3E Frame (TCP, Binary)
2. Modbus TCP
3. OPC UA Client/Subscription

공통 설계 원칙:
- MES 내부에서는 프로토콜별 주소를 직접 노출하지 않고 `TagId`, `DataType`, `Quality`, `Timestamp` 공통 모델로 정규화한다.
- 각 프로토콜 어댑터는 `read`, `write`, `subscribe/poll`, `health check`의 동일 인터페이스를 제공한다.
- 시뮬레이터 단계에서는 안전을 위해 쓰기 범위를 제한한다.
- 운영 단계에서는 보안 없는 레거시 프로토콜(MC/SLMP, Modbus TCP)은 폐쇄망 또는 게이트웨이 뒤에서만 사용한다.

참고:
- 본 문서는 프로젝트 구현용 요약 스펙이다.
- 벤더/표준 단체의 원본 규격서를 대체하지 않는다.
