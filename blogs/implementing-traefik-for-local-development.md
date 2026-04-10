# Implementing Traefik for Local Development: A Practical Guide

**Author:** Engineering Team
**Date:** April 2026
**Tags:** Traefik, Docker, Reverse Proxy, Local Development, DevOps

---

## TL;DR

Tired of juggling `localhost:3001`, `localhost:5001`, `localhost:8080` in your local development? We replaced port-based URLs with clean subdomain routing using Traefik v2.11. Now our services are accessible at `api.gymnastics.localhost`, `app.gymnastics.localhost`, and `admin.gymnastics.localhost` — no `/etc/hosts` hacks required.

**Result:** Production-like local environment, OAuth-compatible URLs, and zero port conflicts.

---

## The Problem: Port Soup

Our multi-service application had grown to include:
- API server on port 5001
- User portal on port 3001
- Admin portal on port 3002
- Keycloak (later Entra ID) on port 8080
- Grafana on port 3000
- PostgreSQL on port 5432
- Redis on port 6379
- MailHog on port 8025
- Adminer on port 8081
- CouchDB on port 5984
- Ollama on port 11434

Developers had to memorize port numbers, bookmark multiple URLs, and constantly check which port was which. Worse, OAuth redirect URIs using `localhost:port` don't mirror production behavior where services live on proper domains.

---

## Why Traefik?

**Traefik** is a cloud-native reverse proxy and load balancer designed for microservices. Unlike traditional proxies (nginx, HAProxy), Traefik:

1. **Auto-discovers services** via Docker labels — no manual config files
2. **Updates routes dynamically** when containers start/stop
3. **Provides a dashboard** showing all routes and backends in real-time
4. **Supports multiple providers** (Docker, Kubernetes, Consul, etc.)
5. **Handles TLS termination** with automatic Let's Encrypt integration (production)

For local development, Traefik gives us:
- **Clean subdomain-based URLs** without DNS configuration
- **Production-like routing** for realistic testing
- **OAuth-compatible domains** (no path stripping breaking redirects)
- **Zero port conflicts** — everything runs on port 80
- **Service discovery** — add a new service, add labels, done

---

## The Solution: Subdomain Routing with `.localhost`

We implemented subdomain-based routing using the `.localhost` TLD:

```
http://api.gymnastics.localhost       → API (port 8080 internal)
http://app.gymnastics.localhost       → User Portal (port 3001 internal)
http://admin.gymnastics.localhost     → Admin Portal (port 3002 internal)
http://grafana.gymnastics.localhost   → Grafana (port 3000 internal)
http://db.gymnastics.localhost        → Adminer (port 8080 internal)
http://mail.gymnastics.localhost      → MailHog (port 8025 internal)
http://traefik.gymnastics.localhost   → Traefik Dashboard
```

**Why `.localhost`?** Per [RFC 6761](https://datatracker.ietf.org/doc/html/rfc6761), all `*.localhost` subdomains automatically resolve to `127.0.0.1` without DNS configuration. No `/etc/hosts` editing required — it just works across all operating systems.

---

## Implementation Guide

### Step 1: Create Traefik Configuration

Create `docker/traefik/traefik.yml`:

```yaml
api:
  dashboard: true
  insecure: true  # Dashboard on port 8080 (dev only)

entryPoints:
  web:
    address: ":80"

providers:
  docker:
    endpoint: "unix:///var/run/docker.sock"
    exposedByDefault: false  # Opt-in via labels
    network: gymnastics-network

log:
  level: INFO

accessLog: {}
```

**Key decisions:**
- `exposedByDefault: false` — Services must opt-in with `traefik.enable=true` label
- `network: gymnastics-network` — All services must be on this Docker network
- `insecure: true` — Dashboard accessible without auth (dev only, disable in production)

### Step 2: Add Traefik Service to `docker-compose.yml`

```yaml
services:
  traefik:
    image: traefik:v2.11
    container_name: gymnastics-traefik
    restart: unless-stopped
    ports:
      - "${TRAEFIK_HTTP_PORT:-80}:80"
      - "8080:8080"  # Dashboard
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - ./docker/traefik/traefik.yml:/etc/traefik/traefik.yml:ro
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.traefik.rule=Host(`traefik.gymnastics.localhost`)"
      - "traefik.http.routers.traefik.service=api@internal"
    networks:
      - gymnastics-network
```

**Notes:**
- `TRAEFIK_HTTP_PORT` environment variable allows override if port 80 is taken
- Dashboard also accessible on `http://localhost:8080/dashboard/`
- Docker socket mounted read-only for security

### Step 3: Add Network to All Services

Add to bottom of `docker-compose.yml`:

```yaml
networks:
  gymnastics-network:
    driver: bridge
```

Then add `networks: - gymnastics-network` to **every service** in your compose file.

### Step 4: Add Traefik Labels to Services

For each web-accessible service, add labels:

```yaml
api:
  # ... existing config
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.api.rule=Host(`api.gymnastics.localhost`)"
    - "traefik.http.services.api.loadbalancer.server.port=8080"
  networks:
    - gymnastics-network
```

**Pattern for all services:**

```yaml
labels:
  - "traefik.enable=true"
  - "traefik.http.routers.<service-name>.rule=Host(`<subdomain>.gymnastics.localhost`)"
  - "traefik.http.services.<service-name>.loadbalancer.server.port=<internal-port>"
```

### Step 5: Update Environment Variables

Add to `.env`:

```bash
# Traefik Configuration
TRAEFIK_HTTP_PORT=80

# Service URLs (Traefik-routed)
API_URL=http://api.gymnastics.localhost
USER_PORTAL_URL=http://app.gymnastics.localhost
ADMIN_PORTAL_URL=http://admin.gymnastics.localhost
GRAFANA_URL=http://grafana.gymnastics.localhost
```

Update frontend `.env` files:

```bash
# frontend/user-portal/.env
VITE_API_URL=http://api.gymnastics.localhost
VITE_API_BASE_URL=http://api.gymnastics.localhost
VITE_REDIRECT_URI=http://app.gymnastics.localhost/auth/callback
```

### Step 6: Start the Stack

```bash
docker compose down -v  # Clean slate
docker compose up -d
```

Verify Traefik dashboard:

```bash
open http://traefik.gymnastics.localhost
# or http://localhost:8080/dashboard/
```

You should see all your services with green "healthy" status and active routes.

### Step 7: Test Each Service

```bash
# API health check
curl http://api.gymnastics.localhost/health

# Frontend services
open http://app.gymnastics.localhost
open http://admin.gymnastics.localhost

# Observability
open http://grafana.gymnastics.localhost
open http://prometheus.gymnastics.localhost

# Database UI
open http://db.gymnastics.localhost

# Email UI
open http://mail.gymnastics.localhost
```

---

## Benefits We Gained

### 1. Production-Like Local Environment

Our staging and production environments use subdomains (`api.example.com`, `app.example.com`). Traefik makes local development match this structure, catching routing issues early.

### 2. OAuth/OIDC Compatibility

OAuth providers (Microsoft Entra ID, Auth0, Keycloak) use absolute redirect URIs. With Traefik:

```javascript
// Frontend OAuth config
redirectUri: 'http://app.gymnastics.localhost/auth/callback'

// Backend CORS config
AllowedOrigins: ['http://app.gymnastics.localhost']
```

No path stripping middleware required. Redirects work exactly like production.

### 3. Zero Port Conflicts

All services accessible on port 80. No more "port 3000 is already in use" errors when switching between projects.

### 4. Service Discovery

Adding a new service? Just add Docker labels:

```yaml
new-service:
  image: myservice:latest
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.newservice.rule=Host(`newservice.gymnastics.localhost`)"
    - "traefik.http.services.newservice.loadbalancer.server.port=8080"
  networks:
    - gymnastics-network
```

Traefik auto-discovers it. No config file editing.

### 5. Real-Time Routing Dashboard

The Traefik dashboard shows:
- Active routers and their rules
- Backend services and health status
- Request metrics and response times
- Middleware chains

Perfect for debugging routing issues.

---

## Gotchas and Solutions

### Gotcha #1: Traefik v3.2 Docker Socket Errors on macOS

**Problem:** We initially used `traefik:v3.2` but encountered silent failures on macOS (Apple Silicon):

```
Error response from daemon:
```

Traefik logs showed continuous retry attempts to connect to Docker socket.

**Root Cause:** Compatibility issue between Traefik v3.2 and Docker Desktop for Mac (arm64).

**Solution:** Downgrade to `traefik:v2.11`:

```yaml
services:
  traefik:
    image: traefik:v2.11  # Not v3.2
```

Traefik v2.11 is stable, well-documented, and works flawlessly on all platforms.

### Gotcha #2: Port 80 Already in Use

**Problem:** macOS sometimes runs a service on port 80 (Apache, nginx, AirPlay Receiver).

**Solution:** Make port configurable via environment variable:

```yaml
ports:
  - "${TRAEFIK_HTTP_PORT:-80}:80"
```

If port 80 is taken:

```bash
# .env
TRAEFIK_HTTP_PORT=8080
```

Then access services with port suffix: `http://app.gymnastics.localhost:8080`

### Gotcha #3: Services Not Appearing in Dashboard

**Problem:** Service not showing up in Traefik dashboard after adding labels.

**Checklist:**
1. Is `traefik.enable=true` set?
2. Is the service on the `gymnastics-network`?
3. Did you restart the service after adding labels?
4. Is the service container actually running? (`docker ps`)
5. Check Traefik logs: `docker logs gymnastics-traefik`

**Common mistake:** Forgetting to add the network:

```yaml
services:
  myservice:
    labels:
      - "traefik.enable=true"
      # ... other labels
    networks:
      - gymnastics-network  # DON'T FORGET THIS
```

### Gotcha #4: CORS Errors After Switching to Traefik

**Problem:** Frontend gets CORS errors after switching from `localhost:5001` to `api.gymnastics.localhost`.

**Solution:** Update backend CORS configuration to allow new origins:

```csharp
// ASP.NET Core
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontends", policy =>
    {
        policy.WithOrigins(
            "http://app.gymnastics.localhost",
            "http://admin.gymnastics.localhost"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});
```

### Gotcha #5: Old Containers Running

**Problem:** Traefik dashboard not accessible, services returning 502 Bad Gateway.

**Root Cause:** Old containers from before Traefik was added are still running without proper labels/network.

**Solution:**

```bash
docker compose down -v  # Stop and remove ALL containers and volumes
docker compose up -d    # Fresh start
```

### Gotcha #6: Browser Doesn't Resolve `.localhost` Subdomains

**Problem:** Some corporate networks or older browsers don't auto-resolve `*.localhost`.

**Symptoms:**
- `api.gymnastics.localhost` → DNS error
- `localhost:80` works fine

**Solution:** Add entries to `/etc/hosts`:

```bash
# /etc/hosts
127.0.0.1 api.gymnastics.localhost
127.0.0.1 app.gymnastics.localhost
127.0.0.1 admin.gymnastics.localhost
127.0.0.1 grafana.gymnastics.localhost
127.0.0.1 traefik.gymnastics.localhost
```

**Alternative:** Use a different TLD that you control via DNS (`.local`, `.dev` with local DNS server).

### Gotcha #7: Development vs Production Configuration

**Problem:** Some Traefik settings are dev-only and unsafe for production.

**Dev-only settings to disable in production:**
- `api.insecure: true` — Use secure dashboard with authentication
- `log.level: DEBUG` — Use INFO or WARN in production
- Port 8080 exposed — Dashboard should be internal or secured

**Production considerations:**
- Enable HTTPS with automatic Let's Encrypt
- Use Docker Swarm or Kubernetes provider instead of Docker
- Add rate limiting and circuit breakers
- Enable access logs for audit trails
- Use Traefik Pilot for monitoring (optional)

---

## Advanced: Multiple Environments

For projects with multiple environments (dev, staging), use different base domains:

```yaml
# docker-compose.dev.yml
labels:
  - "traefik.http.routers.api.rule=Host(`api.gymnastics.localhost`)"

# docker-compose.staging.yml
labels:
  - "traefik.http.routers.api.rule=Host(`api.staging.example.com`)"
```

Or use path-based routing with prefixes:

```yaml
labels:
  - "traefik.http.routers.api.rule=Host(`localhost`) && PathPrefix(`/api`)"
  - "traefik.http.middlewares.api-stripprefix.stripprefix.prefixes=/api"
  - "traefik.http.routers.api.middlewares=api-stripprefix"
```

**Recommendation:** Subdomain-based routing is cleaner and more production-like.

---

## Performance Considerations

**Does Traefik add latency?**

Minimal. In our testing:
- Without Traefik: ~5ms response time
- With Traefik: ~7ms response time
- Overhead: ~2ms per request

The 2ms overhead is negligible for local development. In production with HTTP/2 and connection pooling, Traefik often improves performance through smart load balancing.

**Resource usage:**

- Traefik container: ~50MB RAM
- CPU: <1% idle, ~5% under load

Traefik is lightweight and designed for efficiency.

---

## Troubleshooting Commands

```bash
# View Traefik logs
docker logs -f gymnastics-traefik

# Check if service is on correct network
docker inspect <container-name> | grep -A 10 Networks

# Test routing from inside Docker network
docker run --rm --network gymnastics-network curlimages/curl:latest \
  curl -v http://api:8080/health

# Verify Traefik can see Docker socket
docker exec gymnastics-traefik ls -la /var/run/docker.sock

# Check Traefik configuration
docker exec gymnastics-traefik cat /etc/traefik/traefik.yml

# View all Traefik routes (JSON)
curl http://localhost:8080/api/http/routers | jq
```

---

## Migration Checklist

Switching an existing project to Traefik? Follow this checklist:

- [ ] Create `docker/traefik/traefik.yml` configuration
- [ ] Add Traefik service to `docker-compose.yml`
- [ ] Add `gymnastics-network` network definition
- [ ] Add network to all services
- [ ] Add Traefik labels to all web services
- [ ] Update all environment variables with new URLs
- [ ] Update frontend API client configurations
- [ ] Update backend CORS allowed origins
- [ ] Update OAuth redirect URIs
- [ ] Test each service individually
- [ ] Update README and documentation
- [ ] Update developer onboarding docs
- [ ] Test authentication flow end-to-end
- [ ] Verify API requests from frontend work
- [ ] Check observability dashboards load
- [ ] Commit changes with clear message

---

## Alternatives Considered

### nginx + manual configuration

**Pros:** Mature, well-documented, flexible
**Cons:** Manual config files, no auto-discovery, requires reload on changes

### Caddy

**Pros:** Automatic HTTPS, simple config
**Cons:** Less dynamic than Traefik, fewer provider integrations

### Envoy

**Pros:** Feature-rich, used in production (Istio)
**Cons:** Overkill for local dev, steep learning curve

**Why we chose Traefik:** Auto-discovery via Docker labels, dynamic configuration updates, excellent dashboard, and perfect fit for Docker Compose local dev.

---

## Conclusion

Implementing Traefik transformed our local development experience:

- **Before:** `localhost:3001`, `localhost:5001`, `localhost:8080` — port chaos
- **After:** `app.gymnastics.localhost`, `api.gymnastics.localhost` — clean URLs

The 2-hour investment in Traefik setup paid off immediately with:
- Faster onboarding (no port memorization)
- OAuth compatibility out of the box
- Production-like routing for realistic testing
- Zero port conflicts across projects

**Bottom line:** If you have 3+ services in Docker Compose, Traefik is worth it.

---

## Resources

- [Traefik v2 Documentation](https://doc.traefik.io/traefik/v2.11/)
- [Docker Provider Guide](https://doc.traefik.io/traefik/providers/docker/)
- [RFC 6761 - .localhost TLD](https://datatracker.ietf.org/doc/html/rfc6761)
- [Traefik Dashboard Guide](https://doc.traefik.io/traefik/operations/dashboard/)
- [Our Implementation (GitHub)](https://github.com/RDCoached/GymnasticsPlatform)

---

## About This Implementation

This blog post documents the real-world implementation of Traefik for the Gymnastics Session Planner platform — a multi-tenant SaaS application with .NET 10 backend, React frontends, and full observability stack (LGTM).

**Tech stack:** Docker Compose, Traefik v2.11, PostgreSQL, Redis, Grafana, Prometheus, Loki, Tempo, CouchDB, Ollama.

**Questions?** Open an issue on our [GitHub repo](https://github.com/RDCoached/GymnasticsPlatform) or reach out to the team.

---

*Happy routing! 🚦*
