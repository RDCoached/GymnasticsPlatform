## 🔍 QA Agent: Test Coverage Gaps Detected

**Commit:** [`${{ env.COMMIT_SHORT_SHA }}`](${{ github.event.head_commit.url }}) by ${{ env.COMMIT_AUTHOR }}
**Message:** ${{ env.COMMIT_MESSAGE }}

---

### 📊 Coverage Analysis

${{ env.BACKEND_COVERAGE != '' && format('**Backend Coverage:** {0}%\n', env.BACKEND_COVERAGE) || '' }}${{ env.USER_PORTAL_COVERAGE != '' && format('**User Portal Coverage:** {0}%\n', env.USER_PORTAL_COVERAGE) || '' }}${{ env.ADMIN_PORTAL_COVERAGE != '' && format('**Admin Portal Coverage:** {0}%\n', env.ADMIN_PORTAL_COVERAGE) || '' }}

View coverage reports: [GitHub Actions Artifacts](${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}#artifacts)

---

### 🚨 Missing Test Files

${{ env.MISSING_TESTS != '' && format('The following source files were added or modified but lack corresponding test files:\n\n```\n{0}\n```\n', env.MISSING_TESTS) || 'All source files have corresponding test files. ✅' }}

---

### ⚠️ Edge Case Coverage Gaps

${{ env.EDGE_CASE_GAPS != '' && format('The following edge case patterns were not detected in tests:\n\n{0}\n', env.EDGE_CASE_GAPS) || 'Edge case patterns detected in tests. ✅' }}

---

### 🎯 Recommendations

#### Missing Test Files

${{ env.MISSING_TESTS != '' && 'For each source file without tests, create a corresponding test file:\n\n**Backend (.NET)**\n- `Foo.cs` → `tests/.../FooTests.cs`\n- Use xUnit with `[Fact]` and `[Theory]` attributes\n- Include integration tests with `WebApplicationFactory` + `Testcontainers`\n\n**Frontend (TypeScript/React)**\n- `Component.tsx` → `Component.test.tsx`\n- Use Vitest + React Testing Library\n- Test behavior through user interactions, not implementation details\n' || '' }}

#### Edge Case Coverage

Common edge cases to test:
- **Null/undefined handling**: Input is null, undefined, or missing
- **Empty collections**: Arrays/lists are empty
- **Boundary values**: Min/max values for numbers, empty strings vs whitespace
- **Error conditions**: Network failures, validation errors, database errors
- **Authentication/Authorization**: Unauthenticated requests, insufficient permissions
- **Race conditions**: Concurrent updates, idempotency
- **Invalid input**: Malformed data, type mismatches, SQL injection attempts

---

### 🛠️ Next Steps

The QA agent should:

1. **Create missing test files** for newly added source files
2. **Add edge case tests** for identified gaps
3. **Increase coverage** to target threshold (80%+ backend, 70%+ frontend)
4. **Follow TDD principles**: Red → Green → Refactor
5. **Test behavior, not implementation**: Use public APIs and user interactions
6. **Create PR** with new tests and explanation

---

### 📝 Manual Fix Instructions

To add missing tests locally:

```bash
# Checkout the commit
git checkout ${{ env.COMMIT_SHORT_SHA }}

# Backend: Create test file
# Example: src/Foo.cs → tests/FooTests.cs
dotnet new xunit -n FooTests -o tests
# Write tests using xUnit + WebApplicationFactory

# Frontend: Create test file
# Example: src/Component.tsx → src/Component.test.tsx
cd frontend/user-portal
# Write tests using Vitest + React Testing Library

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDirectory=./coverage
npm run test:coverage

# Review coverage report
open coverage/index.html
```

---

### 🔗 Resources

- **Backend Testing**: xUnit, WebApplicationFactory, Testcontainers
- **Frontend Testing**: Vitest, React Testing Library, Testing Library queries
- **Coverage Tools**: dotnet-reportgenerator, vitest coverage
- **TDD Workflow**: /tdd skill in Claude Code
- **Testing Patterns**: /testing skill in Claude Code

---

**Status:** 🟡 Awaiting Test Implementation

/label qa-agent test-gap automated priority-medium
