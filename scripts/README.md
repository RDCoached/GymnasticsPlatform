# Scripts Directory

Automation scripts for the GymnasticsPlatform project.

## QA Autonomous Agent

The QA agent automatically monitors code quality, runs tests, fixes failures, and generates new tests.

### Quick Start

```bash
# Analyze current changes
./scripts/qa-agent.sh HEAD analyze

# Generate fixes
./scripts/qa-agent.sh HEAD fix

# Full analysis + fixes (used in CI)
./scripts/qa-agent.sh HEAD full

# Interactive fix with Claude Code
./scripts/qa-agent-claude.sh
```

### Available Scripts

| Script | Purpose | Usage |
|--------|---------|-------|
| `qa-agent.sh` | Main QA automation script | `./scripts/qa-agent.sh [commit] [mode]` |
| `qa-agent-claude.sh` | Claude Code integration | `./scripts/qa-agent-claude.sh` |

### Workflow

1. **Automatic (Recommended)**
   - Push to main → CI triggers → QA agent runs automatically
   - If tests fail: Issue created + PR with fixes

2. **Manual (Development)**
   ```bash
   # Make changes
   git commit -m "feature: add new endpoint"

   # Run QA analysis
   ./scripts/qa-agent.sh HEAD analyze

   # If issues found, generate fixes
   ./scripts/qa-agent.sh HEAD fix

   # Review and apply fixes interactively
   ./scripts/qa-agent-claude.sh
   ```

### Modes

#### analyze
- Runs tests
- Generates report
- Identifies missing tests
- No fixes generated

**Use when:** You want to see what needs fixing without auto-generating fixes

```bash
./scripts/qa-agent.sh HEAD analyze
cat qa-agent-report.md
```

#### fix
- Runs tests
- Analyzes failures
- Generates fix prompt
- Prepares Claude Code integration

**Use when:** You want to fix issues interactively with Claude Code

```bash
./scripts/qa-agent.sh HEAD fix
./scripts/qa-agent-claude.sh  # Start interactive session
```

#### full (CI default)
- Complete analysis
- Fix generation
- Missing test detection
- Report creation

**Use when:** Running in CI or doing comprehensive analysis

```bash
./scripts/qa-agent.sh HEAD full
```

### Configuration

#### Change Analysis Patterns

Edit `qa-agent.sh` to customize file pattern detection:

```bash
# Add new pattern
if [[ $file == custom-path/* ]]; then
    CUSTOM_CHANGED=true
fi
```

#### Adjust Test Commands

Modify test execution in `qa-agent.sh`:

```bash
# Backend tests with different options
dotnet test --no-restore --verbosity normal --filter "Category=Integration"

# Frontend tests with custom configuration
npm run test:ci -- --maxWorkers=2
```

### Output Files

The QA agent generates several files:

| File | Content | Keep? |
|------|---------|-------|
| `qa-agent-report.md` | Analysis summary | Yes (for reference) |
| `qa-agent-prompt.txt` | Claude Code prompt | Yes (for manual fixes) |
| `qa-backend-tests.log` | Backend test output | No (temporary) |
| `qa-user-portal-tests.log` | User portal test output | No (temporary) |
| `qa-admin-portal-tests.log` | Admin portal test output | No (temporary) |

### Integration with Claude Code

The `qa-agent-claude.sh` script provides interactive integration:

1. Detects test failures
2. Generates analysis prompt
3. Creates branch for fixes
4. Starts Claude Code in interactive mode
5. Commits and creates PR after fixes

**Requirements:**
- Claude Code CLI installed
- GitHub CLI (`gh`) installed and authenticated
- Write access to repository

### Troubleshooting

#### Script won't run

**Error:** `permission denied`

**Fix:**
```bash
chmod +x scripts/qa-agent.sh
chmod +x scripts/qa-agent-claude.sh
```

#### Tests fail in script but pass manually

**Possible causes:**
1. Environment differences
2. Test isolation issues
3. Timing issues

**Debug:**
```bash
# Run tests the same way the script does
cd frontend/user-portal && npm run test:ci

# Check for environment variables
env | grep -i test
```

#### Claude Code not found

**Error:** `claude: command not found`

**Fix:**
1. Install Claude Code CLI
2. Add to PATH
3. Or use manual mode:

```bash
# Generate prompt
./scripts/qa-agent.sh HEAD fix

# Manually start Claude Code
claude chat --project=.

# Load prompt in chat
# Paste contents of qa-agent-prompt.txt
```

### Best Practices

1. **Run before commit**
   ```bash
   ./scripts/qa-agent.sh HEAD analyze
   ```

2. **Review auto-generated fixes**
   - Don't blindly merge QA agent PRs
   - Check that root causes were addressed
   - Verify tests are meaningful

3. **Iterate on patterns**
   - Update detection logic as project evolves
   - Add new test patterns to skill
   - Document special cases

4. **Monitor effectiveness**
   ```bash
   # Count auto-fixes
   gh pr list --label "qa-agent"

   # Check success rate
   gh pr list --label "qa-agent" --state merged
   ```

### Examples

#### Example 1: Fix backend test failure

```bash
# Scenario: Integration test failing after model change

# Analyze
./scripts/qa-agent.sh HEAD analyze

# Output shows: Backend tests failed (1 failure)
# File: qa-backend-tests.log

# View failure
cat qa-backend-tests.log | grep -A 20 "Failed"

# Generate fix
./scripts/qa-agent.sh HEAD fix

# Interactive fix
./scripts/qa-agent-claude.sh
# Claude Code will analyze and propose fix

# Review and merge PR
```

#### Example 2: Generate tests for new feature

```bash
# Scenario: Added new endpoint, forgot tests

# Analyze
./scripts/qa-agent.sh HEAD analyze

# Output shows: Missing test for src/NewEndpoint.cs

# Generate test
./scripts/qa-agent.sh HEAD fix
./scripts/qa-agent-claude.sh

# Claude Code generates integration test following patterns
# Review and merge PR
```

#### Example 3: CI integration

```yaml
# .github/workflows/qa-agent.yml
- name: Run QA Agent
  run: ./scripts/qa-agent.sh ${{ github.sha }} full

- name: Create Issue if failed
  if: failure()
  uses: actions/create-issue@v1
  with:
    title: "QA Agent: Test failures detected"
    body-path: qa-agent-report.md
```

### Advanced Usage

#### Custom test runner

Create a wrapper script:

```bash
#!/bin/bash
# custom-test.sh

# Run tests with custom config
export TEST_ENV=ci
export TIMEOUT=30000

./scripts/qa-agent.sh HEAD full
```

#### Parallel execution

```bash
# Run analysis for multiple commits
for commit in $(git log --oneline -n 5 --format="%H"); do
    ./scripts/qa-agent.sh "$commit" analyze &
done
wait
```

#### Integration with hooks

```bash
# .git/hooks/pre-push
#!/bin/bash

echo "Running QA analysis before push..."
./scripts/qa-agent.sh HEAD analyze

if [ $? -ne 0 ]; then
    echo "QA analysis failed. Fix issues before pushing."
    exit 1
fi
```

## Additional Scripts

### Database Scripts

```bash
# Apply migrations (if you add this script)
./scripts/migrate.sh up

# Rollback migration
./scripts/migrate.sh down
```

### Development Scripts

```bash
# Start all services (if you add this script)
./scripts/dev.sh start

# Stop services
./scripts/dev.sh stop
```

## Contributing

When adding new scripts:

1. Make executable: `chmod +x scripts/new-script.sh`
2. Add usage documentation to this README
3. Include help text in the script:
   ```bash
   if [ "$1" == "--help" ]; then
       echo "Usage: $0 [options]"
       exit 0
   fi
   ```
4. Test thoroughly before committing
5. Update documentation

## Resources

- [QA Agent Documentation](../docs/QA-AGENT.md)
- [QA Agent Skill](~/.claude/skills/qa-agent/skill.md)
- [Testing Patterns](~/.claude/skills/testing/skill.md)
- [CI/CD Workflows](../.github/workflows/)

## Support

For issues with scripts:
1. Check script logs
2. Review documentation above
3. Create issue with `scripts` label
4. Include error output and environment details
