# QA Agent

Autonomous test quality assurance using Claude Code agents.

## Responsibilities

The QA agent ensures comprehensive test coverage by:

1. **Fix Failing Tests** - Analyzes and repairs broken tests
2. **Identify Test Gaps** - Finds source files without tests
3. **Add Missing Tests** - Creates tests for untested code
4. **Find Edge Cases** - Identifies missing edge case coverage
5. **Add Edge Case Tests** - Tests null checks, errors, boundaries, etc.

## Invocation

Automatically spawned by Claude after committing code.

The agent will:
- Run all tests and fix failures
- Analyze codebase for test gaps
- Check for missing edge cases:
  - Null/undefined handling
  - Empty collections
  - Boundary values (min/max, empty strings)
  - Error conditions (network failures, validation errors)
  - Authentication/authorization edge cases
  - Race conditions and idempotency
- Create comprehensive tests to fill gaps
- Create PR with all improvements

## What Gets Tested

**Backend (.NET/xUnit)**
- Controllers/Endpoints
- Services and business logic
- Domain entities
- Validators
- Edge cases: null inputs, invalid data, authorization

**Frontend (Vitest/React Testing Library)**
- Components and user interactions
- Hooks and state management
- API integration (mocked)
- Edge cases: loading states, errors, empty data

## Manual Invocation

Ask Claude anytime:
- "Run QA agent"
- "Check test coverage and fill gaps"
- "Find missing edge cases"

## Example

```
You: "Add new feature X"
Claude: [implements feature, commits]
Claude: [spawns QA agent]

Agent:
  ✅ All tests passing
  ⚠️  Found gaps:
     - Feature X has no tests
     - Missing error handling tests in Y
     - No edge case for empty input in Z
  ✅ Added 15 new tests
  ✅ Created PR with comprehensive coverage

You: Review PR → Merge → 100% coverage!
```
