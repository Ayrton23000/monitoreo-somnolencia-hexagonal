@echo off
taskkill /FI "WINDOWTITLE eq Terminal 1 - IA Ocular gRPC (Python)*" /T /F
taskkill /FI "WINDOWTITLE eq Terminal 2 - Backend Hexagonal (.NET)*" /T /F
docker compose stop
exit