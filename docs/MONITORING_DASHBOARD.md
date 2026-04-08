# Monitoring Dashboard Configuration

This document provides configuration details for setting up comprehensive monitoring during and after the Entra ID migration.

---

## Overview

**Purpose:** Real-time visibility into authentication health, performance, and user experience during the migration and ongoing operations.

**Tools:**
- **Application Insights** (Primary - Azure)
- **Seq** (Alternative - Self-hosted logs)
- **Grafana** (Alternative - Self-hosted metrics)
- **Azure Monitor** (Azure-specific metrics)

**Update Frequency:**
- Critical metrics: Every 1 minute
- Performance metrics: Every 5 minutes
- Trend metrics: Every 15 minutes

---

## Application Insights Dashboard

### Dashboard Setup

**Create Dashboard:**

1. Azure Portal → Dashboards → New Dashboard
2. Name: "Gymnastics Platform - Authentication Health"
3. Add tiles (see configurations below)
4. Share with: Platform Team, DevOps, Support

### Critical Metrics Tiles

#### 1. Authentication Success Rate

**Query (KQL):**
```kusto
let timeRange = 5m;
let attempts = customMetrics
  | where timestamp > ago(timeRange)
  | where name == "auth.authentication.attempts"
  | summarize TotalAttempts = sum(value);
let failures = customMetrics
  | where timestamp > ago(timeRange)
  | where name == "auth.authentication.failures"
  | summarize TotalFailures = sum(value);
attempts
| extend Failures = toscalar(failures)
| extend SuccessRate = 100.0 * (TotalAttempts - Failures) / TotalAttempts
| project SuccessRate
```

**Visualization:** Big Number
**Target:** > 95%
**Alert Threshold:** < 90%

---

#### 2. Active Authentication Sessions

**Query (KQL):**
```kusto
traces
| where timestamp > ago(15m)
| where message contains "User authenticated successfully"
| summarize ActiveUsers = dcount(tostring(customDimensions.userId))
| project ActiveUsers
```

**Visualization:** Big Number
**Trend:** Should increase after migration (users logging back in)

---

#### 3. Authentication Duration (P95)

**Query (KQL):**
```kusto
customMetrics
| where timestamp > ago(15m)
| where name == "auth.authentication.duration"
| summarize P50 = percentile(value, 50),
            P95 = percentile(value, 95),
            P99 = percentile(value, 99)
| project P50, P95, P99
```

**Visualization:** Line Chart (3 lines)
**Target:** P95 < 2000ms
**Alert Threshold:** P95 > 3000ms

---

#### 4. Provider Distribution

**Query (KQL):**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "authenticated successfully"
| extend provider = tostring(customDimensions.provider)
| summarize Count = count() by provider
| render piechart
```

**Visualization:** Pie Chart
**Expected After Migration:**
- EntraId: 100%
- Keycloak: 0%

---

#### 5. API Error Rate

**Query (KQL):**
```kusto
let timeRange = 5m;
let totalRequests = requests
  | where timestamp > ago(timeRange)
  | count;
let errorRequests = requests
  | where timestamp > ago(timeRange)
  | where resultCode >= 400
  | count;
print ErrorRate = 100.0 * toscalar(errorRequests) / toscalar(totalRequests)
```

**Visualization:** Big Number (with trend)
**Target:** < 5%
**Alert Threshold:** > 10%

---

#### 6. Token Refresh Success Rate

**Query (KQL):**
```kusto
let timeRange = 15m;
customMetrics
| where timestamp > ago(timeRange)
| where name in ("auth.token.refresh.success", "auth.token.refresh.failure")
| summarize count() by name
| extend MetricType = case(name contains "success", "Success", "Failure")
| summarize Total = sum(count_) by MetricType
| extend SuccessRate = 100.0 * Total / (Total + toscalar(Total))
| where MetricType == "Success"
| project SuccessRate
```

**Visualization:** Big Number
**Target:** > 99%
**Alert Threshold:** < 95%

---

### Time-Series Metrics

#### 7. Authentication Attempts Over Time

**Query (KQL):**
```kusto
customMetrics
| where timestamp > ago(1h)
| where name in ("auth.authentication.attempts", "auth.authentication.failures")
| extend MetricType = case(name contains "failure", "Failures", "Attempts")
| summarize Count = sum(value) by bin(timestamp, 5m), MetricType
| render timechart
```

**Visualization:** Area Chart (stacked)
**Y-Axis:** Count
**X-Axis:** Time

---

#### 8. Authentication Latency Over Time

**Query (KQL):**
```kusto
customMetrics
| where timestamp > ago(1h)
| where name == "auth.authentication.duration"
| summarize P50 = percentile(value, 50),
            P95 = percentile(value, 95)
          by bin(timestamp, 5m)
| render timechart
```

**Visualization:** Line Chart
**Y-Axis:** Milliseconds
**X-Axis:** Time

---

#### 9. API Requests by Status Code

**Query (KQL):**
```kusto
requests
| where timestamp > ago(1h)
| summarize Count = count() by resultCode, bin(timestamp, 5m)
| render timechart
```

**Visualization:** Line Chart (multi-line)
**Legend:** Group by resultCode (200, 401, 403, 500, etc.)

---

### Error Analysis

#### 10. Top Authentication Errors

**Query (KQL):**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "authentication failed"
| extend reason = tostring(customDimensions.reason)
| summarize Count = count() by reason
| order by Count desc
| take 10
```

**Visualization:** Bar Chart (horizontal)

---

#### 11. Failed Login Attempts by Email

**Query (KQL):**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "authentication failed"
| extend email = tostring(customDimensions.email)
| summarize FailedAttempts = count() by email
| where FailedAttempts > 3
| order by FailedAttempts desc
| take 20
```

**Visualization:** Table
**Purpose:** Detect brute force attempts or user lockouts

---

#### 12. Error Details Table

**Query (KQL):**
```kusto
exceptions
| where timestamp > ago(1h)
| where outerMessage contains "auth" or outerMessage contains "login"
| project
    timestamp,
    operation_Name,
    problemId,
    outerMessage,
    innermostMessage
| order by timestamp desc
| take 50
```

**Visualization:** Table
**Purpose:** Debug specific authentication failures

---

## Alert Rules

### Critical Alerts (Immediate Action Required)

#### Alert 1: Authentication Success Rate Drops Below 80%

**Condition:**
```kusto
let timeRange = 15m;
let attempts = customMetrics
  | where timestamp > ago(timeRange)
  | where name == "auth.authentication.attempts"
  | summarize TotalAttempts = sum(value);
let failures = customMetrics
  | where timestamp > ago(timeRange)
  | where name == "auth.authentication.failures"
  | summarize TotalFailures = sum(value);
attempts
| extend Failures = toscalar(failures)
| extend SuccessRate = 100.0 * (TotalAttempts - Failures) / TotalAttempts
| where SuccessRate < 80
```

**Severity:** Critical (P1)
**Action:** Page on-call engineer immediately
**Recipients:** DevOps team, Platform architect
**Frequency:** Every 5 minutes until resolved

---

#### Alert 2: API Error Rate Exceeds 15%

**Condition:**
```kusto
let timeRange = 5m;
let totalRequests = requests
  | where timestamp > ago(timeRange)
  | count;
let errorRequests = requests
  | where timestamp > ago(timeRange)
  | where resultCode >= 400
  | count;
let errorRate = 100.0 * toscalar(errorRequests) / toscalar(totalRequests);
print errorRate
| where errorRate > 15
```

**Severity:** Critical (P1)
**Action:** Page on-call engineer
**Recipients:** DevOps team, Platform architect
**Frequency:** Every 5 minutes

---

#### Alert 3: Database Connection Failures

**Condition:**
```kusto
exceptions
| where timestamp > ago(5m)
| where outerMessage contains "database" or outerMessage contains "connection"
| count
| where Count > 5
```

**Severity:** Critical (P1)
**Action:** Page on-call engineer + DBA
**Recipients:** DevOps team, DBA team
**Frequency:** Immediately

---

### High Priority Alerts (Urgent Response)

#### Alert 4: Authentication Success Rate 80-90%

**Condition:** Same as Alert 1 but threshold between 80-90%
**Severity:** High (P2)
**Action:** Notify on-call engineer via Slack
**Recipients:** #platform-alerts Slack channel
**Frequency:** Every 15 minutes

---

#### Alert 5: Token Refresh Failure Rate > 5%

**Condition:**
```kusto
let timeRange = 15m;
let failures = customMetrics
  | where timestamp > ago(timeRange)
  | where name == "auth.token.refresh.failure"
  | summarize Failures = sum(value);
let total = customMetrics
  | where timestamp > ago(timeRange)
  | where name in ("auth.token.refresh.success", "auth.token.refresh.failure")
  | summarize Total = sum(value);
failures
| extend TotalCount = toscalar(total)
| extend FailureRate = 100.0 * Failures / TotalCount
| where FailureRate > 5
```

**Severity:** High (P2)
**Action:** Notify platform team
**Recipients:** #platform-alerts Slack channel
**Frequency:** Every 15 minutes

---

#### Alert 6: P95 Latency > 3 Seconds

**Condition:**
```kusto
customMetrics
| where timestamp > ago(15m)
| where name == "auth.authentication.duration"
| summarize P95 = percentile(value, 95)
| where P95 > 3000
```

**Severity:** High (P2)
**Action:** Notify platform team
**Recipients:** #platform-alerts Slack channel
**Frequency:** Every 15 minutes

---

### Medium Priority Alerts (Monitor and Investigate)

#### Alert 7: Increased Failed Login Attempts (Possible Brute Force)

**Condition:**
```kusto
traces
| where timestamp > ago(1h)
| where message contains "authentication failed"
| extend email = tostring(customDimensions.email)
| summarize FailedAttempts = count() by email
| where FailedAttempts > 10
| count
| where Count > 5
```

**Severity:** Medium (P3)
**Action:** Security team review
**Recipients:** Security team + platform team
**Frequency:** Every 1 hour

---

## Seq Dashboard (Alternative/Complement)

### Seq Queries

#### Query 1: Recent Authentication Events

**Query:**
```
Level >= "Information" AND MessageTemplate like "%authenticated%"
| select @Timestamp, Level, MessageTemplate, Provider, Email, TenantId
| order by @Timestamp desc
| limit 100
```

---

#### Query 2: Authentication Failures by Provider

**Query:**
```
Level = "Warning" AND MessageTemplate like "%authentication failed%"
| group by Provider
| select Provider, count() as FailureCount
| order by FailureCount desc
```

---

#### Query 3: Slow Authentications (> 2 seconds)

**Query:**
```
MessageTemplate like "%authenticated successfully%" AND Duration > 2000
| select @Timestamp, Email, Provider, Duration
| order by Duration desc
| limit 50
```

---

## Grafana Dashboard (Alternative)

### Prometheus Metrics

**Metrics to Collect:**

```yaml
# .NET Metrics (via OpenTelemetry)
- auth_authentication_attempts_total (counter)
- auth_authentication_failures_total (counter)
- auth_authentication_duration_milliseconds (histogram)
- auth_token_refresh_success_total (counter)
- auth_token_refresh_failure_total (counter)

# HTTP Metrics
- http_request_duration_ms (histogram)
- http_requests_total (counter by status_code)
```

### Panel 1: Authentication Success Rate

**Query:**
```promql
100 * (
  rate(auth_authentication_attempts_total[5m]) -
  rate(auth_authentication_failures_total[5m])
) / rate(auth_authentication_attempts_total[5m])
```

**Visualization:** Gauge (0-100%)
**Thresholds:**
- Red: < 90%
- Yellow: 90-95%
- Green: > 95%

---

### Panel 2: Authentication Duration

**Query:**
```promql
histogram_quantile(0.95,
  rate(auth_authentication_duration_milliseconds_bucket[5m])
)
```

**Visualization:** Graph
**Y-Axis:** Milliseconds
**Thresholds:**
- Red: > 3000ms
- Yellow: 2000-3000ms
- Green: < 2000ms

---

### Panel 3: Requests by Status Code

**Query:**
```promql
sum(rate(http_requests_total[5m])) by (status_code)
```

**Visualization:** Stacked Area Chart
**Legend:** By status_code

---

## Support Team Dashboard

### Zendesk / Support Ticket Metrics

**Widgets:**

1. **Open Tickets by Category**
   - Authentication Issues
   - Login Issues
   - OAuth Issues
   - Other

2. **Average First Response Time**
   - Target: < 30 minutes
   - Alert: > 60 minutes

3. **Average Resolution Time**
   - Target: < 2 hours
   - Alert: > 4 hours

4. **Tickets Created (Last 24h)**
   - Trend line showing increase/decrease
   - Compare to previous week

5. **Escalations**
   - Count of escalations to platform team
   - Escalation reasons

---

## Migration Day Dashboard

### Temporary Dashboard (Migration Day Only)

**Purpose:** Real-time monitoring during the 2-hour migration window

**Tiles:**

1. **Migration Status** (Manual Update)
   - Current Step: [Step name]
   - Status: ⬜ Pending / 🟡 In Progress / ✅ Complete / ❌ Failed
   - Next Step: [Step name]

2. **Smoke Test Results**
   - Email/Password Login: ⬜ / ✅ / ❌
   - Google OAuth: ⬜ / ✅ / ❌
   - Microsoft OAuth: ⬜ / ✅ / ❌
   - API Health: ⬜ / ✅ / ❌

3. **Real-Time Authentication Attempts**
   - Last 5 minutes: [count]
   - Success rate: [percentage]
   - Failures: [count]

4. **Active Users**
   - Users logged in since migration: [count]
   - Unique users: [count]

5. **Error Log (Live)**
   - Last 10 authentication errors
   - Auto-refresh every 10 seconds

6. **Rollback Decision**
   - Rollback Criteria Met: ❌ No / ⚠️ Warning / ✅ Yes
   - Auth success rate < 80%: ❌
   - P1 incident: ❌
   - Smoke tests failed: ❌

---

## Post-Migration Monitoring Schedule

### Day 1-3: Active Monitoring

**Check Frequency:** Every 4 hours
**Metrics:**
- Authentication success rate
- Active user count
- Support ticket volume
- Error rates

**Actions:**
- Daily summary to stakeholders
- Immediate escalation if metrics degrade

---

### Day 4-7: Reduced Monitoring

**Check Frequency:** Once daily
**Metrics:**
- Same as above
- Trend analysis

**Actions:**
- Daily summary email
- Weekly review meeting

---

### Week 2+: Normal Operations

**Check Frequency:** Weekly review
**Metrics:**
- Weekly aggregates
- Month-over-month trends

**Actions:**
- Weekly platform health review
- Quarterly architecture review

---

## Incident Response Runbook

### If Authentication Success Rate Drops Below 90%

**Immediate Actions (within 5 minutes):**

1. **Check dashboard**
   - Identify provider causing failures (Entra or both)
   - Check error details table
   - Review recent deployments

2. **Notify team**
   - Post to #platform-alerts Slack channel
   - Page on-call engineer if < 80%

3. **Assess impact**
   - How many users affected?
   - Is it specific to one provider?
   - Is it all users or specific accounts?

4. **Triage**
   - If Entra-specific: Check Azure service health
   - If all providers: Check API server health
   - If specific users: Check those user accounts

5. **Decide**
   - If fixable in 15 min: Fix and monitor
   - If not fixable quickly: Consider rollback

**Rollback Decision Criteria:**
- Success rate < 80% for >15 minutes
- No clear fix identified
- Critical security issue
- P1 incident declared

---

## Dashboard Maintenance

### Weekly Review

**Every Monday:**
- Review alert thresholds (are they too sensitive/loose?)
- Check for new error patterns
- Update queries if needed
- Review dashboard layout (is it still useful?)

### Monthly Review

**First Monday of month:**
- Audit alert rules (which fired, which didn't)
- Remove obsolete metrics
- Add new metrics based on learnings
- Update documentation

---

## Appendix: Complete Dashboard Export

### Application Insights Dashboard JSON

**File:** `azure-monitoring-dashboard.json`

```json
{
  "lenses": {
    "0": {
      "order": 0,
      "parts": {
        "0": {
          "position": {"x": 0, "y": 0, "colSpan": 6, "rowSpan": 4},
          "metadata": {
            "type": "Extension/Microsoft_OperationsManagementSuite_Workspace/PartType/LogsTablePart",
            "inputs": [],
            "settings": {
              "content": {
                "Query": "customMetrics\n| where timestamp > ago(5m)\n| where name == \"auth.authentication.attempts\"\n| summarize TotalAttempts = sum(value)\n| extend Failures = toscalar(customMetrics | where timestamp > ago(5m) | where name == \"auth.authentication.failures\" | summarize sum(value))\n| extend SuccessRate = 100.0 * (TotalAttempts - Failures) / TotalAttempts\n| project SuccessRate"
              }
            }
          }
        }
        // Additional tiles...
      }
    }
  },
  "metadata": {
    "model": {
      "timeRange": {
        "value": {
          "relative": {
            "duration": 24,
            "timeUnit": 1
          }
        }
      }
    }
  }
}
```

**Import Instructions:**
1. Azure Portal → Dashboards → Upload
2. Select `azure-monitoring-dashboard.json`
3. Click "Upload"
4. Dashboard appears in your dashboard list

---

**Document Version:** 1.0
**Last Updated:** 2026-04-08
**Maintained By:** DevOps Team
