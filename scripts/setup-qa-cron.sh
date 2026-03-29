#!/usr/bin/env bash
set -euo pipefail

# Setup cron job for QA autonomous agent

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
QA_SCRIPT="$SCRIPT_DIR/qa-agent-autonomous.sh"

echo "Setting up QA Autonomous Agent cron job..."
echo ""
echo "This will schedule the agent to run periodically and check for test failures."
echo ""

# Ask user for schedule
echo "How often should the agent run?"
echo "1) Every hour"
echo "2) Every 4 hours"
echo "3) Every 12 hours"
echo "4) Once daily (at 2 AM)"
echo "5) Custom cron expression"
read -p "Choose (1-5): " choice

case $choice in
    1)
        CRON_SCHEDULE="0 * * * *"
        DESCRIPTION="every hour"
        ;;
    2)
        CRON_SCHEDULE="0 */4 * * *"
        DESCRIPTION="every 4 hours"
        ;;
    3)
        CRON_SCHEDULE="0 */12 * * *"
        DESCRIPTION="every 12 hours"
        ;;
    4)
        CRON_SCHEDULE="0 2 * * *"
        DESCRIPTION="daily at 2 AM"
        ;;
    5)
        read -p "Enter cron expression (e.g., '0 */2 * * *'): " CRON_SCHEDULE
        DESCRIPTION="custom schedule"
        ;;
    *)
        echo "Invalid choice. Exiting."
        exit 1
        ;;
esac

CRON_JOB="$CRON_SCHEDULE cd $PROJECT_ROOT && $QA_SCRIPT >> $PROJECT_ROOT/.qa-agent.log 2>&1"

echo ""
echo "Will add cron job: $DESCRIPTION"
echo "Command: $CRON_JOB"
echo ""
read -p "Proceed? (y/n): " confirm

if [[ "$confirm" != "y" ]]; then
    echo "Cancelled."
    exit 0
fi

# Add to crontab
(crontab -l 2>/dev/null | grep -v "qa-agent-autonomous.sh"; echo "$CRON_JOB") | crontab -

echo "✅ Cron job added successfully!"
echo ""
echo "The QA agent will run $DESCRIPTION and:"
echo "  1. Check for new commits"
echo "  2. Run tests if changes detected"
echo "  3. Analyze and fix any failures"
echo "  4. Create PRs with fixes"
echo ""
echo "Logs: $PROJECT_ROOT/.qa-agent.log"
echo "State: $PROJECT_ROOT/.qa-agent-state"
echo ""
echo "To view current crontab: crontab -l"
echo "To remove cron job: crontab -e (then delete the line)"
echo ""
echo "To test manually: $QA_SCRIPT"
