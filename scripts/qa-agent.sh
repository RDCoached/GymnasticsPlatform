#!/bin/bash

# QA Autonomous Agent Script
# Analyzes code changes, runs tests, fixes failures, and generates new tests

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
COMMIT_SHA="${1:-HEAD}"
MODE="${2:-analyze}" # analyze, fix, or full

echo -e "${BLUE}🤖 QA Autonomous Agent${NC}"
echo -e "${BLUE}================================${NC}"
echo ""

# Function to analyze commit changes
analyze_commit() {
    echo -e "${YELLOW}📊 Analyzing commit $COMMIT_SHA${NC}"

    # Get changed files
    if [ "$COMMIT_SHA" == "HEAD" ]; then
        CHANGED_FILES=$(git diff --name-only HEAD~1 HEAD)
    else
        CHANGED_FILES=$(git diff --name-only "$COMMIT_SHA~1" "$COMMIT_SHA")
    fi

    echo "Changed files:"
    echo "$CHANGED_FILES"
    echo ""

    # Categorize changes
    BACKEND_CHANGED=false
    FRONTEND_USER_CHANGED=false
    FRONTEND_ADMIN_CHANGED=false
    NEW_FEATURES=()

    while IFS= read -r file; do
        if [[ $file == src/* ]] || [[ $file == tests/* ]]; then
            BACKEND_CHANGED=true

            # Check if it's a new feature (new file, not a test)
            if [[ $file != tests/* ]] && git diff --name-status "$COMMIT_SHA~1" "$COMMIT_SHA" | grep -q "^A.*$file"; then
                NEW_FEATURES+=("$file")
            fi
        fi

        if [[ $file == frontend/user-portal/src/* ]] && [[ $file != *.test.* ]]; then
            FRONTEND_USER_CHANGED=true

            if git diff --name-status "$COMMIT_SHA~1" "$COMMIT_SHA" | grep -q "^A.*$file"; then
                NEW_FEATURES+=("$file")
            fi
        fi

        if [[ $file == frontend/admin-portal/src/* ]] && [[ $file != *.test.* ]]; then
            FRONTEND_ADMIN_CHANGED=true

            if git diff --name-status "$COMMIT_SHA~1" "$COMMIT_SHA" | grep -q "^A.*$file"; then
                NEW_FEATURES+=("$file")
            fi
        fi
    done <<< "$CHANGED_FILES"

    export BACKEND_CHANGED
    export FRONTEND_USER_CHANGED
    export FRONTEND_ADMIN_CHANGED
    export NEW_FEATURES

    echo -e "${GREEN}✓ Analysis complete${NC}"
    echo "  Backend changed: $BACKEND_CHANGED"
    echo "  User portal changed: $FRONTEND_USER_CHANGED"
    echo "  Admin portal changed: $FRONTEND_ADMIN_CHANGED"
    echo "  New features: ${#NEW_FEATURES[@]}"
    echo ""
}

# Function to detect test patterns from existing tests
detect_test_patterns() {
    echo -e "${YELLOW}🔍 Detecting test patterns from codebase${NC}"

    PATTERNS_FILE="$PROJECT_ROOT/qa-test-patterns.md"
    echo "# Detected Test Patterns" > "$PATTERNS_FILE"
    echo "" >> "$PATTERNS_FILE"
    echo "This file contains patterns detected from existing tests in the codebase." >> "$PATTERNS_FILE"
    echo "Use these patterns when generating new tests." >> "$PATTERNS_FILE"
    echo "" >> "$PATTERNS_FILE"

    # Backend test patterns
    if [ "$BACKEND_CHANGED" == "true" ] || [ ${#NEW_FEATURES[@]} -gt 0 ]; then
        echo "## Backend Tests (.NET)" >> "$PATTERNS_FILE"
        echo "" >> "$PATTERNS_FILE"

        # Find example integration test
        EXAMPLE_TEST=$(find "$PROJECT_ROOT/tests" -name "*Tests.cs" -type f | head -n 1)
        if [ -n "$EXAMPLE_TEST" ]; then
            echo "**Framework:** xUnit v3 + WebApplicationFactory + Testcontainers" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"
            echo "**Example test file:** \`${EXAMPLE_TEST#$PROJECT_ROOT/}\`" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"

            # Extract key patterns
            if grep -q "IClassFixture" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses \`IClassFixture<ApiFixture>\` for shared setup" >> "$PATTERNS_FILE"
            fi
            if grep -q "HttpClient" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Integration tests via HTTP client" >> "$PATTERNS_FILE"
            fi
            if grep -q "TestAuthenticationHandler" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses TestAuthenticationHandler with X-Test headers for auth" >> "$PATTERNS_FILE"
            fi
            echo "" >> "$PATTERNS_FILE"

            # Show example test structure
            echo "\`\`\`csharp" >> "$PATTERNS_FILE"
            head -n 30 "$EXAMPLE_TEST" | grep -A 20 "\[Fact\]" | head -n 25 >> "$PATTERNS_FILE"
            echo "\`\`\`" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"
        fi
    fi

    # Frontend test patterns (User Portal)
    if [ "$FRONTEND_USER_CHANGED" == "true" ] || [[ " ${NEW_FEATURES[@]} " =~ "frontend/user-portal" ]]; then
        echo "## Frontend Tests (User Portal)" >> "$PATTERNS_FILE"
        echo "" >> "$PATTERNS_FILE"

        EXAMPLE_TEST=$(find "$PROJECT_ROOT/frontend/user-portal/src" -name "*.test.tsx" -type f | head -n 1)
        if [ -n "$EXAMPLE_TEST" ]; then
            echo "**Framework:** Vitest + React Testing Library" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"
            echo "**Example test file:** \`${EXAMPLE_TEST#$PROJECT_ROOT/}\`" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"

            # Extract patterns
            if grep -q "vi.mock" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses Vitest mocks (\`vi.mock\`, \`vi.mocked\`)" >> "$PATTERNS_FILE"
            fi
            if grep -q "render(" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses React Testing Library's \`render\`" >> "$PATTERNS_FILE"
            fi
            if grep -q "waitFor(" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses \`waitFor\` for async operations" >> "$PATTERNS_FILE"
            fi
            if grep -q "@react-keycloak/web" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Mocks Keycloak for authentication" >> "$PATTERNS_FILE"
            fi
            echo "" >> "$PATTERNS_FILE"

            # Show example
            echo "\`\`\`typescript" >> "$PATTERNS_FILE"
            head -n 50 "$EXAMPLE_TEST" | grep -B 5 -A 15 "describe(" | head -n 25 >> "$PATTERNS_FILE"
            echo "\`\`\`" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"

            # Check for test factories
            if [ -f "$PROJECT_ROOT/frontend/user-portal/src/test/factories.ts" ]; then
                echo "**Factory Pattern:** Test data factories exist at \`src/test/factories.ts\`" >> "$PATTERNS_FILE"
                echo "" >> "$PATTERNS_FILE"
            fi
        fi
    fi

    # Frontend test patterns (Admin Portal)
    if [ "$FRONTEND_ADMIN_CHANGED" == "true" ] || [[ " ${NEW_FEATURES[@]} " =~ "frontend/admin-portal" ]]; then
        echo "## Frontend Tests (Admin Portal)" >> "$PATTERNS_FILE"
        echo "" >> "$PATTERNS_FILE"

        EXAMPLE_TEST=$(find "$PROJECT_ROOT/frontend/admin-portal/src" -name "*.test.tsx" -type f | head -n 1)
        if [ -n "$EXAMPLE_TEST" ]; then
            echo "**Framework:** Vitest + React Testing Library" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"
            echo "**Example test file:** \`${EXAMPLE_TEST#$PROJECT_ROOT/}\`" >> "$PATTERNS_FILE"
            echo "" >> "$PATTERNS_FILE"

            # Same pattern detection as user portal
            if grep -q "vi.mock" "$EXAMPLE_TEST"; then
                echo "**Pattern:** Uses Vitest mocks (\`vi.mock\`, \`vi.mocked\`)" >> "$PATTERNS_FILE"
            fi
            echo "" >> "$PATTERNS_FILE"
        fi
    fi

    export PATTERNS_FILE

    echo -e "${GREEN}✓ Test patterns detected: $PATTERNS_FILE${NC}"
    echo ""
}

# Function to run tests
run_tests() {
    echo -e "${YELLOW}🧪 Running tests${NC}"

    BACKEND_TESTS_FAILED=false
    USER_PORTAL_TESTS_FAILED=false
    ADMIN_PORTAL_TESTS_FAILED=false

    # Backend tests
    if [ "$BACKEND_CHANGED" == "true" ]; then
        echo "Running backend tests..."
        cd "$PROJECT_ROOT"
        if ! dotnet test --no-restore --verbosity normal 2>&1 | tee "$PROJECT_ROOT/qa-backend-tests.log"; then
            BACKEND_TESTS_FAILED=true
            echo -e "${RED}✗ Backend tests failed${NC}"
        else
            echo -e "${GREEN}✓ Backend tests passed${NC}"
        fi
        echo ""
    fi

    # User portal tests
    if [ "$FRONTEND_USER_CHANGED" == "true" ]; then
        echo "Running user portal tests..."
        cd "$PROJECT_ROOT/frontend/user-portal"
        if ! npm run test:ci 2>&1 | tee "$PROJECT_ROOT/qa-user-portal-tests.log"; then
            USER_PORTAL_TESTS_FAILED=true
            echo -e "${RED}✗ User portal tests failed${NC}"
        else
            echo -e "${GREEN}✓ User portal tests passed${NC}"
        fi
        echo ""
    fi

    # Admin portal tests
    if [ "$FRONTEND_ADMIN_CHANGED" == "true" ]; then
        echo "Running admin portal tests..."
        cd "$PROJECT_ROOT/frontend/admin-portal"
        if ! npm run test:ci 2>&1 | tee "$PROJECT_ROOT/qa-admin-portal-tests.log"; then
            ADMIN_PORTAL_TESTS_FAILED=true
            echo -e "${RED}✗ Admin portal tests failed${NC}"
        else
            echo -e "${GREEN}✓ Admin portal tests passed${NC}"
        fi
        echo ""
    fi

    export BACKEND_TESTS_FAILED
    export USER_PORTAL_TESTS_FAILED
    export ADMIN_PORTAL_TESTS_FAILED

    cd "$PROJECT_ROOT"
}

# Function to check for missing tests
check_missing_tests() {
    echo -e "${YELLOW}🔍 Checking for missing tests${NC}"

    MISSING_TESTS=()

    for file in "${NEW_FEATURES[@]}"; do
        # Determine expected test file location
        TEST_FILE=""

        if [[ $file == src/* ]]; then
            # Backend file - check for corresponding test in tests/
            BASE_NAME=$(basename "$file" .cs)
            TEST_FILE="tests/${BASE_NAME}Tests.cs"
        elif [[ $file == frontend/user-portal/src/* ]]; then
            # User portal file - check for .test.tsx
            TEST_FILE="${file%.tsx}.test.tsx"
        elif [[ $file == frontend/admin-portal/src/* ]]; then
            # Admin portal file - check for .test.tsx
            TEST_FILE="${file%.tsx}.test.tsx"
        fi

        if [ -n "$TEST_FILE" ] && [ ! -f "$PROJECT_ROOT/$TEST_FILE" ]; then
            MISSING_TESTS+=("$file|$TEST_FILE")
            echo -e "  ${YELLOW}⚠${NC}  Missing test for: $file"
            echo "      Expected: $TEST_FILE"
        fi
    done

    export MISSING_TESTS

    if [ ${#MISSING_TESTS[@]} -eq 0 ]; then
        echo -e "${GREEN}✓ All new features have tests${NC}"
    else
        echo -e "${YELLOW}⚠ ${#MISSING_TESTS[@]} features missing tests${NC}"
    fi
    echo ""
}

# Function to create analysis report
create_report() {
    REPORT_FILE="$PROJECT_ROOT/qa-agent-report.md"

    echo "# QA Agent Analysis Report" > "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"
    echo "**Commit:** \`$(git rev-parse --short $COMMIT_SHA)\`" >> "$REPORT_FILE"
    echo "**Date:** $(date)" >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"

    echo "## Test Results" >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"
    echo "| Component | Status |" >> "$REPORT_FILE"
    echo "|-----------|--------|" >> "$REPORT_FILE"

    if [ "$BACKEND_CHANGED" == "true" ]; then
        if [ "$BACKEND_TESTS_FAILED" == "true" ]; then
            echo "| Backend | ❌ Failed |" >> "$REPORT_FILE"
        else
            echo "| Backend | ✅ Passed |" >> "$REPORT_FILE"
        fi
    fi

    if [ "$FRONTEND_USER_CHANGED" == "true" ]; then
        if [ "$USER_PORTAL_TESTS_FAILED" == "true" ]; then
            echo "| User Portal | ❌ Failed |" >> "$REPORT_FILE"
        else
            echo "| User Portal | ✅ Passed |" >> "$REPORT_FILE"
        fi
    fi

    if [ "$FRONTEND_ADMIN_CHANGED" == "true" ]; then
        if [ "$ADMIN_PORTAL_TESTS_FAILED" == "true" ]; then
            echo "| Admin Portal | ❌ Failed |" >> "$REPORT_FILE"
        else
            echo "| Admin Portal | ✅ Passed |" >> "$REPORT_FILE"
        fi
    fi

    echo "" >> "$REPORT_FILE"
    echo "## Missing Tests" >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"

    if [ ${#MISSING_TESTS[@]} -eq 0 ]; then
        echo "✅ All new features have tests" >> "$REPORT_FILE"
    else
        echo "The following features are missing tests:" >> "$REPORT_FILE"
        echo "" >> "$REPORT_FILE"
        for item in "${MISSING_TESTS[@]}"; do
            IFS='|' read -r file test_file <<< "$item"
            echo "- **$file**" >> "$REPORT_FILE"
            echo "  - Expected test: \`$test_file\`" >> "$REPORT_FILE"
        done
    fi

    echo "" >> "$REPORT_FILE"
    echo "## Recommendations" >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"

    if [ "$BACKEND_TESTS_FAILED" == "true" ] || [ "$USER_PORTAL_TESTS_FAILED" == "true" ] || [ "$ADMIN_PORTAL_TESTS_FAILED" == "true" ]; then
        echo "1. Fix failing tests (see test logs for details)" >> "$REPORT_FILE"
    fi

    if [ ${#MISSING_TESTS[@]} -gt 0 ]; then
        echo "2. Create tests for new features" >> "$REPORT_FILE"
    fi

    if [ "$BACKEND_TESTS_FAILED" == "false" ] && [ "$USER_PORTAL_TESTS_FAILED" == "false" ] && [ "$ADMIN_PORTAL_TESTS_FAILED" == "false" ] && [ ${#MISSING_TESTS[@]} -eq 0 ]; then
        echo "✅ No issues found - code quality is good!" >> "$REPORT_FILE"
    fi

    echo "" >> "$REPORT_FILE"
    echo "---" >> "$REPORT_FILE"
    echo "*Report generated by QA Autonomous Agent*" >> "$REPORT_FILE"

    echo -e "${GREEN}✓ Report created: $REPORT_FILE${NC}"
    echo ""
}

# Function to invoke Claude Code for fixes
invoke_claude_for_fixes() {
    echo -e "${YELLOW}🤖 Invoking Claude Code to fix issues${NC}"

    # Create a prompt for Claude Code
    PROMPT_FILE="$PROJECT_ROOT/qa-agent-prompt.txt"

    echo "You are a QA autonomous agent. Analyze the following test failures and create fixes:" > "$PROMPT_FILE"
    echo "" >> "$PROMPT_FILE"

    # Include detected patterns
    if [ -f "$PATTERNS_FILE" ]; then
        echo "## Test Patterns to Follow" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        cat "$PATTERNS_FILE" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        echo "---" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
    fi

    if [ "$BACKEND_TESTS_FAILED" == "true" ]; then
        echo "## Backend Test Failures" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        tail -n 100 "$PROJECT_ROOT/qa-backend-tests.log" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
    fi

    if [ "$USER_PORTAL_TESTS_FAILED" == "true" ]; then
        echo "## User Portal Test Failures" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        tail -n 100 "$PROJECT_ROOT/qa-user-portal-tests.log" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
    fi

    if [ "$ADMIN_PORTAL_TESTS_FAILED" == "true" ]; then
        echo "## Admin Portal Test Failures" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        tail -n 100 "$PROJECT_ROOT/qa-admin-portal-tests.log" >> "$PROMPT_FILE"
        echo "\`\`\`" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
    fi

    if [ ${#MISSING_TESTS[@]} -gt 0 ]; then
        echo "## Missing Tests" >> "$PROMPT_FILE"
        echo "" >> "$PROMPT_FILE"
        echo "Create tests for the following files:" >> "$PROMPT_FILE"
        for item in "${MISSING_TESTS[@]}"; do
            IFS='|' read -r file test_file <<< "$item"
            echo "- $file (expected test: $test_file)" >> "$PROMPT_FILE"
        done
        echo "" >> "$PROMPT_FILE"
    fi

    echo "Please:" >> "$PROMPT_FILE"
    echo "1. Fix all failing tests" >> "$PROMPT_FILE"
    echo "2. Create missing tests following existing patterns" >> "$PROMPT_FILE"
    echo "3. Create a git branch called 'qa-agent/fix-$(git rev-parse --short $COMMIT_SHA)'" >> "$PROMPT_FILE"
    echo "4. Commit the changes" >> "$PROMPT_FILE"
    echo "5. Push the branch and create a PR" >> "$PROMPT_FILE"

    echo -e "${BLUE}Prompt saved to: $PROMPT_FILE${NC}"
    echo -e "${BLUE}To manually run Claude Code with this prompt:${NC}"
    echo -e "${BLUE}  claude chat --project='$PROJECT_ROOT' < $PROMPT_FILE${NC}"
    echo ""
}

# Main execution
main() {
    cd "$PROJECT_ROOT"

    case "$MODE" in
        analyze)
            analyze_commit
            detect_test_patterns
            run_tests
            check_missing_tests
            create_report
            cat "$PROJECT_ROOT/qa-agent-report.md"
            ;;
        fix)
            analyze_commit
            detect_test_patterns
            run_tests
            check_missing_tests
            invoke_claude_for_fixes
            ;;
        full)
            analyze_commit
            detect_test_patterns
            run_tests
            check_missing_tests
            create_report
            invoke_claude_for_fixes
            ;;
        *)
            echo "Invalid mode: $MODE"
            echo "Usage: $0 [commit_sha] [analyze|fix|full]"
            exit 1
            ;;
    esac

    # Exit with error if tests failed
    if [ "$BACKEND_TESTS_FAILED" == "true" ] || [ "$USER_PORTAL_TESTS_FAILED" == "true" ] || [ "$ADMIN_PORTAL_TESTS_FAILED" == "true" ]; then
        exit 1
    fi
}

main
