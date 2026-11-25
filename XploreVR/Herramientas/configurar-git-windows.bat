@echo off
REM ===================================================================
REM  Configura Git LFS y Smart Merge de Unity en ESTE repositorio.
REM  Ejecutadlo UNA vez cada uno, en vuestro ordenador, despues de clonar.
REM ===================================================================
cd /d "%~dp0.."
echo.
echo [1/2] Activando Git LFS...
git lfs install
if errorlevel 1 (
  echo   No se encontro git-lfs. Instalad Git para Windows ^(trae LFS^) desde https://git-scm.com
  pause
  exit /b 1
)

echo [2/2] Buscando UnityYAMLMerge ^(Smart Merge^)...
set MERGE=
for /d %%V in ("C:\Program Files\Unity\Hub\Editor\*") do (
  if exist "%%V\Editor\Data\Tools\UnityYAMLMerge.exe" set MERGE=%%V\Editor\Data\Tools\UnityYAMLMerge.exe
)
if "%MERGE%"=="" (
  echo   No encuentro UnityYAMLMerge.exe. Unity esta instalado con Unity Hub en la ruta por defecto?
  echo   Buscadlo dentro de la carpeta de Unity: Editor\Data\Tools\UnityYAMLMerge.exe
  pause
  exit /b 1
)
set MERGE=%MERGE:\=/%
git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config merge.unityyamlmerge.driver "'%MERGE%' merge -p %%O %%B %%A %%A"
git config merge.unityyamlmerge.recursive binary
echo.
echo Listo. Usando: %MERGE%
pause
