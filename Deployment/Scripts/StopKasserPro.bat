@echo off
chcp 65001 >nul 2>&1
title KasserPro - Stop Service

:: Check for admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo ============================================
echo    KasserPro POS - Stopping Service
echo ============================================
echo.

sc query KasserProService | find "STOPPED" >nul 2>&1
if %errorlevel% equ 0 (
    echo [OK] Service is already STOPPED.
) else (
    echo Stopping KasserPro service...
    sc stop KasserProService >nul 2>&1
    timeout /t 3 /nobreak >nul
    sc query KasserProService | find "STOPPED" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] Service has been STOPPED successfully.
    ) else (
        echo [..] Service is still stopping, please wait...
        timeout /t 5 /nobreak >nul
        sc query KasserProService | find "STOPPED" >nul 2>&1
        if %errorlevel% equ 0 (
            echo [OK] Service has been STOPPED successfully.
        ) else (
            echo [WARNING] Service may still be stopping.
            echo           Please check Windows Services panel.
        )
    )
)

echo.
echo ============================================
pause
