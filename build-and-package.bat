@echo off
cd /d "%~dp0"

echo === Step 1: Publishing project ===
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none
if %errorlevel% neq 0 (
    echo Publish failed!
    pause
    exit /b 1
)

echo.
echo === Step 2: Compiling installer ===
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" setup.iss
if %errorlevel% neq 0 (
    echo Installer compilation failed!
    pause
    exit /b 1
)

echo.
echo === Done! ===
echo Installer created: PrayerReminder-Setup.exe
pause
