@echo off
chcp 65001 >nul
title OFK GEX Loader - NQ + ES
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
set FALHAS=

rem Portavel: roda a partir da pasta onde este .cmd esta, em qualquer drive/caminho
cd /d "%~dp0OFK_GEX_Pipeline" || (echo ERRO - pasta do pipeline nao encontrada. & goto :fim)

echo === 1/3 Testes do pipeline ===
python -m pytest -q
if errorlevel 1 (
  echo.
  echo Testes falharam. A coleta NAO foi executada.
  goto :fim
)

echo.
echo === 2/3 Coleta NQ + briefing (2 a 3 min) ===
python run_morning_NQ.py
if errorlevel 1 set FALHAS=%FALHAS% NQ

echo.
echo === 3/3 Coleta ES + briefing (2 a 3 min) ===
python run_morning_ES.py
if errorlevel 1 set FALHAS=%FALHAS% ES

echo.
if defined FALHAS (
  echo ERRO na coleta de:%FALHAS% - veja as mensagens acima.
) else (
  echo Concluido. O ATAS ja pode ler full_levels_NQ.json e full_levels_ES.json.
)

:fim
echo.
pause
