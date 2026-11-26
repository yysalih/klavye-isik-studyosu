@echo off
color 0B
title Monster Abra A5 Klavye Kontrol Paneli
:menu
cls
echo =================================================
echo    MONSTER ABRA A5 V16.7 KLAVYE KONTROL PANELI
echo =================================================
echo Lutfen uygulamak istediginiz modu secin:
echo.
echo
echo 1 - Nefes Alma Modu (Pürüzsüz Renk Gecisleri)
echo 2 - Mavi Tonlu Nefes (Ozel Mavi Tonlu Gecis)
echo 3 - Gokkusagi Gecisi (Yumusak Renk Dongusu)
echo 4 - Renk Degisimi (Hizli Renk Dongusu)
echo 5 - Filas / Yanip Sonme (Pals Efekti)
echo 6 - Kalp Atisi (Kirmizi Renkte Kalp Atisi)
echo.
echo
echo 7 - Isiklari Ac (Varsayilan Sabit Renk)
echo 8 - Isiklari Kapat
echo.
echo 9 - Cikis
echo =================================================
set /p secim="Seciminiz (1-9): "

cd /d D:\indirilenler\CLEVO_KeyboardColour

if "%secim%"=="1" goto mod1
if "%secim%"=="2" goto mod2
if "%secim%"=="3" goto mod3
if "%secim%"=="4" goto mod4
if "%secim%"=="5" goto mod5
if "%secim%"=="6" goto mod6
if "%secim%"=="7" goto mod7
if "%secim%"=="8" goto mod8
if "%secim%"=="9" goto cikis

:mod1
.\CLEVO_KeyboardColour.exe theme breathe
goto menu

:mod2
.\CLEVO_KeyboardColour.exe theme freshbreathe
goto menu

:mod3
.\CLEVO_KeyboardColour.exe theme colourtransform
goto menu

:mod4
.\CLEVO_KeyboardColour.exe theme colourshift
goto menu

:mod5
.\CLEVO_KeyboardColour.exe theme blink
goto menu

:mod6
.\CLEVO_KeyboardColour.exe theme heartbeat
goto menu

:mod7
.\CLEVO_KeyboardColour.exe backlight on
goto menu

:mod8
.\CLEVO_KeyboardColour.exe backlight off
goto menu

:cikis
exit