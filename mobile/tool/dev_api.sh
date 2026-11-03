#!/usr/bin/env bash
# Starts the ERP API on a throwaway LocalDB database with the demo seed, for checking the mobile app against the real contract.
#   usage: tool/dev_api.sh <port> <database>      e.g. tool/dev_api.sh 5091 ERP_Mobile_Hr
# Run it in the background. Use your OWN port and database name so parallel work does not collide.
# Stop it with tool/stop_api.sh <port>; remove the database with tool/drop_db.sh <database>. Never point this at ERP_Dev/Staging/Prod.
set -euo pipefail
PORT=${1:?port}; DB=${2:?database}
case "$DB" in ERP_Dev|ERP_Staging|ERP_Prod) echo "refusing to use $DB"; exit 1;; esac
cd "$(dirname "$0")/../../backend/src/ERP.Api"
export ASPNETCORE_ENVIRONMENT=Development Database__AutoMigrate=true Seed__Enabled=true
export Seed__AdminPassword='DevOnly#2026' Seed__AdminEmail=admin@demo.local Seed__MultiWorkspaceEmail=owner@demo.local
export ASPNETCORE_URLS="http://localhost:$PORT"
export ConnectionStrings__Default="Server=(localdb)\MSSQLLocalDB;Database=$DB;Integrated Security=true;TrustServerCertificate=true"
exec dotnet run --no-launch-profile
