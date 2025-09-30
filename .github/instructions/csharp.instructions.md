---
applyTo: "**/*.cs"
---

# C# 코드 스타일 및 제안 지침

목적: Copilot이 `*.cs` 파일에 대해 일관성 있고 안전한 제안을 하도록 프로젝트 규칙을 정의합니다.

요약
- 우선순위: 안정성 → 가독성 → 성능 → 테스트 용이성

코딩 규칙
- 모든 클래스와 public 멤버는 `PascalCase` 사용. 로컬 변수 및 매개변수는 `camelCase`.
- private 필드는 `_camelCase` 사용(예: `_logger`).
- 모든 public 메서드/타입에 XML 주석 사용 (`<summary>`, `<param>`, `<returns>`).
- 널러블 참조 타입 사용 권장: 파일 또는 프로젝트에서 `#nullable enable` 또는 프로젝트 수준 설정을 고려.

비동기/병렬
- I/O는 `async/await`로 구현하고 `Task` 반환을 우선 사용.
- 외부 호출(웹/DB)은 `CancellationToken`을 수용하도록 설계.
- 블로킹 API(`Thread.Sleep`) 대신 `await Task.Delay(...)` 사용.
- 병렬 처리 시 `SemaphoreSlim`으로 동시성 한도 설정.

예외 처리와 로깅
- 구체적 예외를 캐치하고 필요 시 로깅 후 재던지기(또는 래핑). 예외를 무시하지 말 것.
- 로깅은 `ILogger<T>`와 구조화 로그 사용 (`_logger.LogInformation("... {Id}", id)`).

리소스/수명관리
- `IDisposable`/`IAsyncDisposable`는 `using` 또는 `await using`으로 처리.
- 드라이버/소켓/스트림은 명확히 해제.

성능 및 확장성
- 빈번한 외부 호출은 캐싱 또는 배치 처리 적용.
- 무거운 작업은 백그라운드 큐(예: Azure Queue)로 오프로드 고려.

테스트
- 외부 종속성은 모킹 가능하도록 추상화하여 단위 테스트 작성.
- 통합 테스트는 실제 크롤러/드라이버 대신 `assets/`의 HTML 샘플을 사용.

포맷/정적분석
- 일관된 코드 스타일 유지(예: `dotnet format` 또는 __Format Document__).
- nullable 경고, CA(Analyzers) 등 빌드 분석을 통과하도록 제안.

보안
- 비밀(데이터베이스 연결 문자열, 크레덴셜)은 `local.settings.json` 또는 환경 변수로 관리하고 코드에 하드코딩 금지.

응답 스타일
- 제안은 한국어로 작성.
- 변경할 코드 블록 제공 시 간결한 설명(무엇을, 왜 변경했는지) 포함.
- 가능한 경우 `async` 기반 예제 제공 및 마이그레이션 팁 포함.