## 🤖 QA Agent Analysis Report

**Commit:** `${{ env.COMMIT_SHORT_SHA }}` by ${{ env.COMMIT_AUTHOR }}
**Message:** ${{ env.COMMIT_MESSAGE }}
**Workflow Run:** [View Details](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }})

---

## 📊 Test Results Summary

| Component | Status |
|-----------|--------|
| Backend Tests | ${{ env.BACKEND_FAILED == 'true' && '❌ Failed' || '✅ Passed' }} |
| User Portal Tests | ${{ env.USER_PORTAL_FAILED == 'true' && '❌ Failed' || '✅ Passed' }} |
| Admin Portal Tests | ${{ env.ADMIN_PORTAL_FAILED == 'true' && '❌ Failed' || '✅ Passed' }} |

---

## 🔍 Analysis

The QA agent has detected test failures after commit `${{ env.COMMIT_SHORT_SHA }}`.

### Next Steps

1. **Automated Fix PR**: The QA agent will create a pull request with proposed fixes
2. **Manual Review**: Review the PR and test logs before merging
3. **Root Cause**: Check the workflow artifacts for detailed test output

### Test Logs

Download the test results artifact from the [workflow run](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }}) for detailed failure information.

---

## 🤖 QA Agent Actions

- [ ] Analyze test failures
- [ ] Generate fix PR
- [ ] Create new tests for uncovered code
- [ ] Update test coverage report

---

*This issue was automatically created by the QA Autonomous Agent*
