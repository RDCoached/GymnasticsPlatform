**Commit:** `${{ env.COMMIT_SHORT_SHA }}` - ${{ env.COMMIT_MESSAGE }}

**Failed:**
${{ env.BACKEND_FAILED == 'true' && '- Backend\n' || '' }}${{ env.USER_PORTAL_FAILED == 'true' && '- User Portal\n' || '' }}${{ env.ADMIN_PORTAL_FAILED == 'true' && '- Admin Portal\n' || '' }}

[Test Logs](https://github.com/${{ github.repository }}/actions/runs/${{ github.run_id }})

---
*QA Agent will create PR with fixes*
