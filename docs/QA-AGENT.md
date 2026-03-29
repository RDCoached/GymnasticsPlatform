# QA Agent

Autonomous test monitoring and fixing using Claude Code agents.

## How It Works

After committing code, Claude spawns a QA agent that:
1. Runs all tests (backend + frontends)
2. If failures detected, analyzes root cause
3. Fixes the issues automatically
4. Creates a PR with the fixes

## No Setup Required

The QA agent is invoked automatically after commits. No cron jobs, no scripts, just clean event-driven flow.

## Example Workflow

```
You: "Add a new feature"
Claude: [writes code, commits]
Claude: [spawns QA agent automatically]
Agent: Runs tests → finds failures → fixes them → creates PR
You: Review PR → merge → done!
```

## Manual Invocation

You can also ask Claude to check tests anytime:
- "Run tests and fix any failures"
- "Check if tests are passing"
- "QA agent please"

Claude will spawn the agent on demand.
