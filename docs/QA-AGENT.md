# QA Autonomous Agent

The QA Autonomous Agent automatically detects, analyzes, and fixes test failures without human intervention.

## Quick Start

```bash
# Setup cron job
./scripts/setup-qa-cron.sh

# Or run manually
./scripts/qa-agent-autonomous.sh
```

## Architecture

**Two-Tier System:**

1. **GitHub Actions** (Tier 1) - Immediate notification
   - Runs on push to main
   - Creates issues when tests fail
   - Does NOT fix tests

2. **Local Autonomous Agent** (Tier 2) - Actual fixing
   - Runs on schedule (cron)
   - Uses Claude Code to analyze and fix
   - Creates PRs automatically

## How It Works

```
Cron → Poll repo → Run tests → Claude analyzes → Fix → Create PR
```

The agent:
1. Fetches latest main
2. Checks for new commits
3. Runs all tests if changes detected
4. If failures found, invokes Claude Code to fix
5. Creates PR with fixes

## Files

- `scripts/qa-agent-autonomous.sh` - Main agent script
- `scripts/setup-qa-cron.sh` - Cron setup wizard
- `.qa-agent-state` - Tracks last checked commit
- `.qa-agent.log` - Execution log

## Prerequisites

- Claude Code CLI (`claude` command)
- GitHub CLI (`gh`)
- Node.js & .NET SDK

## Usage

```bash
# Setup scheduled runs
./scripts/setup-qa-cron.sh

# Manual run
./scripts/qa-agent-autonomous.sh

# View logs
tail -f .qa-agent.log

# Check state
cat .qa-agent-state
```

## Troubleshooting

```bash
# Check if cron job exists
crontab -l | grep qa-agent

# View recent logs
tail -n 100 .qa-agent.log

# Check Claude output
cat claude-output.log
```

For detailed documentation, see comments in `scripts/qa-agent-autonomous.sh`.
