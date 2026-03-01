# Code Style

Naming conventions and formatting rules will be established as the architecture finalizes. See `ARCHITECTURE.md` for the current design direction.

## Diagnostics / Logging

Use `System.Diagnostics.Debug.WriteLine` for all diagnostic or debug output — never `Console.WriteLine`. Debug output is visible in the Visual Studio Output window (Debug pane) and is automatically stripped from Release builds.

## Test Guidelines

### Philosophy — Quality over Coverage

Prioritize maintainability and clarity over exhaustive coverage:

- **Critical paths**: Test the main success path and the most important failure scenarios
- **Key edge cases**: Null/empty inputs, boundary conditions — but only those likely to cause real issues
- **High-value tests**: Tests that would catch real bugs, not every possible permutation
- **Avoid verbosity**: If similar scenarios require nearly identical tests, consider combining them or testing only the most representative case
- **Maintainability**: Prefer fewer, clear tests over many verbose tests. Each test should justify its existence by testing something meaningfully different. If a single feature was added, aim for 1-3 tests, not more.

### Explicit Test Arrangements

Every test must explicitly declare all values it will assert against in its Arrange section. Do NOT rely on shared helper methods to define expected values. If a test checks that a value equals `"Screens"`, the string `"Screens"` must appear in the test's Arrange section.

Helper methods are fine for common setup (file creation, object initialization), but should accept parameters for the specific values being tested. This makes tests self-contained and prevents hidden dependencies.

**Pattern**: If you assert a specific value, that value must be explicitly declared in the test's Arrange section.
