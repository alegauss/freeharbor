@echo off
REM ==========================================================================
REM build-msix.cmd - publishes FreeHarbor, then packs it as an MSIX for the
REM Microsoft Store (DD310). Output: dist\FreeHarbor.msix, unsigned, because
REM the Store signs on submission.
REM
REM   build-msix.cmd             the Store package
REM   build-msix.cmd -Sideload   also dist\FreeHarbor-sideload.msix, signed with a
REM                              self-signed certificate, to install on a test
REM                              machine (trust the .cer it prints first)
REM
REM The Inno installer (build-installer.cmd) is untouched by this.
REM ==========================================================================
setlocal

REM This script lives in build\; everything it produces (dist\) goes in the folder above.
cd /d "%~dp0.."

call "%~dp0build.cmd"
if errorlevel 1 (
    echo *** Build failed; no package was produced. ***
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-msix.ps1" %*
if errorlevel 1 (
    echo.
    echo *** The MSIX package could not be built. ***
    exit /b 1
)

endlocal
