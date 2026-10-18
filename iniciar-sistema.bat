@echo off
title Lanzador Sistema de Monitoreo de Somnolencia
cd /d "%~dp0"

echo Iniciando contenedor Redis en Docker...
docker compose up -d --remove-orphans

echo Abriendo Terminal 1: Servidor gRPC Python con keras_model.h5...
start "Terminal 1 - IA Ocular gRPC (Python)" powershell -NoExit -Command "cd '%~dp0ai-ocular-service'; .\venv\Scripts\Activate.ps1; python grpc_ocular_server.py"

echo Abriendo Terminal 2: Servidor Web Hexagonal (.NET)...
start "Terminal 2 - Backend Hexagonal (.NET)" powershell -NoExit -Command "cd '%~dp0'; dotnet run --project src/Somnolencia.WebApi/Somnolencia.WebApi.csproj --urls 'https://0.0.0.0:5001;http://0.0.0.0:5000'"

echo Esperando 6 segundos para abrir el navegador...
timeout /t 6 /nobreak > nul
start http://localhost:5000
exit