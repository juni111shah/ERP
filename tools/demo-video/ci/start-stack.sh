#!/usr/bin/env bash
# CI only: starts the API (Development, demo seed) on :5080 against the SQL Server
# service container, fills the demo workspaces and serves the web app on :4200. Leaves both running for the next steps.
set -euo pipefail
cd "$(dirname "$0")/../../.."
ROOT=$PWD

if [ "${SKIP_WEB:-0}" != "1" ]; then (cd frontend && npm ci --no-audit --no-fund); fi

(cd backend && dotnet build src/ERP.Api/ERP.Api.csproj -c Release -nologo -v q)

export ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrate=true Seed__Enabled=true
export Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local
export ASPNETCORE_URLS=http://0.0.0.0:5080 RateLimiting__LoginPerMinute=100000
export ConnectionStrings__Default="Server=localhost,1433;Database=ERP_DemoVideo;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True"
cd backend/src/ERP.Api
nohup dotnet bin/Release/net8.0/ERP.Api.dll > "$ROOT/api.log" 2>&1 &
cd "$ROOT"

for i in $(seq 1 180); do
  if curl -fs http://localhost:5080/health > /dev/null; then echo "API up after ${i}x2 s"; break; fi
  sleep 2
done
curl -fs http://localhost:5080/health > /dev/null || { tail -80 api.log; exit 1; }

# Demo data (the API's own top-up runs at start; these add the rest through the real workflows). A failure is not fatal.
node tools/seed-demo.mjs http://localhost:5080/api/v1 || echo "seed-demo failed"
node docs/demo-data/1-payroll-leave-approvals-settings.mjs || echo "demo-data 1 failed"
node docs/demo-data/2-credit-notes-attachments-idempotency.mjs || echo "demo-data 2 failed"
# The web app, exactly as in development: ng serve on :4200 with /api proxied to the API.
if [ "${SKIP_WEB:-0}" != "1" ]; then
  (cd frontend && nohup npx ng serve --proxy-config proxy.conf.json --port 4200 > "$ROOT/web.log" 2>&1 &)
  for i in $(seq 1 150); do curl -fs -o /dev/null http://localhost:4200/ && break; sleep 2; done
  curl -fs -o /dev/null http://localhost:4200/ || { tail -50 web.log; exit 1; }
  curl -fs -o /dev/null http://localhost:4200/main.js || true   # warm the bundle
fi
echo "Stack ready."
