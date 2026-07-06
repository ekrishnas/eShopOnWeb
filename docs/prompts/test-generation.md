# Prompt: Test Generation

Use this prompt to generate unit tests for any service or domain class in this repo.

---

## Prompt

```
Read CLAUDE.md first.

I need unit tests for `[CLASS NAME]` in `[FILE PATH]`.

Before writing any tests:
1. Read the class and understand what it does
2. List the scenarios that need testing (happy path, edge cases, error cases)
3. Check `tests/UnitTests/ApplicationCore/Services/BasketServiceTests/AddItemToBasket.cs` as the canonical example of test style in this project

Then write the tests following these rules:
- Framework: xUnit ([Fact] and [Theory])
- Mocking: NSubstitute only (never Moq)
- One class per scenario, named as a verb phrase (e.g. `CreateOrderWithMissingCatalogItem`)
- All in namespace `Microsoft.eShopWeb.UnitTests.ApplicationCore.Services.[ClassName]Tests`
- Place files in `tests/UnitTests/ApplicationCore/Services/[ClassName]Tests/`
- Each test asserts one thing (single Act + single Assert or Received() check)
- Use `Arg.Any<T>()` for NSubstitute argument matchers
- Test for Guard clause exceptions using `Assert.ThrowsAsync<ArgumentException>(...)` or the specific domain exception

Scenarios to cover at minimum:
- Happy path: normal inputs produce expected output and expected repository/service calls
- Guard clause: null or empty inputs throw the expected exception
- Edge case: [describe any domain-specific edge cases for this class]

Write the tests now. Create one file per scenario.
```

---

## When to Use

- After writing a new service method
- When closing a known test coverage gap
- When an AI tool made changes and tests are needed to validate them

## Known Coverage Gaps (as of July 2025)

- `OrderService.CreateOrderAsync` — no unit tests exist
- `UriComposer.ComposePicUri` — no unit tests exist
- PublicApi: `UpdateCatalogItemEndpoint`, `CatalogBrandListEndpoint`, `CatalogTypeListEndpoint`

## Related Prompts

- `code-review.md` — review tests after generation
