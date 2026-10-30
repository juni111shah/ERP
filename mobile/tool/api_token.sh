#!/usr/bin/env bash
# Prints an access token for the demo admin on a running dev API.   usage: tool/api_token.sh <port> [workspace-code, default demo]
# Example:  T=$(tool/api_token.sh 5091); curl -s http://localhost:5091/api/v1/me -H "Authorization: Bearer $T"
set -euo pipefail
PORT=${1:?port}; WS=${2:-demo}; B="http://localhost:$PORT/api/v1"
LOGIN=$(curl -s -X POST "$B/auth/login" -H 'content-type: application/json' -d '{"email":"admin@demo.local","password":"DevOnly#2026","rememberMe":false}')
SEL=$(echo "$LOGIN" | sed -n 's/.*"selectionToken":"\([^"]*\)".*/\1/p')
# the workspaces array lists {"id":"...","code":"demo",...}; pick the one with the wanted code
ID=$(echo "$LOGIN" | grep -o '"id":"[^"]*","code":"'"$WS"'"' | sed 's/"id":"\([^"]*\)".*/\1/' | head -1)
curl -s -X POST "$B/auth/select-workspace" -H 'content-type: application/json' -d "{\"selectionToken\":\"$SEL\",\"workspaceId\":\"$ID\"}" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p'
