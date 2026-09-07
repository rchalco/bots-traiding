# EURUSD RSI2 Pullback (V3 M15)

cBot de cTrader para EURUSD M15: RSI(2) como gatillo de entrada dentro de una tendencia confirmada por EMA20/50/200, con zona de pullback, filtro de ATR, filtro de spread, filtro de sesión horaria, SL/TP dinámico por ATR, time-stop y protección de capital global. Ver [strategy.md](strategy.md) para el detalle completo de la lógica, las observaciones técnicas, la evidencia del backtest que motivó los cambios de V3 (sección 8) y el análisis de en qué otros activos es estructuralmente compatible (sección 7).

**Novedad V3:** `UseAtrBasedRisk` (SL/TP como múltiplo de ATR, default activado) y `UseSessionFilter` (bloquea nuevos setups fuera de 07:00-22:00 UTC, default activado). Ambos parametrizables y reversibles a comportamiento V2 si se desactivan — **todavía sin validar en backtest**, correr uno largo (6-12+ meses) antes de ir a real.

## Compilar

El proyecto vive en `...\Documentos\cAlgo\Sources\Robots\EURUSD_RSI2_PULLBACK\EURUSD_RSI2_PULLBACK\` (mismo formato SDK-style .NET 6 que `XAU_ADX`).

**Opción A — cTrader Automate:** abrirlo ahí, el robot aparece en la lista → Build (Ctrl+B).

**Opción B — VS Code / terminal:**
```bash
dotnet build "C:\Users\viajero\OneDrive\Documentos\cAlgo\Sources\Robots\EURUSD_RSI2_PULLBACK\EURUSD_RSI2_PULLBACK\EURUSD_RSI2_PULLBACK.csproj"
```
Genera `EURUSD_RSI2_PULLBACK.algo` en `Sources\Robots\`, donde cTrader lo espera encontrar.

Backtestear en el simulador antes de correr en cuenta real. Adjuntar al gráfico de EURUSD en M15 (el bot emite un warning en el log si el símbolo o el timeframe del gráfico no coinciden).

## Otros activos

La estrategia está calibrada para EURUSD M15 (spread máx. 1.2 pips, ATR 3-18 pips, SL/TP fijos 8/12 pips). Antes de correrla en otro par (GBPUSD, USDJPY, etc.), backtestear con el histórico propio del par y re-optimizar SL/TP y la banda de ATR — ver la tabla de compatibilidad en `strategy.md` sección 7. **No usar en oro/índices/cripto sin reescalar todos los parámetros** (la escala de volatilidad es completamente distinta).
