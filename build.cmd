@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title YTM Dock 빌드

echo.
echo  ============================================
echo   YTM Dock exe 만들기 (더블클릭 한 번이면 끝)
echo  ============================================
echo.
echo  실행 중인 YTM Dock 을 끕니다. (켜져 있으면 exe 가 잠겨서 빌드가 실패해요)
taskkill /im YtmDock.exe /f >nul 2>&1
taskkill /im YtmDock-self-contained.exe /f >nul 2>&1
timeout /t 1 /nobreak >nul

echo.
echo  [1/2] 작은 exe 만드는 중... (.NET 10 Desktop Runtime 필요한 버전)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
if errorlevel 1 goto :fail

echo.
echo  [2/2] 런타임 포함 exe 만드는 중... (1~2분 걸려요)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -SelfContained
if errorlevel 1 goto :fail

rem GitHub Release 에 그대로 올릴 이름으로 한 폴더에 모은다
set "REL=%~dp0publish\release"
if not exist "%REL%" mkdir "%REL%"
copy /y "publish\framework-dependent\YtmDock.exe" "%REL%\YtmDock.exe" >nul || goto :fail
copy /y "publish\self-contained\YtmDock.exe" "%REL%\YtmDock-self-contained.exe" >nul || goto :fail

rem 예전에 self-contained 폴더 안에서 이름을 바꿔 쓰던 exe 가 있으면 그것도 새 것으로 덮어 둔다
rem (로그인 시 자동 실행이 그 경로를 가리키고 있을 수 있어서)
if exist "publish\self-contained\YtmDock-self-contained.exe" copy /y "publish\self-contained\YtmDock.exe" "publish\self-contained\YtmDock-self-contained.exe" >nul

echo.
echo  완료! publish\release 폴더가 열립니다.
echo    YtmDock.exe                 - 작은 버전
echo    YtmDock-self-contained.exe  - 런타임 포함 버전
echo  이 두 파일을 GitHub Release 에 올리면 됩니다.
explorer "%REL%"
echo.
pause
exit /b 0

:fail
echo.
echo  [실패] 위 빨간 글씨를 캡처해서 Claude 에게 보여 주세요.
pause
exit /b 1
