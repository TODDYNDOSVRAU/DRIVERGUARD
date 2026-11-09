@echo off
title Compilando DriverGuard Single-File Executable (.exe)
echo ========================================================
echo         DRIVERGUARD — GERADOR DE EXECUTAVEL (.EXE)
echo ========================================================
echo.

where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo [ERRO] O .NET 8 SDK nao foi encontrado no PATH do Windows!
    echo Por favor, instale o .NET 8.0 SDK (x64) com suporte a Desktop:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo [1/3] Restaurando dependencias NuGet...
dotnet restore DriverGuard.sln
if %ERRORLEVEL% neq 0 (
    echo [ERRO] Falha ao restaurar pacotes NuGet.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Executando testes unitarios xUnit...
dotnet test tests\DriverGuard.Tests.Unit\DriverGuard.Tests.Unit.csproj -c Release
if %ERRORLEVEL% neq 0 (
    echo [AVISO] Alguns testes falharam, mas prosseguindo com a publicacao.
)

echo.
echo [3/3] Publicando executavel unico portatil (Single-File x64)...
dotnet publish src\DriverGuard.UI\DriverGuard.UI.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o ".\bin\publish"

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo  [SUCESSO] Executavel gerado com exito!
    echo  Caminho: %~dp0bin\publish\DriverGuard.exe
    echo ========================================================
    echo.
    explorer.exe ".\bin\publish"
) else (
    echo.
    echo [ERRO] Falha ao compilar o executavel.
)

pause
