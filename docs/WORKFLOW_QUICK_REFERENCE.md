# Workflow Quick Reference

## ✅ What We Did

**Deleted 9 stale branches:**
- ✓ feature-entra-auth
- ✓ feature-skills-database
- ✓ feature/phase1-auth-provider-abstraction
- ✓ feature/phase3-entra-id-implementation
- ✓ feature/phase5-dual-provider-testing
- ✓ feature/phase6-production-migration
- ✓ feature/phase7-cleanup-keycloak
- ✓ feature/traefik
- ✓ fix/dashboard-test-api-mocking

**Result:** Clean repository with only `main` branch remaining

---

## 🚀 Recommended Workflow Going Forward

### The Simple Version

```bash
# 1. Create feature branch
git checkout -b feature/my-feature

# 2. Make commits
git add .
git commit -m "feat: add awesome feature"

# 3. Keep up-to-date with main
git fetch origin
git rebase origin/main

# 4. Push and create PR
git push -u origin feature/my-feature
gh pr create --title "Add awesome feature"

# 5. After PR is merged on GitHub...
git checkout main
git pull
git branch -d feature/my-feature  # Delete local branch
# Remote branch auto-deleted by GitHub ✓
```

### Enable Auto-Delete on GitHub

**Settings → General → Pull Requests:**
- ☑ **Automatically delete head branches**

This prevents stale branches from accumulating.

---

## 🔧 Merge Strategies

### Use Squash Merge (Default)
- ✅ Clean, linear history
- ✅ One commit per feature
- ✅ Easy to revert
- ⚠️ Loses individual commit details

**When to use:** 99% of features and fixes

### Use Rebase + Merge
- ✅ Preserves commit history
- ✅ Linear history (no merge commits)
- ⚠️ Requires well-crafted commits

**When to use:** When commits tell a valuable story

---

## 🧹 Branch Maintenance

### Monthly Audit
```bash
./scripts/audit-branches.sh
```

This shows:
- Merged branches (safe to delete)
- Unmerged branches (review needed)
- Branch ages
- Squash-merge detection

### Manual Cleanup
```bash
# Delete all remote branches merged into main
git branch -r --merged main |
  grep -v main |
  sed 's/origin\///' |
  xargs -n 1 git push origin --delete
```

### Automated Cleanup
GitHub Action runs weekly: `.github/workflows/cleanup-merged-branches.yml`
- Auto-deletes merged branches
- Runs every Sunday
- Can trigger manually

---

## 🎯 Best Practices

### ✅ DO
- Delete branches immediately after merge
- Keep branches short-lived (< 1 week)
- Rebase before creating PR
- Use descriptive branch names
- Break large features into smaller PRs

### ❌ DON'T
- Keep branches alive after merging
- Create long-lived feature branches
- Use merge commits (creates messy history)
- Create "phase" branches (integrate incrementally instead)
- Wait for "the perfect time" to merge

---

## 📊 Why Squash Merge Caused Confusion

**What happened:**
```
feature-entra-auth:     main:
  a3d0911 OAuth blog    6e8750e PR#17
  d96b4d5 Fix tests       ↑
  699de33 Fix IDs         Squashed 20 commits
  ... 17 more ...         into one
```

**Result:**
- Git sees different commit SHAs
- Thinks branch is "ahead" with 20 commits
- But the changes ARE in main (just squashed)

**Solution:**
- Delete branch after merge
- GitHub Action prevents accumulation

---

## 🔍 Quick Health Check

### Check Branch Status
```bash
# List all branches
git branch -a

# Check what's merged
git branch -r --merged main

# See branch ages
git for-each-ref --sort=-committerdate refs/remotes
```

### Verify Clean State
```bash
git fetch --prune
./scripts/audit-branches.sh
```

Should show: "None found" for merged branches

---

## 💡 Pro Tips

1. **Use GitHub CLI for faster workflow:**
   ```bash
   gh pr create --fill  # Auto-fill from commits
   gh pr merge --squash --delete-branch
   ```

2. **Set up branch protection on main:**
   - Require PR reviews
   - Require status checks
   - No direct pushes to main

3. **Use conventional commits:**
   - `feat:` - New feature
   - `fix:` - Bug fix
   - `refactor:` - Code improvement
   - `chore:` - Maintenance
   - `docs:` - Documentation

4. **Keep PRs small:**
   - < 400 lines changed
   - Single concern
   - Fully tested
   - Easy to review

---

## 📚 Full Documentation

- **Detailed workflow:** `docs/GIT_WORKFLOW.md`
- **Branch audit script:** `scripts/audit-branches.sh`
- **Auto-cleanup workflow:** `.github/workflows/cleanup-merged-branches.yml`

---

## ✨ Current State

**Branches:** `main` only (clean ✓)
**Auto-delete:** Enabled on GitHub ✓
**Audit script:** Available ✓
**GitHub Action:** Ready to use ✓

**Next PR:** Will auto-delete its branch after merge 🎉
