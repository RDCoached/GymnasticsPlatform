#!/usr/bin/env bash
set -euo pipefail

# Autonomous QA Agent
# Polls repository for changes, runs tests, fixes failures automatically, and creates PRs

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STATE_FILE="$PROJECT_ROOT/.qa-agent-state"
LOG_FILE="$PROJECT_ROOT/.qa-agent.log"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

error() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] ERROR: $*" | tee -a "$LOG_FILE" >&2
}

# Initialize state file if it doesn't exist
init_state() {
    if [[ ! -f "$STATE_FILE" ]]; then
        log "Initializing state file"
        git rev-parse HEAD > "$STATE_FILE"
    fi
}

# Get last checked commit
get_last_commit() {
    if [[ -f "$STATE_FILE" ]]; then
        cat "$STATE_FILE"
    else
        echo ""
    fi
}

# Update last checked commit
update_last_commit() {
    local commit=$1
    echo "$commit" > "$STATE_FILE"
    log "Updated last checked commit to $commit"
}

# Check for new commits
check_for_changes() {
    local last_commit=$(get_last_commit)
    local current_commit=$(git rev-parse HEAD)

    if [[ "$last_commit" == "$current_commit" ]]; then
        log "No new commits since last check ($current_commit)"
        return 1
    fi

    log "New commits detected: $last_commit -> $current_commit"
    return 0
}

# Fetch latest changes
fetch_changes() {
    log "Fetching latest changes from origin"
    cd "$PROJECT_ROOT"
    git fetch origin main
    git merge --ff-only origin/main || {
        error "Failed to fast-forward merge. Manual intervention required."
        return 1
    }
    log "Successfully updated to latest main"
}

# Run tests and capture results
run_tests() {
    log "Running all tests..."
    cd "$PROJECT_ROOT"

    local backend_failed=false
    local frontend_user_failed=false
    local frontend_admin_failed=false

    # Backend tests
    log "Running backend tests..."
    if dotnet test --no-restore --verbosity normal > backend-test.log 2>&1; then
        log "✅ Backend tests passed"
    else
        log "❌ Backend tests failed"
        backend_failed=true
    fi

    # User portal tests
    log "Running user portal tests..."
    cd "$PROJECT_ROOT/frontend/user-portal"
    if npm test 2>&1 | tee user-portal-test.log; then
        log "✅ User portal tests passed"
    else
        log "❌ User portal tests failed"
        frontend_user_failed=true
    fi

    # Admin portal tests
    log "Running admin portal tests..."
    cd "$PROJECT_ROOT/frontend/admin-portal"
    if npm test 2>&1 | tee admin-portal-test.log; then
        log "✅ Admin portal tests passed"
    else
        log "❌ Admin portal tests failed"
        frontend_admin_failed=true
    fi

    cd "$PROJECT_ROOT"

    # Return failure if any tests failed
    if [[ "$backend_failed" == "true" ]] || [[ "$frontend_user_failed" == "true" ]] || [[ "$frontend_admin_failed" == "true" ]]; then
        return 1
    fi

    return 0
}

# Analyze failures and generate fixes using Claude Code
analyze_and_fix() {
    log "Invoking Claude Code to analyze and fix test failures..."
    cd "$PROJECT_ROOT"

    # Create a prompt file for Claude Code
    local prompt_file=$(mktemp)
    cat > "$prompt_file" <<'EOF'
You are the autonomous QA agent. Tests have failed in this repository.

## Your Task

1. Read the test output logs to identify which tests failed
2. Analyze the root cause of each failure
3. Fix the failing tests following TDD principles
4. Verify your fixes by running the tests again
5. Do NOT commit - just prepare the fixes

## Available Test Logs

- backend-test.log (if backend tests failed)
- frontend/user-portal/user-portal-test.log (if frontend tests failed)
- frontend/admin-portal/admin-portal-test.log (if frontend tests failed)

## Instructions

- Use Read tool to examine test logs
- Identify the specific failing tests
- Determine why they're failing (missing mocks, wrong assertions, etc.)
- Fix the test files
- Run tests again to verify fixes work
- When all tests pass, output "FIXES_COMPLETE" so the script knows to proceed

Start by reading the test logs and analyzing the failures.
EOF

    log "Calling Claude Code agent..."

    # Invoke Claude Code in non-interactive mode
    # Note: This requires claude-code CLI to be available
    if ! command -v claude &> /dev/null; then
        error "Claude Code CLI not found. Please install it first."
        rm "$prompt_file"
        return 1
    fi

    # Run Claude Code autonomously with permissions bypassed
    # Remove --print to allow tool execution (Edit, Read, etc.)
    # Use --dangerously-skip-permissions to bypass all prompts
    if echo "" | claude --dangerously-skip-permissions "$(cat "$prompt_file")" > claude-output.log 2>&1; then
        log "Claude Code agent completed successfully"
        rm "$prompt_file"
        return 0
    else
        error "Claude Code agent failed. Check claude-output.log for details."
        rm "$prompt_file"
        return 1
    fi
}

# Verify fixes by running tests again
verify_fixes() {
    log "Verifying fixes by running tests again..."

    if run_tests; then
        log "✅ All tests now pass!"
        return 0
    else
        error "❌ Tests still failing after fixes. Manual intervention needed."
        return 1
    fi
}

# Create PR with fixes
create_pr() {
    log "Creating PR with test fixes..."
    cd "$PROJECT_ROOT"

    local current_commit=$(git rev-parse --short HEAD)
    local branch_name="qa/fix-tests-${current_commit}"

    # Check if there are any changes to commit
    if [[ -z $(git status --porcelain) ]]; then
        log "No changes to commit. Skipping PR creation."
        return 0
    fi

    # Create new branch
    log "Creating branch: $branch_name"
    git checkout -b "$branch_name"

    # Stage all changes
    git add -A

    # Create commit
    local commit_message=$(cat <<EOF
fix: QA agent auto-fixes for failing tests

🤖 Autonomous QA Agent detected and fixed test failures.

## Changes
- Fixed failing tests identified in commit $current_commit
- Updated test mocks, assertions, and setup as needed
- All tests now passing

## Verification
✅ Backend tests passing
✅ User portal tests passing
✅ Admin portal tests passing

Generated automatically by QA autonomous agent.
EOF
)

    git commit -m "$commit_message"

    # Push branch
    log "Pushing branch to origin..."
    git push origin "$branch_name"

    # Create PR using gh CLI
    log "Creating pull request..."
    local pr_body="## QA Autonomous Agent

This PR contains automated test fixes generated by the QA agent.

### What Changed

The agent detected test failures, analyzed the root causes, and generated fixes following TDD principles.

### Verification

All tests are now passing:
- Backend (.NET/xUnit)
- User Portal (Vitest)
- Admin Portal (Vitest)

### Review Notes

Please review the fixes to ensure they:
1. Actually fix the root cause (not just symptoms)
2. Follow existing test patterns
3. Don't introduce new issues

### Auto-generated

This PR was created automatically by scripts/qa-agent-autonomous.sh"

    gh pr create \
        --title "QA Agent: Auto-fix failing tests in $current_commit" \
        --body "$pr_body" \
        --label "qa-agent,automated,test-fixes" \
        --base main \
        --head "$branch_name"

    log "✅ PR created successfully!"

    # Switch back to main
    git checkout main
}

# Main execution flow
main() {
    log "=== QA Autonomous Agent Starting ==="

    # Initialize
    init_state

    # Fetch latest changes
    if ! fetch_changes; then
        error "Failed to fetch changes. Exiting."
        exit 1
    fi

    # Check for new commits
    if ! check_for_changes; then
        log "No changes detected. Exiting."
        exit 0
    fi

    # Run tests
    if run_tests; then
        log "✅ All tests passed! No action needed."
        update_last_commit "$(git rev-parse HEAD)"
        exit 0
    fi

    log "Tests failed. Proceeding with analysis and fixes..."

    # Analyze and fix
    if ! analyze_and_fix; then
        error "Failed to analyze and fix tests. Manual intervention required."
        exit 1
    fi

    # Verify fixes
    if ! verify_fixes; then
        error "Fixes didn't resolve all failures. Manual intervention required."
        exit 1
    fi

    # Create PR
    if ! create_pr; then
        error "Failed to create PR. Manual intervention required."
        exit 1
    fi

    # Update state
    update_last_commit "$(git rev-parse HEAD)"

    log "=== QA Autonomous Agent Complete ==="
    log "PR created with test fixes. Check GitHub for details."
}

# Run main function
main "$@"
