#!/usr/bin/env bash
set -euo pipefail

# Branch Audit Script
# Helps identify stale branches that may need cleanup

echo "=== Git Branch Audit ==="
echo ""

# Fetch latest from remote
echo "📡 Fetching latest from remote..."
git fetch --all --prune
echo ""

# Show branches merged into main (candidates for deletion)
echo "✅ Branches merged into main (safe to delete):"
git branch -r --merged main | grep -v "main" | grep -v "HEAD" | sed 's/origin\///' || echo "  None found"
echo ""

# Show branches not merged (might be stale or active)
echo "⚠️  Branches NOT merged into main (review needed):"
git branch -r --no-merged main | grep -v "HEAD" | sed 's/origin\///' || echo "  None found"
echo ""

# Show branch ages
echo "📅 Branch last commit dates:"
git for-each-ref \
  --sort=-committerdate \
  --format='%(committerdate:short) %(refname:short) - %(authorname)' \
  refs/remotes/origin | \
  grep -v "HEAD" | \
  head -20
echo ""

# Check for squash-merged branches (commits in branch but not in main)
echo "🔍 Checking for squash-merged branches..."
echo "(These show as 'not merged' but their changes are in main)"
echo ""

for branch in $(git branch -r --no-merged main | grep -v "HEAD" | sed 's/origin\///'); do
  # Get commits in branch but not in main
  ahead=$(git rev-list --count main..origin/"$branch" 2>/dev/null || echo "0")
  behind=$(git rev-list --count origin/"$branch"..main 2>/dev/null || echo "0")

  if [ "$ahead" -gt 0 ] && [ "$behind" -gt 10 ]; then
    echo "  ⚡ $branch: $ahead ahead, $behind behind"
    echo "     └─ Likely squash-merged, check if safe to delete"
  fi
done
echo ""

# Summary
merged_count=$(git branch -r --merged main | grep -v "main" | grep -v "HEAD" | wc -l)
not_merged_count=$(git branch -r --no-merged main | grep -v "HEAD" | wc -l)

echo "=== Summary ==="
echo "  Merged branches: $merged_count"
echo "  Not merged: $not_merged_count"
echo ""

# Offer cleanup
if [ "$merged_count" -gt 0 ]; then
  echo "💡 To delete all merged branches, run:"
  echo "   git branch -r --merged main | grep -v main | sed 's/origin\///' | xargs -n 1 git push origin --delete"
  echo ""
fi

echo "✨ Audit complete!"
