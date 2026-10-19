@echo off
title Instalador Automatico - Sistema de Somnolencia v1.1
cd /d "%~dp0"

echo [1/4] Configurando entorno virtual de Python 3.10 e instalando TensorFlow...
cd ai-ocular-service
if exist venv rmdir /s /q venv
py -3.10 -m venv venv
call .\venv\Scripts\activate.bat
python -m pip install --upgrade pip
pip install tensorflow tf-keras pillow "numpy<2.0" grpcio grpcio-tools
python -m grpc_tools.protoc -I../protos --python_out=. --grpc_python_out=. ../protos/ocular_detector.proto
cd ..

echo [2/4] Restaurando paquetes NuGet de la Arquitectura Hexagonal (.NET)...
dotnet restore

echo [3/4] Instalando herramienta Entity Framework Core y aplicando migraciones...
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/Somnolencia.Infrastructure/Somnolencia.Infrastructure.csproj --startup-project src/Somnolencia.WebApi/Somnolencia.WebApi.csproj

echo [4/4] Descargando contenedor Redis en Docker...
docker compose pull

echo Instalacion completada. Ya puedes usar iniciar-sistema.bat
pause