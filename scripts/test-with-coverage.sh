#!/bin/bash
set -e

echo "======================================"
echo "Running Tests with Code Coverage"
echo "======================================"

# Clean previous coverage data
rm -rf coverage-results coverage-report

# Run tests with coverlet data collection
dotnet test \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage-results \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

# Install ReportGenerator if not already installed
if ! dotnet tool list -g | grep -q reportgenerator; then
    echo "Installing ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
fi

# Generate HTML and XML reports
echo ""
echo "Generating Coverage Reports..."
reportgenerator \
  -reports:"coverage-results/**/coverage.opencover.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:"Html;Cobertura;TextSummary;Badges" \
  -verbosity:Info

# Display summary
echo ""
echo "======================================"
echo "Coverage Summary"
echo "======================================"
cat coverage-report/Summary.txt

# Open HTML report (macOS)
if [[ "$OSTYPE" == "darwin"* ]]; then
    open coverage-report/index.html
fi

echo ""
echo "Full report available at: coverage-report/index.html"
