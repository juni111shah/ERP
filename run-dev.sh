#!/usr/bin/env bash
# One-command local run: API (SQL Server, seeded demo data) on :5080 and the Angular app on :4200.
# Needs: .NET 8 SDK, Node 20+ and SQL Server. The API uses ConnectionStrings__Default if set, otherwise
# appsettings.Development.json (localhost, Windows authentication, database ERP_Dev). For the SQL Server container
# (deploy/docker-compose.yml) export ConnectionStrings__Default="Server=localhost,1433;Database=ERP_Dev;User Id=sa;Password=...;TrustServerCertificate=True".
# Open http://localhost:4200 and sign in with:
#   admin@demo.local / DevOnly#2026   (general workspace)
#   owner@demo.local / DevOnly#2026   (pick workspace "demo-re" for Real Estate)
set -euo pipefail
cd "$(dirname "$0")"

export ASPNETCORE_ENVIRONMENT=Development
export Database__AutoMigrate=true
export Seed__Enabled=true Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local
export ASPNETCORE_URLS=http://localhost:5080

(cd backend/src/ERP.Api && dotnet run --no-launch-profile) &
API=$!
trap 'kill $API 2>/dev/null' EXIT

# Fill the demo workspace with sample data in the background once the API is up (skips if already seeded; SEED_DEMO=0 turns it off).
if [ "${SEED_DEMO:-1}" = "1" ]; then
  (
    for _ in $(seq 1 90); do curl -s -o /dev/null http://localhost:5080/api/v1/auth/login -X POST -H 'content-type: application/json' -d '{}' && break; sleep 2; done
    node tools/seed-demo.mjs http://localhost:5080/api/v1 || echo "Demo seeding failed (the app still works)."
  ) &
fi

cd frontend
[ -d node_modules ] || npm install
npx ng serve --proxy-config proxy.conf.json --port 4200
