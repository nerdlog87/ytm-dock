@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title YTM Dock 테스트 실행
rem 코드 고친 뒤 바로 확인용. 빌드한 뒤 이 창과 '분리해서' 실행한다.
rem (dotnet run 은 이 창의 자식이라 창을 닫으면 독도 같이 꺼졌다)
echo  실행 중인 YTM Dock 을 끄고 새 코드로 빌드합니다...
taskkill /im YtmDock.exe /f >nul 2>&1
taskkill /im YtmDock-self-contained.exe /f >nul 2>&1
timeout /t 1 /nobreak >nul

dotnet build "%~dp0src\YtmDock\YtmDock.csproj" -c Debug -nologo -v q
if errorlevel 1 (
  echo.
  echo  [실패] 위 빨간 글씨를 캡처해서 Claude 에게 보여 주세요.
  pause
  exit /b 1
)

start "" "%~dp0src\YtmDock\bin\Debug\net10.0-windows\YtmDock.exe"
echo  실행했어요. 이 창은 3초 뒤 저절로 닫혀요. (닫혀도 독은 계속 켜져 있어요)
timeout /t 3 >nul
