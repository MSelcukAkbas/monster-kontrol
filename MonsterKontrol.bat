@echo off
rem Monster Kontrol: derle ve çalıştır. "temiz" argümanıyla bin/obj silinip sıfırdan derlenir.
chcp 65001 >nul
cd /d "%~dp0"
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

rem Çalışan uygulama dosyaları kilitler; zorla kapatmak fanı güvenli duruma döndürmez.
tasklist /fi "imagename eq MonsterKontrol.exe" | find /i "MonsterKontrol.exe" >nul && (
    echo Monster Kontrol açık. Önce tepsi simgesinden Çıkış yap, sonra tekrar çalıştır.
    pause
    exit /b 1
)

if /i "%~1"=="temiz" (
    for /d /r %%d in (bin obj) do if exist "%%d" rd /s /q "%%d"
)

dotnet build kontrol\MonsterKontrol\MonsterKontrol.csproj -p:Platform=x64 -v q -nologo || (
    echo Derleme başarısız.
    pause
    exit /b 1
)
dotnet build klavye\MonsterKlavye.Mcp\MonsterKlavye.Mcp.csproj -v q -nologo >nul
dotnet build performans\MonsterPerformans.Mcp\MonsterPerformans.Mcp.csproj -v q -nologo >nul

start "" "kontrol\MonsterKontrol\bin\x64\Debug\net10.0-windows\MonsterKontrol.exe"
