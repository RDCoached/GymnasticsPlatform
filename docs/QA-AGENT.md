# QA Autonomous Agent

An automated quality assurance system that monitors code changes, runs tests, fixes failures, and generates new tests.

## 🎯 Purpose

The QA Agent automatically:
- ✅ Detects when tests fail after commits
- 🔍 Analyzes test failures and identifies root causes
- 🔧 Creates pull requests with fixes
- 📝 Generates tests for new features that lack coverage
- 📊 Creates detailed analysis reports

## 🚀 Quick Start

### Automatic Mode (Recommended)

The QA agent runs automatically on every push to `main`:

1. Push code to main branch
2. Wait for CI to complete
3. If tests fail:
   - GitHub issue created with analysis
   - PR created with fixes (if agent can fix them)
4. Review and merge the PR

### Manual Mode

Run the QA agent locally:

```bash
# Analyze current changes
./scripts/qa-agent.sh HEAD analyze

# Generate fixes for current changes
./scripts/qa-agent.sh HEAD fix

# Full analysis + fixes
./scripts/qa-agent.sh HEAD full
```

## 📋 How It Works

### 1. Change Detection

When code is pushed, the agent:
- Identifies changed files
- Categorizes changes (backend, user-portal, admin-portal)
- Detects new features without tests

### 2. Test Execution

Runs appropriate test suites:
- **Backend**: `dotnet test` for C# changes
- **User Portal**: `npm run test:ci` for user-portal changes
- **Admin Portal**: `npm run test:ci` for admin-portal changes

### 3. Failure Analysis

For each failed test:
- Parses error messages
- Identifies failure type (assertion, runtime, timeout, setup)
- Locates relevant code changes
- Determines root cause

### 4. Fix Generation

Creates fixes based on failure patterns:

| Failure Type | Fix Strategy |
|--------------|--------------|
| Assertion failure | Update test expectation or fix implementation |
| Runtime error | Add null checks, fix logic errors |
| Timeout | Increase timeout or fix async code |
| Mock issue | Fix mock setup or isolation |
| TDZ error | Reorder declarations |

### 5. Missing Test Detection

Scans for new features without tests:
- New API endpoints → Integration test
- New React components → Component test
- New service methods → Unit test

### 6. PR Creation

Creates a pull request with:
- Fixed tests
- New tests for uncovered features
- Detailed explanation of changes
- Link to original issue

## 🔧 Configuration

### Enable/Disable QA Agent

Edit `.github/workflows/qa-agent.yml`:

```yaml
on:
  push:
    branches: [main]  # Comment out to disable
  workflow_dispatch:  # Keep for manual triggering
```

### Adjust Test Patterns

Modify file pattern detection in `scripts/qa-agent.sh`:

```bash
# Add new patterns
if [[ $file == custom-path/* ]]; then
    CUSTOM_CHANGED=true
fi
```

### Change Trigger Conditions

Edit workflow to trigger on different events:

```yaml
on:
  pull_request:  # Run on PRs instead
    branches: [main]
  schedule:      # Run on schedule
    - cron: '0 0 * * *'  # Daily at midnight
```

## 📊 Report Structure

The QA agent generates detailed reports:

```markdown
# QA Agent Analysis Report

**Commit:** `abc123`
**Date:** 2024-01-15

## Test Results
| Component | Status |
|-----------|--------|
| Backend | ✅ Passed |
| User Portal | ❌ Failed |

## Missing Tests
- src/NewFeature.tsx (expected: src/NewFeature.test.tsx)

## Recommendations
1. Fix failing tests (see logs)
2. Create tests for new features
```

## 🐛 Troubleshooting

### Issue: Agent not triggering

**Symptoms:** No issue/PR created after failed tests

**Solutions:**
1. Check GitHub Actions permissions:
   ```yaml
   permissions:
     contents: write
     pull-requests: write
     issues: write
   ```

2. Verify workflow is enabled in repo settings
3. Check workflow logs for errors

### Issue: Tests passing locally but failing in CI

**Symptoms:** Local tests pass, CI tests fail

**Solutions:**
1. Check for environment-specific issues:
   - Timezone differences
   - File path case sensitivity (Linux vs macOS/Windows)
   - Missing environment variables

2. Run tests in CI mode locally:
   ```bash
   npm run test:ci  # Frontend
   dotnet test --no-restore  # Backend
   ```

### Issue: Agent creates wrong fixes

**Symptoms:** PR changes don't fix the actual problem

**Solutions:**
1. Review the generated prompt:
   ```bash
   cat qa-agent-prompt.txt
   ```

2. Run manually with more context:
   ```bash
   claude chat --project=. < qa-agent-prompt.txt
   # Provide additional context in chat
   ```

3. Check test logs for full error details:
   ```bash
   cat qa-backend-tests.log
   cat qa-user-portal-tests.log
   cat qa-admin-portal-tests.log
   ```

### Issue: Missing tests not detected

**Symptoms:** New features added but agent doesn't create tests

**Solutions:**
1. Verify files are tracked by git:
   ```bash
   git status  # Should show as committed, not untracked
   ```

2. Check file naming conventions match detection patterns:
   - Backend: `src/*.cs` → `tests/*Tests.cs`
   - Frontend: `*.tsx` → `*.test.tsx`

3. Ensure changes are in the analyzed commit:
   ```bash
   git diff HEAD~1 HEAD --name-only
   ```

## 🔍 Manual Investigation

When automatic fixes don't work, investigate manually:

### 1. Generate Analysis Report

```bash
./scripts/qa-agent.sh HEAD analyze
cat qa-agent-report.md
```

### 2. Review Test Logs

```bash
# Backend tests
cat qa-backend-tests.log | grep -A 20 "Failed"

# Frontend tests
cat qa-user-portal-tests.log | grep -A 20 "FAIL"
```

### 3. Interactive Fix Session

```bash
# Generate prompt for Claude
./scripts/qa-agent.sh HEAD fix

# Review prompt
cat qa-agent-prompt.txt

# Start interactive session
claude chat --project=.
# Then paste prompt or load from file
```

## 📝 Best Practices

### For Developers

1. **Write tests first (TDD)** — Agent won't need to generate as many tests
2. **Keep tests fast** — Avoid timeouts and slow CI runs
3. **Follow naming conventions** — Helps agent detect missing tests
4. **Review agent PRs carefully** — Agent is helpful but not perfect
5. **Provide feedback** — If agent creates bad fixes, improve the patterns

### For Test Quality

1. **Test behavior, not implementation** — Makes tests more resilient
2. **One assertion per test** — Easier for agent to diagnose failures
3. **Clear test names** — Agent uses names to understand intent
4. **Avoid flaky tests** — Fix root cause instead of increasing retries
5. **Mock external dependencies** — Prevents network-dependent failures

### For Maintainability

1. **Keep workflows simple** — Complex workflows are harder to debug
2. **Document patterns** — Agent learns from existing test patterns
3. **Regular cleanup** — Remove obsolete tests and unused mocks
4. **Monitor agent performance** — Track success rate of auto-fixes
5. **Iterate on patterns** — Update agent logic as project evolves

## 🎓 Examples

### Example 1: Backend Test Failure

**Scenario:** New endpoint added, integration test fails

**Agent Actions:**
1. Detects test failure in backend suite
2. Analyzes error: `Expected status code 200 but got 400`
3. Reviews recent changes to endpoint
4. Identifies validation error
5. Fixes validator configuration
6. Creates PR with fix

**PR Changes:**
```diff
// JoinClubRequestValidator.cs
- .Length(6)
+ .Length(8)  // Match actual invite code length
```

### Example 2: Missing Frontend Test

**Scenario:** New React component created without test

**Agent Actions:**
1. Detects new file: `src/components/NewWidget.tsx`
2. Checks for: `src/components/NewWidget.test.tsx` (not found)
3. Analyzes component structure
4. Generates test following existing patterns
5. Creates PR with new test

**PR Changes:**
```typescript
// NewWidget.test.tsx (new file)
import { render, screen } from '@testing-library/react';
import { NewWidget } from './NewWidget';

describe('NewWidget', () => {
  it('should render with props', () => {
    render(<NewWidget title="Test" />);
    expect(screen.getByText('Test')).toBeInTheDocument();
  });
});
```

### Example 3: Timeout Error

**Scenario:** Frontend test times out on CI

**Agent Actions:**
1. Detects timeout error in test output
2. Identifies async operation without proper waiting
3. Adds `waitFor` with appropriate timeout
4. Creates PR with fix

**PR Changes:**
```diff
- expect(screen.getByText('Loaded')).toBeInTheDocument();
+ await waitFor(() => {
+   expect(screen.getByText('Loaded')).toBeInTheDocument();
+ }, { timeout: 10000 });
```

## 🚦 Integration with Existing CI

The QA agent complements existing CI:

```
Push to main
    ↓
Main CI runs (.github/workflows/ci.yml)
    ↓
Tests run (backend + frontend)
    ↓
    ├─ Tests Pass ✅
    │       ↓
    │   QA Agent checks for missing tests
    │       ↓
    │   Creates PR if tests needed
    │
    └─ Tests Fail ❌
            ↓
        QA Agent analyzes failures
            ↓
        Creates issue with report
            ↓
        Creates PR with fixes
            ↓
        PR goes through same CI checks
```

## 📈 Metrics & Monitoring

Track agent effectiveness:

| Metric | Good Target |
|--------|-------------|
| Auto-fix success rate | > 80% |
| Time to fix | < 10 minutes |
| False positives | < 10% |
| Test coverage maintained | ≥ 80% |

Review metrics periodically:
```bash
# Count QA agent PRs
gh pr list --label "qa-agent" --state all

# Check success rate
gh pr list --label "qa-agent" --state merged
```

## 🔮 Future Enhancements

Planned features:

- [ ] **Coverage regression detection** — Fail if coverage drops
- [ ] **Performance regression** — Detect slow tests
- [ ] **Flaky test detection** — Track intermittent failures
- [ ] **Auto-merge** — Merge PR if all checks pass
- [ ] **Notifications** — Slack/Discord alerts
- [ ] **Learning mode** — Improve patterns from feedback

## 🤝 Contributing

To improve the QA agent:

1. Add patterns to `qa-agent` skill
2. Enhance detection in `qa-agent.sh`
3. Update workflow in `qa-agent.yml`
4. Test changes thoroughly
5. Document new capabilities

## 📚 Additional Resources

- [Testing Skill](/.claude/skills/testing/skill.md) - Test patterns
- [TDD Skill](/.claude/skills/tdd/skill.md) - TDD workflow
- [CI/CD Documentation](/.github/workflows/README.md) - Pipeline details
- [Contributing Guide](/CONTRIBUTING.md) - Contribution guidelines

---

**Need Help?**
- Create an issue with `qa-agent` label
- Check workflow logs in GitHub Actions
- Review test logs: `qa-*-tests.log`
- Run manual analysis: `./scripts/qa-agent.sh HEAD analyze`
