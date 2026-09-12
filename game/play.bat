@echo off
rem Запуск игры: двойной щелчок или play.bat --screen 3 --theme b --t 101
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

echo Запуск. Клавиши: Tab экраны по кругу, F1 подсказка, F3 телеметрия, F4 развёртка, F5 вход,
echo F6 сменить задание, F7 отказы, 1 сводка, 2 эфир (ещё раз - следующая камера), 3 свободная,
echo стрелки тяга и тангаж, "," и "." крен, T ДМТ, B/N/M отсек, V ступень,
echo [ и ] скорость времени, R полёт заново, Esc выход.
"%GODOT%" --headless --import --path . >nul 2>&1
"%GODOT%" --path . -- %*
