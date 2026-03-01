# Code Style

Naming conventions and formatting rules will be established as the architecture finalizes. See `ARCHITECTURE.md` for the current design direction.

## Diagnostics / Logging

Use `System.Diagnostics.Debug.WriteLine` for all diagnostic or debug output — never `Console.WriteLine`. Debug output is visible in the Visual Studio Output window (Debug pane) and is automatically stripped from Release builds.

## Test Organization

Test files must mirror the engine's namespace and folder structure. A test for a type in `FlatRedBall2.Collision` goes in a `Collision/` subfolder with `namespace FlatRedBall2.Tests.Collision`. Tests for root-namespace types (`FlatRedBall2`) stay at the test project root with `namespace FlatRedBall2.Tests`.

## Test Guidelines

### Philosophy — Minimal Coverage

We are not targeting 100% coverage. Write the fewest tests that meaningfully verify the feature:

- **Minimal by default**: If a single feature was added, aim for 1–3 tests. Do not write tests for every permutation.
- **Critical paths only**: Test the main success path and the most important failure scenario. Skip edge cases unless they are a realistic source of bugs.
- **Short**: Each test should be as short as possible. Remove any line that does not directly contribute to the assertion.
- **Comments when needed**: Add a brief comment if there is any realistic chance a reader will be confused about *why* a value was chosen or what the test is verifying. Omit comments when the test is self-evident.

### Assertion Library

Always use **Shouldly** for assertions — never `Assert.*` from xunit. Use `ShouldBe`, `ShouldBeTrue`, `ShouldBeFalse`, `ShouldBeNull`, `ShouldContain`, `ShouldBeEmpty`, `Should.Throw`, etc. For floats with rounding error, pass a `tolerance:` parameter.

### Explicit Test Arrangements

Every test must explicitly declare all values it will assert against in its Arrange section. Do NOT rely on shared helper methods to define expected values. If a test checks that a value equals `"Screens"`, the string `"Screens"` must appear in the test's Arrange section.

Helper methods are fine for common setup (file creation, object initialization), but should accept parameters for the specific values being tested. This makes tests self-contained and prevents hidden dependencies.

**Pattern**: If you assert a specific value, that value must be explicitly declared in the test's Arrange section.
