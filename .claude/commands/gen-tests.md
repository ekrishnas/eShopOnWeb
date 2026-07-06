---
description: Generate xUnit v3 tests for a class following repo conventions
---

Target: $ARGUMENTS (a class or method under `src/`).

1. Read the target and its dependencies; identify observable behaviors —
   including failure paths and boundary conditions, not just happy paths.
2. Mirror repo conventions: xUnit v3, NSubstitute for interfaces,
   folder-per-class / file-per-method layout under the matching
   `tests/UnitTests/<Layer>/<Area>/<ClassName>Tests/` path, file-scoped
   namespaces, `RootNamespace Microsoft.eShopWeb.UnitTests`.
3. Name tests `<Behavior>_<Condition>` style, one assert-concept per test.
4. Do NOT touch the class under test. If it is untestable as designed,
   report why instead of writing brittle tests.
5. Run `dotnet test tests/UnitTests/UnitTests.csproj` and iterate until
   green. Show the final test-run summary.
