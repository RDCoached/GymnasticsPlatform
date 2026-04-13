---
name: branch-hygiene
description: Git branch management, cleanup automation, and merge strategy guidance
triggers:
  - /branch-hygiene
  - /cleanup-branches
  - /audit-branches
  - "stale branch"
  - "merged but ahead"
  - "clean up branches"
  - "squash merge confusion"
  - "branch management"
skill_type: workflow
---

# Branch Hygiene Skill

Manages Git branch lifecycle, prevents stale branch accumulation, and provides merge strategy guidance.

## When to Use This Skill

Invoke this skill when:
- Branches show as "ahead" even though they're merged
- Repository has accumulated stale branches
- Choosing between squash/rebase/merge strategies
- Setting up branch automation
- Troubleshooting merge history confusion
- Implementing branch protection rules
- Establishing team Git workflow

## Core Capabilities

### 1. Branch Auditing

**Identify stale branches:**
```bash
# Run comprehensive audit
./scripts/audit-branches.sh

# What it checks:
- Branches merged into main (safe to delete)
- Branches not merged (review needed)
- Branch ages and last activity
- Squash-merged branches (ahead but actually merged)
```

**Interpret results:**
- **Merged branches:** Safe to delete immediately
- **Ahead + Behind (large behind count):** Likely squash-merged, verify then delete
- **Not merged + old:** Review if still needed
- **Not merged + recent:** Active development

### 2. Branch Cleanup

**Immediate cleanup:**
```bash
# Delete specific branch
git push origin --delete branch-name
git branch -d branch-name

# Delete all merged branches
git branch -r --merged main |
  grep -v main |
  sed 's/origin\///' |
  xargs -n 1 git push origin --delete
```

**Automated cleanup:**
- GitHub Action: `.github/workflows/cleanup-merged-branches.yml`
- Triggers: On PR merge, weekly schedule, manual
- Prevents accumulation by auto-deleting after merge

**GitHub setting:**
- Settings → General → Pull Requests
- Enable: "Automatically delete head branches"

### 3. Merge Strategy Selection

**Decision Tree:**

```
Is the feature branch well-crafted with meaningful commits?
├─ YES → Use Rebase + Merge
│         ✅ Preserves commit history
│         ✅ Git recognizes as merged
│         ✅ Linear history
│         ⚠️  Requires discipline
│
└─ NO  → Use Squash Merge (DEFAULT)
          ✅ Clean single commit
          ✅ Easy to revert
          ✅ Hides messy dev history
          ⚠️  Branch will show as "ahead" until deleted
```

**Avoid merge commits unless:**
- Preserving exact branch point is critical
- Multiple developers worked simultaneously
- You need clear integration markers in history

### 4. Squash Merge Confusion

**Problem:**
```
feature-branch:           main:
  a3d0911 Add feature     6e8750e PR merge
  d96b4d5 Fix tests         ↑
  699de33 Fix bug           Squashed 3 commits
```

Git sees different SHAs → thinks branch is "ahead" → but changes ARE in main

**Solution:**
1. **Immediate:** Delete branch after PR merge
2. **Automated:** GitHub auto-delete setting
3. **Verification:** Run audit script to confirm

**Detection pattern:**
- Branch shows commits ahead
- Branch is MANY commits behind (10+)
- PR was merged to main
- → Likely squash-merged, safe to delete

### 5. Workflow Setup

**Recommended workflow for .NET projects:**

```bash
# 1. Create feature branch
git checkout -b feature/my-feature

# 2. Develop with focused commits
git commit -m "feat: add validation"
git commit -m "test: add edge cases"

# 3. Keep updated with main
git fetch origin
git rebase origin/main

# 4. Push and create PR
git push -u origin feature/my-feature
gh pr create --title "Add validation" --body "..."

# 5. After GitHub squash-merges...
git checkout main
git pull
git branch -d feature/my-feature  # Local cleanup
# Remote auto-deleted by GitHub ✓
```

**Key settings:**
- Require linear history (forces squash or rebase)
- Auto-delete head branches
- Require PR reviews
- Require status checks before merge

### 6. Branch Protection

**Recommended rules for main:**

```yaml
Branch Protection Rules for 'main':
  ☑ Require pull request reviews (1 approver)
  ☑ Require status checks to pass
  ☑ Require branches to be up to date
  ☑ Require linear history
  ☐ Allow force pushes (NEVER)
  ☐ Allow deletions (NEVER)
```

**Why linear history:**
- Forces squash or rebase (no merge commits)
- Cleaner git log
- Easier to revert
- Bisect works better

## Common Scenarios

### Scenario 1: "Branch shows 20 ahead, 30 behind"

**Diagnosis:** Likely squash-merged

**Action:**
1. Check if PR was merged: `gh pr list --state merged`
2. Verify changes in main: `git diff main origin/branch-name --stat`
3. If no unique changes: Delete branch
4. If unique changes: Create new PR

### Scenario 2: "Dozens of old branches"

**Diagnosis:** No cleanup process

**Action:**
1. Run audit: `./scripts/audit-branches.sh`
2. Delete merged: Use cleanup command from audit output
3. Review unmerged: Check with team
4. Enable GitHub auto-delete
5. Deploy cleanup GitHub Action

### Scenario 3: "Should I squash or rebase?"

**Decision factors:**

**Squash when:**
- Feature has WIP commits, typo fixes, "revert" commits
- You want clean, single-commit history
- Team is less experienced with Git
- Feature is straightforward

**Rebase when:**
- Commits tell a valuable story
- Each commit is production-ready
- You want to preserve attribution
- Debugging might need commit granularity

**Default recommendation:** Squash for 95% of cases

### Scenario 4: "How often to clean up?"

**Cadence:**
- **Automatic:** After every PR merge (GitHub Action)
- **Weekly:** Automated GitHub Action run
- **Monthly:** Manual audit (`./scripts/audit-branches.sh`)
- **Quarterly:** Review branch protection rules

## Files This Skill Manages

```
.github/workflows/cleanup-merged-branches.yml  # Auto-cleanup on PR merge
scripts/audit-branches.sh                       # Manual audit tool
docs/GIT_WORKFLOW.md                           # Comprehensive guide
docs/WORKFLOW_QUICK_REFERENCE.md               # Quick reference
```

## Integration with Other Skills

**Works well with:**
- `/commit` - Ensures branches are cleaned after commit
- `/pr` - Reminds to enable auto-delete
- `/verify` - Checks branch state before merge
- `/checkpoint` - Can trigger branch audit

## Anti-Patterns to Avoid

❌ **Long-lived feature branches (> 2 weeks)**
  → Break into smaller, incremental PRs

❌ **"Phase" branches (phase1, phase2, etc.)**
  → Use feature flags, merge incrementally

❌ **Keeping branches after merge "just in case"**
  → Git history preserves everything, delete confidently

❌ **Manual branch deletion**
  → Enable auto-delete, use GitHub Action

❌ **Merge commits for features**
  → Use squash or rebase for linear history

## Quick Commands Reference

```bash
# Audit all branches
./scripts/audit-branches.sh

# Delete merged branches
git branch -r --merged main | grep -v main | sed 's/origin\///' | xargs -n 1 git push origin --delete

# Check specific branch status
git log --oneline main..origin/branch-name  # Commits ahead
git log --oneline origin/branch-name..main  # Commits behind

# Verify branch was merged
gh pr list --state merged --search "head:branch-name"

# Manual cleanup workflow trigger
gh workflow run cleanup-merged-branches.yml
```

## Success Metrics

After implementing this skill's recommendations:

✅ **Repository has < 3 branches total** (main + 1-2 active features)
✅ **No branches > 1 week old** (except main)
✅ **GitHub auto-delete enabled**
✅ **Cleanup GitHub Action deployed**
✅ **Team uses squash merge consistently**
✅ **Monthly audit shows zero stale branches**

## Skill Invocation Examples

**User:** "I have a bunch of old branches, help me clean up"
**Skill:** Runs audit, identifies merged branches, provides cleanup commands, sets up automation

**User:** "This branch shows as ahead but I already merged it"
**Skill:** Explains squash merge confusion, verifies PR was merged, safely deletes branch

**User:** "Should I squash or rebase this PR?"
**Skill:** Asks about commit quality, provides decision tree, recommends strategy

**User:** "/audit-branches"
**Skill:** Runs audit script, interprets results, suggests actions

## Additional Resources

- **Trunk-Based Development:** https://trunkbaseddevelopment.com/
- **GitHub Flow:** https://docs.github.com/en/get-started/quickstart/github-flow
- **Conventional Commits:** https://www.conventionalcommits.org/

## Maintenance

**Monthly:** Review and update merge strategy guidance based on team feedback
**Quarterly:** Update cleanup scripts for new Git features
**As needed:** Add new scenarios based on team pain points
