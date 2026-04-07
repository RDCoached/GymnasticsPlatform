# Observability Stack Documentation

## Overview

This document describes the comprehensive observability setup for the Gymnastics Platform, including dashboards, metrics, traces, logs, and alerts.

## Stack Components

- **Grafana** (http://localhost:3000) - Visualization and dashboards
- **Prometheus** (http://localhost:9090) - Metrics storage and querying
- **Loki** (http://localhost:3100) - Log aggregation
- **Tempo** (http://localhost:3200) - Distributed tracing
- **OpenTelemetry** - Instrumentation SDK

## Dashboards

### 1. API Overview Dashboard

**Purpose**: High-level API performance monitoring

**Key Panels**:
- Request rate (requests/sec) by endpoint
- Error rate (4xx/5xx)
- Response time P95 latency
- Total requests and errors (last 5m)
- Active HTTP connections
- Request duration distribution (heatmap)
- Top 10 slowest endpoints
- Recent 5xx error logs

**Use Cases**:
- Quickly identify performance degradation
- Spot traffic spikes or drops
- Find slow endpoints needing optimization

### 2. Auth Module Dashboard

**Purpose**: Authentication and onboarding monitoring

**Key Panels**:
- Login success rate (gauge)
- Registration rate over time
- Token refresh rate
- Failed logins (last hour)
- Onboarding funnel (status checks → club creation/join/individual)
- Failed auth attempts by status code
- Login attempts over time (successful vs failed)
- Session creation rate

**Use Cases**:
- Monitor authentication health
- Track onboarding conversion rates
- Detect brute force attacks (high failed login rate)
- Analyze user registration trends

### 3. Database Performance Dashboard

**Purpose**: PostgreSQL and EF Core monitoring

**Key Panels**:
- Query duration (P50/P95/P99)
- Queries per second
- Connection pool usage (gauge)
- Active/idle/max connections
- Slow queries (>1s) table
- Query duration distribution (heatmap)
- Connection errors by type

**Use Cases**:
- Identify slow queries needing indexes
- Monitor connection pool exhaustion
- Detect database performance issues
- Track query patterns and hot tables

### 4. Business Metrics Dashboard

**Purpose**: Product KPIs and user behavior

**Key Panels**:
- New registrations (daily trend)
- Active users (DAU)
- Clubs created (today)
- Users joined clubs (today)
- Individual mode users (today)
- Successful logins (today)
- Onboarding completion rate (gauge)
- Onboarding choice distribution (pie chart)
- Weekly registration trend
- User retention (7-day)

**Use Cases**:
- Track product growth
- Measure feature adoption
- Calculate conversion funnels
- Monitor user engagement

### 5. Infrastructure Dashboard

**Purpose**: System-level runtime metrics

**Key Panels**:
- CPU usage %
- Memory usage (working set, private memory)
- GC collections (Gen 0/1/2)
- GC heap size
- Thread pool threads and queue length
- Thread pool starvation (gauge)
- Allocated memory rate
- HTTP client connections
- Process information

**Use Cases**:
- Detect memory leaks
- Monitor GC pressure
- Identify thread pool starvation
- Track resource utilization

### 6. Errors & Alerts Dashboard

**Purpose**: Error monitoring and alerting

**Key Panels**:
- Error rate by status code (timeseries)
- Total errors (last hour)
- 5xx errors (last hour)
- Error distribution by type (pie chart)
- Top 10 error endpoints (table)
- Recent error logs (live)
- Individual status code stats (400, 401, 403, 404, 409, 500)

**Use Cases**:
- Triage production incidents
- Find error hotspots
- Track error trends
- View contextual error logs

## Instrumentation Added

### OpenTelemetry Configuration

**Traces**:
- ASP.NET Core HTTP requests (with exception recording)
- HttpClient calls
- **Entity Framework Core queries** (NEW)
  - SQL statements captured
  - Command type tagging
  - Duration tracking
- Custom ActivitySource support for `GymnasticsPlatform.*`

**Metrics**:
- ASP.NET Core (request duration, active requests)
- HttpClient (request duration, active requests)
- **Runtime metrics** (NEW)
  - GC collections (Gen 0/1/2)
  - GC heap size
  - Memory allocations
- **Process metrics** (NEW)
  - CPU usage
  - Memory usage
  - Thread count
- Custom Meter support for `GymnasticsPlatform.*`

**Logs**:
- Structured logging via OpenTelemetry
- OTLP export to Loki
- Correlation with traces (trace ID in logs)

### Packages Added

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.EntityFrameworkCore" Version="1.15.0-beta.1" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.0" />
```

## Prometheus Alerts

Located in: `docker/prometheus/alerts.yml`

### API Alerts

- **HighErrorRate**: 5xx error rate > 5% for 5 minutes (CRITICAL)
- **HighLatency**: P95 latency > 2s for 10 minutes (WARNING)
- **APIDown**: Service not responding for 1 minute (CRITICAL)

### Database Alerts

- **DatabaseConnectionPoolExhausted**: Pool usage > 80% for 5 minutes (WARNING)
- **SlowDatabaseQueries**: P95 query duration > 1s for 10 minutes (WARNING)
- **DatabaseConnectionErrors**: Connection errors > 0.1/sec for 5 minutes (CRITICAL)

### Auth Alerts

- **HighFailedLoginRate**: Failed logins > 10/sec for 5 minutes (WARNING)
- **LoginEndpointDown**: 500 errors on /api/auth/login for 2 minutes (CRITICAL)

### Infrastructure Alerts

- **HighMemoryUsage**: Memory usage > 85% for 5 minutes (WARNING)
- **ThreadPoolStarvation**: Queue length > 50 for 5 minutes (WARNING)
- **HighGCPressure**: Gen 2 GCs > 0.1/sec for 10 minutes (WARNING)

## How to Use

### Viewing Dashboards

1. Start the observability stack:
   ```bash
   docker-compose up -d
   ```

2. Open Grafana:
   ```
   http://localhost:3000
   ```
   - Username: `admin`
   - Password: `admin` (change on first login)

3. Navigate to **Dashboards** in the left sidebar

4. All dashboards are auto-provisioned from:
   ```
   docker/grafana/provisioning/dashboards/*.json
   ```

### Querying Metrics (Prometheus)

1. Open Prometheus UI:
   ```
   http://localhost:9090
   ```

2. Example queries:
   ```promql
   # Request rate
   rate(http_server_request_duration_seconds_count[5m])

   # Error rate
   sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m]))

   # P95 latency
   histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))
   ```

### Viewing Logs (Loki)

Logs are integrated into Grafana dashboards (see "Recent Error Logs" panels).

To query directly:
1. Go to Grafana → Explore
2. Select **Loki** datasource
3. Example queries:
   ```logql
   # All errors
   {job="gymnastics-api"} |~ "(?i)error"

   # 5xx errors with JSON parsing
   {job="gymnastics-api"} |= "error" | json | http_response_status_code >= 500

   # Specific endpoint logs
   {job="gymnastics-api"} | json | http_route="/api/auth/login"
   ```

### Viewing Traces (Tempo)

1. Go to Grafana → Explore
2. Select **Tempo** datasource
3. Options:
   - Search by Trace ID (from logs)
   - Search recent traces
   - Jump from metrics → traces (via trace exemplars)

## Adding Custom Metrics

### Example: Business Event Counter

```csharp
// In Program.cs, add after OpenTelemetry configuration:
builder.Services.AddSingleton<ActivitySource>(sp =>
    new ActivitySource("GymnasticsPlatform.Api"));

builder.Services.AddSingleton(sp =>
{
    var meterFactory = sp.GetRequiredService<IMeterFactory>();
    return meterFactory.Create("GymnasticsPlatform.Api");
});

// In a service or endpoint:
public sealed class OrderService(ActivitySource activitySource, Meter meter)
{
    private readonly Counter<long> _ordersCreated = meter.CreateCounter<long>(
        "orders.created.total",
        description: "Total number of orders created");

    public async Task<Order> CreateOrderAsync(CreateOrderRequest request)
    {
        using var activity = activitySource.StartActivity("CreateOrder");
        activity?.SetTag("order.productId", request.ProductId);
        activity?.SetTag("order.quantity", request.Quantity);

        try
        {
            var order = await _repository.SaveAsync(request);

            _ordersCreated.Add(1, new KeyValuePair<string, object?>("product_id", request.ProductId));

            activity?.SetTag("order.id", order.Id);
            return order;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

### Querying Custom Metrics

```promql
# Total orders created
orders_created_total

# Orders created per second
rate(orders_created_total[5m])

# Orders by product ID
sum(rate(orders_created_total[5m])) by (product_id)
```

## Troubleshooting

### Dashboards Not Loading

1. Check Grafana is running:
   ```bash
   docker ps | grep grafana
   ```

2. Check datasource connectivity:
   - Grafana → Configuration → Data sources
   - Click "Test" on each datasource

3. Restart Grafana to reload dashboards:
   ```bash
   docker-compose restart grafana
   ```

### No Metrics Appearing

1. Verify API is sending metrics:
   ```bash
   curl http://localhost:5001/metrics
   ```

2. Check Prometheus is scraping:
   - Open http://localhost:9090/targets
   - Verify `gymnastics-api` target is UP

3. Check prometheus.yml scrape config:
   ```yaml
   scrape_configs:
     - job_name: 'gymnastics-api'
       static_configs:
         - targets: ['api:8080']  # Update if API port differs
   ```

### No Logs in Loki

1. Verify logs are being sent:
   ```bash
   docker logs gymnastics-api | grep -i "otlp"
   ```

2. Check Loki is receiving logs:
   ```bash
   curl http://localhost:3100/ready
   ```

### Alerts Not Firing

1. Check Prometheus alert rules loaded:
   ```
   http://localhost:9090/rules
   ```

2. Verify alerts.yml syntax:
   ```bash
   docker exec gymnastics-prometheus promtool check rules /etc/prometheus/alerts.yml
   ```

3. Update prometheus.yml to include alerts:
   ```yaml
   rule_files:
     - "/etc/prometheus/alerts.yml"
   ```

## Next Steps

### Recommended Enhancements

1. **Add Alertmanager**: Route alerts to Slack/PagerDuty
2. **Custom Business Metrics**: Track domain-specific KPIs
3. **SLO Dashboards**: Define and track Service Level Objectives
4. **Distributed Tracing**: Add spans for critical business operations
5. **Log Aggregation**: Add structured logging to all endpoints

### Dashboard Customization

All dashboards are editable in Grafana. To make changes permanent:

1. Edit dashboard in Grafana UI
2. Save dashboard
3. Export JSON:
   - Dashboard settings → JSON Model → Copy to clipboard
4. Save to: `docker/grafana/provisioning/dashboards/<name>.json`
5. Commit to git

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/dashboards/build-dashboards/best-practices/)
- [Prometheus Querying](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Loki LogQL](https://grafana.com/docs/loki/latest/logql/)
- [Tempo Tracing](https://grafana.com/docs/tempo/latest/)
