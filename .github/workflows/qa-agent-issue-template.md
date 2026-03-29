## 🤖 QA Agent: Test Failures Detected

**Commit:** [`${{ env.COMMIT_SHORT_SHA }}`](${{ github.event.head_commit.url }}) by ${{ env.COMMIT_AUTHOR }}
**Message:** ${{ env.COMMIT_MESSAGE }}

---

### ❌ Failed Test Suites

${{ env.BACKEND_FAILED == 'true' && '- **Backend Tests** (.NET/xUnit)\n' || '' }}${{ env.USER_PORTAL_FAILED == 'true' && '- **User Portal Tests** (Vitest)\n' || '' }}${{ env.ADMIN_PORTAL_FAILED == 'true' && '- **Admin Portal Tests** (Vitest)\n' || '' }}

### 📊 Test Results

View full test output: [GitHub Actions Run](${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }})

Download test logs: [Artifacts](${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}#artifacts)

---

### 🔍 Diagnostic Information

#### Changed Files in This Commit
```
${{ env.CHANGED_FILES }}
```

#### Next Steps

The QA agent should:

1. **Analyze the test failures** from the logs
2. **Identify root cause** by examining:
   - Changed files in the commit
   - Test error messages and stack traces
   - Missing mocks or test setup
   - API changes that broke existing tests
3. **Generate fixes** following TDD principles:
   - Update test mocks to match new API signatures
   - Add missing test setup/teardown
   - Fix test assertions to match new behavior
4. **Create PR** with fixes and explanation

---

### 🛠️ Manual Fix Instructions

To investigate and fix locally:

```bash
# Checkout the failing commit
git checkout ${{ env.COMMIT_SHORT_SHA }}

# Run tests locally
${{ env.BACKEND_FAILED == 'true' && 'dotnet test\n' || '' }}${{ env.USER_PORTAL_FAILED == 'true' && 'cd frontend/user-portal && npm test\n' || '' }}${{ env.ADMIN_PORTAL_FAILED == 'true' && 'cd frontend/admin-portal && npm test\n' || '' }}

# Use QA agent for automated fix
./scripts/qa-agent.sh ${{ env.COMMIT_SHORT_SHA }} analyze
./scripts/qa-agent.sh ${{ env.COMMIT_SHORT_SHA }} fix

# Or use Claude Code interactively
./scripts/qa-agent-claude.sh
```

---

### 📝 Common Failure Patterns

#### Frontend Test Failures
- **Unmocked API calls**: Component calls new API method that isn't mocked in tests
- **Missing props/context**: Component expects new props or context values
- **Async timing**: useEffect or async operations not properly awaited in tests

#### Backend Test Failures
- **Database state**: Tests expect specific database state that isn't set up
- **Missing dependencies**: New service dependencies not injected in tests
- **Authorization**: Tests need updated authentication/authorization setup

---

**Status:** 🔴 Awaiting Analysis

/label qa-agent test-failure automated priority-high
