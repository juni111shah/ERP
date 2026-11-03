#!/usr/bin/env bash
# Stops the API listening on <port> (only that process).   usage: tool/stop_api.sh <port>
PORT=${1:?port}
PID=$(netstat -ano | grep ":$PORT " | grep LISTENING | awk '{print $5}' | head -1)
[ -n "$PID" ] && taskkill //PID "$PID" //F || echo "nothing listening on $PORT"
