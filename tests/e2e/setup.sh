#!/bin/bash

echo "Setting up Playwright E2E tests..."

# Install npm dependencies
npm install

# Install Playwright browsers
npx playwright install

echo "Setup complete! Run 'npm test' to execute tests."
