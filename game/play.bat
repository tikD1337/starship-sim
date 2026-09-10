@echo off
rem Запуск игры: двойной щелчок или play.bat --cockpit --t 5890
setlocal
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
set "GODOT=%LOCALAPPDATA%\Programs\Godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"

cd /d "%~dp0"

if not exist "%GODOT%" (
  echo Godot не найден: %GODOT%
  pause
  exit /b 1
)

echo Сборка...
dotnet build -v quiet --nologo
if errorlevel 1 (
  echo Сборка не прошла.
  pause
  exit /b 1
)

echo Запуск. Клавиши: Tab пульт, F1 подсказка, F2 обзор, F3 графики, F4 развёртка, F5 вход,
echo F6 сменить задание, F7 отказы, 1 орбита, 2 на корпусе, 3 свободная, 4 кабина,
echo стрелки тяга и тангаж, "," и "." крен, T ДМТ, B/N/M отсек, V ступень,
echo [ и ] скорость времени, R полёт заново, Esc выход.
"%GODOT%" --headless --import --path . >nul 2>&1
"%GODOT%" --path . -- %*
