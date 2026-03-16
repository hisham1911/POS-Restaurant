@echo off
chcp 65001 >nul 2>&1
title KasserPro - Start Service

:: Check for admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================
echo    KasserPro POS - Starting Service
echo ============================================
echo.

sc start KasserProService >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] KasserPro service is starting...
    timeout /t 3 /nobreak >nul
    sc query KasserProService | find "RUNNING" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] Service is now RUNNING.
        echo.
        echo You can access KasserPro POS at:
        echo    http://localhost:5243
    ) else (
        echo [..] Service is still starting, please wait...
    )
) else (
    sc query KasserProService | find "RUNNING" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] Service is already RUNNING.
    ) else (
        echo [ERROR] Failed to start the service.
        echo         Please check Windows Event Log for details.
    )
)

echo.
echo ============================================
pause
