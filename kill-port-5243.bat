@echo off
echo Killing process on port 5243...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5243') do (
    echo Found PID: %%a
    taskkill /F /PID %%a
)
echo Done!
pause
