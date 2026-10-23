#!/usr/bin/env bash
# One-command local run: API (SQLite, seeded demo data) on :5080 and the Angular app on :4200.
# Needs: .NET 8 SDK and Node 20+. Open http://localhost:4200 and sign in with:
#   admin@demo.local / DevOnly#2026   (general workspace)
#   owner@demo.local / DevOnly#2026   (pick workspace "demo-re" for Real Estate)
set -euo pipefail
cd "$(dirname "$0")"

export ASPNETCORE_ENVIRONMENT=Development
export Database__Provider=Sqlite Database__AutoMigrate=true
export ConnectionStrings__Default="Data Source=$PWD/erp-dev.db"
export Seed__Enabled=true Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local
export ASPNETCORE_URLS=http://localhost:5080

(cd backend/src/ERP.Api && dotnet run --no-launch-profile) &
API=$!
trap 'kill $API 2>/dev/null' EXIT

cd frontend
[ -d node_modules ] || npm install
npx ng serve --proxy-config proxy.conf.json --port 4200
