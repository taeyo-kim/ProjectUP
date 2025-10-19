---
mode: 'agent'
model: Claude Sonnet 3.7
description: 'write Unit Test Code'
---
단위 테스트 코드를 작성해 주세요.

* 테스트 프레임워크로는 `xUnit` 또는 `NUnit`을 사용합니다.
* 테스트 코드는 `test/` 폴더에 위치합니다.
* 각 테스트 파일은 `*.Tests.cs` 형식을 따릅니다.
* 테스트 클래스는 `ClassNameTests` 형식을 따릅니다.
* 테스트는 독립적이며 반복 실행 가능해야 합니다.
* `xUnit` 또는 `NUnit` 기반의 테스트 코드를 제안해주세요.
* `Moq`, `FluentAssertions` 등의 라이브러리를 활용하여 가독성 높은 테스트 코드를 작성합니다.
* `Moq`를 활용한 의존성 주입 및 Mock 객체 생성을 추천해주세요.
* 각 테스트 메서드는 `MethodName_Should_ExpectedBehavior_When_State` 형식을 따릅니다.
* 예외 상황 및 경계 조건에 대한 테스트도 포함해주세요.
* 테스트 코드에는 설명 주석을 포함하여 가독성을 높여주세요.
* 테스트는 빠르고 독립적으로 실행 가능해야 하며, 외부 환경에 의존하지 않도록 구성해주세요.
