---
applyTo: "**/*.json"
---

# JSON 파일 작성 지침 (ProjectUP)

목적: Copilot이 프로젝트의 JSON 파일(.json, .jsonc 포함)에 대해 일관되고 유효한 제안을 하도록 규칙을 정의합니다.

주요 원칙
- 항상 유효한 JSON을 생성할 것(주석 금지 in .json). 개발 중 설명이 필요하면 `.jsonc` 사용 권장.
- 안정성 → 가독성 → 호환성 순으로 우선순위 적용.
- 민감 정보(시크릿, 연결 문자열 등)는 절대 코드에 하드코딩하지 말고 환경 변수 또는 로컬 비밀 저장소 사용.

포맷/인코딩
- 인덴트: 스페이스 2칸.
- 라인 끝: LF (\n).
- 인코딩: UTF-8 (BOM 없이).
- 트레일링 콤마 금지(유효하지 않음).
- 모든 키는 camelCase 사용(설명서나 외부 규격이 따로 있으면 그 규격을 우선).

스키마 및 검증
- 가능하면 JSON Schema를 작성하여 주요 구성 파일에 `$schema` 참조를 포함.
- Copilot이 JSON을 생성할 때는 스키마에 맞춰 값을 채우고, 누락된 필드는 명시적으로 null 또는 빈 값으로 처리하지 말고 스키마 요구사항을 따를 것.
- 제안된 JSON은 항상 JSONLint 또는 유사 검증 도구로 검사 가능하도록 유효성 유지.

환경별 구성
- 로컬 개발 전용 설정은 `local.settings.json` 또는 `appsettings.Development.json`에 두되, `local.settings.json`은 .gitignore에 추가하여 커밋 금지.
- 프로덕션 설정은 배포 파이프라인/환경 변수로 관리.
- 파일에 시크릿이 포함된 경우 Copilot은 경고를 표시할 것(예: "비밀이 포함되어 있습니다 — 환경 변수 사용 권장").

메타데이터 및 주석
- `.json` 파일에는 주석을 포함하지 말 것. 설명이 필요하면 별도 `.md` 문서나 `.jsonc` 파일을 사용.
- 파일 상단에 스키마 참조(`$schema`)나 버전(`version`) 필드를 권장.

도구/에디터 통합
- 편집 시 __Format Document__ (또는 `dotnet format`) 사용 권장.
- Visual Studio/VSCode에서 JSON Schema 연동 활성화 권장.

Copilot에 대한 지침
- JSON 생성 시: 유효한 JSON 문자열만 출력하고, 주석이나 설명 텍스트는 별도의 마크다운 블록으로 분리해서 제공.
- 예제 JSON 생성 시 실제 비밀값은 `"<REDACTED>"` 또는 환경 변수 자리표시자로 대체.
- 변경 제안이 있을 때는 "무엇을, 왜 바꿨는지" 한 줄 요약 포함.

간단 예시
- 파일 상단에 스키마를 참조하는 형태:
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "version": "1.0",
  "settings": { ... }
}