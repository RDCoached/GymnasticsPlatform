# Git Workflow Best Practices

## The Problem We Solved

After merging feature branches via squash merge, the original branches showed as "ahead" of main, creating confusion:
- Squash merges create new commit SHAs, so git doesn't recognize the commits as "merged"
- Feature branches retained old code (like Keycloak configs) that was later cleaned up in main
- Multiple phase branches created a complex merge history

## Recommended Workflow

### 1. Branch Cleanup After Merge

**Immediately after a PR is merged, delete the branch:**

```bash
# GitHub does this automatically if you enable the setting:
# Settings → General → "Automatically delete head branches"

# Manual cleanup if needed:
git push origin --delete feature/my-branch
git branch -d feature/my-branch
```

**Why:** Prevents accumulation of stale branches and confusion about what's merged.

### 2. Choose the Right Merge Strategy

#### Use **Squash Merge** for:
- Feature branches with many small commits
- Work-in-progress commits that don't need to be preserved
- Experimental/iterative development

**Pros:**
- Clean, linear history on main
- Single commit per feature for easy revert
- Hides messy development history

**Cons:**
- Loses individual commit context
- Makes branch comparison confusing (this was our issue)

#### Use **Rebase + Merge** for:
- Branches with well-crafted, meaningful commits
- When commit history tells a story worth preserving
- Collaborative features where attribution matters

**Pros:**
- Preserves individual commits
- Linear history (no merge commits)
- git correctly recognizes commits as merged

**Cons:**
- Requires discipline in commit quality
- More complex for beginners

#### Avoid **Merge Commits** unless:
- You need to preserve the exact branching point
- Multiple people worked on the feature simultaneously
- You want a clear "integration point" in history

### 3. GitHub Settings for Better Hygiene

Enable these in **Settings → General**:
```
☑ Automatically delete head branches
☑ Require linear history (forces rebase or squash)
☐ Allow merge commits (disable this)
```

### 4. Branch Naming Convention

Use prefixes to signal intent and lifespan:

```bash
feature/my-feature      # New functionality, delete after merge
fix/bug-description     # Bug fixes, delete after merge
refactor/what-changed   # Code improvements, delete after merge
chore/task-name         # Maintenance work, delete after merge

# Avoid long-lived feature branches
# Break large features into smaller, mergeable chunks
```

### 5. Alternative: Trunk-Based Development

For faster iteration, consider trunk-based development:

```bash
# Work directly on main with feature flags
# Short-lived branches (< 1 day)
# Continuous integration to main
# Use feature flags to hide incomplete work
```

**Benefits:**
- No branch staleness issues
- Faster feedback cycles
- Encourages small, incremental changes

### 6. Periodic Branch Audit

Run this monthly to find stale branches:

```bash
# List branches merged into main
git branch -r --merged main | grep -v main

# List branches not merged (might be stale)
git branch -r --no-merged main

# Check when a branch was last updated
git for-each-ref --sort=-committerdate refs/remotes/origin --format='%(committerdate:short) %(refname:short)'

# Delete all merged remote branches
git branch -r --merged main | grep -v main | sed 's/origin\///' | xargs -n 1 git push origin --delete
```

### 7. Our Recommended Setup

Based on your project, here's the optimal workflow:

**For this project:**
```bash
1. Create feature branch from main
   git checkout -b feature/my-feature

2. Work in small, focused commits
   git commit -m "feat: add user authentication"

3. Keep branch up-to-date with main
   git fetch origin
   git rebase origin/main

4. Create PR when ready
   gh pr create --title "feat: add user authentication"

5. Use SQUASH MERGE on GitHub
   - Clean history on main
   - Easy to revert if needed

6. GitHub auto-deletes branch ✓

7. Locally delete branch
   git checkout main
   git pull
   git branch -d feature/my-feature
```

**Result:** Clean history, no stale branches, clear intent.

## Example: How Phases Should Have Been Done

Instead of:
```
feature/phase1-auth-provider-abstraction
feature/phase3-entra-id-implementation
feature/phase5-dual-provider-testing
feature/phase6-production-migration
feature/phase7-cleanup-keycloak
```

Better approach:
```
1. feature/auth-provider-abstraction
   → Merge to main, delete branch

2. feature/entra-id-authentication
   → Merge to main, delete branch
   → Use feature flag if not ready for production

3. Continue with small, incremental PRs
   → Each fully tested and mergeable
   → Main always deployable
```

## Quick Reference

| Scenario | Strategy | Auto-delete |
|----------|----------|-------------|
| Small feature (1-5 commits) | Squash merge | ✓ |
| Large feature (well-crafted commits) | Rebase + merge | ✓ |
| Bug fix | Squash merge | ✓ |
| Hotfix to production | Rebase + merge | ✓ |
| Experimental work | Squash merge or close without merging | ✓ |

## GitHub Action for Branch Cleanup

Create `.github/workflows/cleanup-merged-branches.yml`:

```yaml
name: Cleanup Merged Branches

on:
  pull_request:
    types: [closed]
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday

jobs:
  cleanup:
    if: github.event.pull_request.merged == true || github.event_name == 'schedule'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Delete merged branches
        run: |
          git fetch --prune
          git branch -r --merged main |
            grep -v main |
            grep -v HEAD |
            sed 's/origin\///' |
            xargs -r -n 1 git push origin --delete
```

This automatically cleans up merged branches weekly and after PR merges.

## Key Takeaways

1. **Delete branches immediately after merge** - Don't let them accumulate
2. **Enable GitHub auto-delete** - One less thing to remember
3. **Use squash merge for most features** - Clean history, easy revert
4. **Rebase before merging** - Keep main linear and up-to-date
5. **Avoid long-lived branches** - Break work into smaller, mergeable chunks
6. **Audit branches monthly** - Clean up any stragglers
7. **Feature flags > long branches** - Hide incomplete work, ship frequently

Following these practices will prevent the "ahead but already merged" confusion and keep your repository clean.
