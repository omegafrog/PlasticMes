# 프로젝트 개요

이 프로젝트는 미쯔비시, Modbus 등 여러 종류의 PLC 프로토콜을 시뮬레이션하고, 플라스틱 사출 공정에 MES를 도입하기 위한 시스템을 개발하는 것을 목표로 한다.

# Build/Test 명령

추후 작성.

# 문서 네비게이션

- [docs/design-docs](docs/design-docs/index.md): 설계 의사결정과 검증 내용을 관리한다.
- [docs/product-specs](docs/product-specs/index.md): 사용자 요구사항, 기능 요구사항, 유스케이스를 관리한다.
- [docs/exec-plans/active](docs/exec-plans/active/index.md): 진행 중인 실행 계획을 관리한다.
- [docs/exec-plans/completed](docs/exec-plans/completed/index.md): 종료된 실행 계획을 관리한다.
- `docs/generated`: 생성되고 저장되어야 할 생성물을 보관한다.
- `docs/references`: 에이전트나 개발자가 참고할 문서를 보관한다.

# 문서 정책

- `docs/` 아래를 source of truth로 본다.

# 금지사항

추후 작성.

# 문서 검증

After creating or updating planning docs under `docs/`, run:

```bash
python scripts/validate_docs.py
```

If validation fails:

do not ignore failures
fix broken links, missing required sections, or invalid properties
rerun the command until it passes
