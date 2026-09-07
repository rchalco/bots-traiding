# EURUSD RSI2 Pullback (V3 M15) - Requerimientos

> Fuente verificada: `...\Documentos\cAlgo\Sources\Robots\EURUSD_RSI2_PULLBACK\EURUSD_RSI2_PULLBACK\EURUSD_RSI2_PULLBACK.cs`
> (clase `EurUsdPullbackV2M15` — el nombre de clase no cambió para no romper el registro existente en cTrader Automate, aunque el label por defecto y los prints ya dicen V3). Este documento describe el bot **tal como está implementado**, no un ideal de diseño.
>
> **V3 (esta versión):** agrega SL/TP dinámico basado en ATR y filtro de sesión horaria, en respuesta directa a los hallazgos del backtest de la sección 8. Ambos cambios son **parametrizables y activados por default**, pero **no han sido backtesteados todavía** — antes de operar en real, correr el backtester de cTrader sobre un rango largo (6-12+ meses) con los nuevos parámetros.

## 1. Contexto y Objetivo
* **Instrumento / Activo:** EURUSD (el bot valida `SymbolName.StartsWith("EURUSD")` y emite warning si no coincide).
* **Temporalidad (TF):** M15 (el bot valida `TimeFrame == TimeFrame.Minute15` y emite warning si no coincide).
* **Objetivo Principal:** Continuación de tendencia vía pullback — no es un RSI(2) de reversión pura (Larry Connors clásico); el RSI(2) se usa como gatillo de entrada *dentro* de una tendencia ya confirmada por EMAs, filtrado por ATR y confirmado por ruptura de precio.

## 2. Indicadores y Variables Técnicas
* **RSI:** Periodo = `2` (parametrizable 2-20). Sobreventa = `10`, Sobrecompra = `90`.
* **EMAs (triple, roles distintos):**
  * EMA `20` (Pullback) — zona de retroceso donde se arma el setup.
  * EMA `50` (Filter) — define tendencia junto con EMA200 y su pendiente.
  * EMA `200` (Trend) — filtro de tendencia macro.
* **ATR:** Periodo `14` (Exponential). Usado para (a) tolerancia de la zona de pullback y (b) filtro de volatilidad válida.
* **Slope Bars:** `3` velas atrás para confirmar pendiente de EMA50.

## 3. Lógica de Entrada (Reglas de Apertura)

Máquina de estados: `None → LongArmed/ShortArmed → entrada o reseteo`. No hay entrada directa en la misma vela que detecta la condición; el setup se "arma" y se confirma en hasta `MaxSetupBars = 3` velas siguientes.

* **Condición de Tendencia (Bullish):** `close > EMA200` y `EMA50 > EMA200` y `EMA50 > EMA50[hace 3 velas]` (pendiente ascendente). Simétrico para Bearish.
* **Armado Long:** Tendencia alcista + precio toca/se aproxima a EMA20 (dentro de `tolerancia = ATR × PullbackAtrDistance(0.5)`, o la vela cruza la EMA20) + estructura válida (`close >= EMA50 - tolerancia`) + `RSI <= 10`.
* **Armado Short:** simétrico con tendencia bajista, `RSI >= 90`.
* **Confirmación (dentro de las siguientes ≤3 velas):**
  * Tendencia debe seguir vigente (si no, reset "Bullish/Bearish trend invalidated").
  * Volatilidad debe seguir válida (si no, reset "Invalid volatility").
  * Si `RequireRsiExit = true` (default): RSI debe haber salido de la zona extrema (Long: `RSI > 10`; Short: `RSI < 90`).
  * Confirmación de precio: vela con cuerpo direccional (`close > open` para long) que además rompe el extremo de la vela anterior (`close > high[1]` para long; simétrico para short).
  * Spread debe estar dentro del máximo permitido (si no, se rechaza la entrada puntual, sin resetear el setup).
* **Una sola posición simultánea** (por `Positions.FindAll(BotLabel, SymbolName)`).
* Setup expira y se resetea si no confirma dentro de `MaxSetupBars`.

## 4. Gestión de Riesgo y Salida
* **Stop Loss / Take Profit:** dos modos, seleccionados por `UseAtrBasedRisk` (default `true`):
  * **ATR-based (default):** `SL = ATR_pips × SlAtrMultiple (1.0)`, `TP = ATR_pips × TpAtrMultiple (1.5)`, calculado al momento de abrir la posición — mantiene el R:R 1:1.5 pero escala con la volatilidad real de entrada en vez de una distancia fija dentro de una banda de ATR de 6x (3-18 pips).
  * **Fijo (legacy, `UseAtrBasedRisk = false`):** SL = `StopLossPips (8.0)`, TP = `TakeProfitPips (12.0)` — el comportamiento original de V2, disponible para comparar en backtest A/B.
* **Time Stop:** cierre forzado tras `MaxBarsInTrade = 8` velas M15 (2 horas) si no se activó SL/TP antes. `MaxBarsInTrade = 0` desactiva este stop. (Sin cambios en V3 — ver nota en sección 8 sobre por qué no se tocó.)
* **Filtro de Volatilidad (entrada):** ATR en pips debe estar entre `MinimumAtrPips = 3.0` y `MaximumAtrPips = 18.0`, verificado tanto al armar el setup como en cada vela de confirmación.
* **Filtro de Sesión Horaria (entrada, nuevo en V3):** `UseSessionFilter` (default `true`) bloquea el armado de **setups nuevos** fuera de la ventana `SessionStartHour`-`SessionEndHour` (default `07:00`-`22:00` UTC, excluye la sesión asiática de baja liquidez). Un setup ya armado antes de la ventana puede seguir confirmando o expirando con normalidad — el filtro no cierra ni resetea setups en curso, solo gatea la detección de oportunidades nuevas (mismo patrón que el filtro de volatilidad).
* **Filtro de Spread (entrada):** `MaximumSpreadPips = 1.2` — rechaza la ejecución puntual si el spread excede este umbral (el setup no se resetea, solo se pierde esa vela de confirmación).
* **Protección de Capital (circuit breaker global):** único stop automático de todo el bot. `ReferenceCapital` (default 100, o equity al arrancar si es 0) × `(1 - MaximumCapitalLossPercent/100)` (default 50%) define el nivel de equity de corte. Al cruzarlo: cierra todas las posiciones del bot y llama `Stop()`. **Sigue sin haber límite de pérdida diaria ni lockout tras rachas de pérdidas** — ver hallazgo #3 en sección 6 (no resuelto en V3).

## 5. Requerimientos de Código (cTrader / C#)
* **Estructura:** cBot en `cAlgo.Robots`, clase `EurUsdPullbackV2M15`, usa `OnStart` / `OnTick` (solo protección de capital) / `OnBarClosed` (toda la lógica de señal) / `OnStop`.
* **Control de Órdenes:** valida `GetBotPosition() == null` antes de abrir; `ExecuteMarketOrder` sin control explícito de slippage/desviación máxima.
* **Logging:** vía `Print()` a la consola/journal de cTrader — no persiste fuera de la sesión de la plataforma (sin archivo de log externo ni exportación de operaciones).

## 6. Observaciones de la revisión técnica

1. ~~SL/TP fijos vs. banda de ATR amplia~~ — **Resuelto en V3** (`UseAtrBasedRisk`, ver sección 4 y 8). El modo fijo se mantiene disponible como opción para comparar en backtest A/B.
2. ~~Sin filtro de sesión horaria~~ — **Resuelto en V3** (`UseSessionFilter`, ver sección 4 y 8).
3. **Protección de capital sigue siendo el único circuito de seguridad:** no hay límite de pérdida diaria ni lockout tras N pérdidas consecutivas. Para uso con capital real, considerar un stop diario adicional (trazabilidad/resiliencia operativa). **No abordado en V3** — el backtest de sección 8 no mostró rachas de pérdidas consecutivas lo bastante largas como para priorizarlo sobre los otros dos hallazgos, pero sigue siendo una brecha real.
4. **Label único por símbolo:** el label por defecto cambió a `EURUSD_PULLBACK_V3_M15` en este release; si se despliega más de una instancia sobre el mismo símbolo con el mismo label, `Positions.FindAll` las trataría como una sola entidad — usar labels distintas por instancia si se corre en paralelo.
5. **Sin control de slippage/deviation** en `ExecuteMarketOrder` — en la práctica, para EURUSD con spreads sub-pip esto es de bajo riesgo, pero es una omisión a tener presente si se reutiliza la misma lógica en un activo más volátil.
6. **Los cambios de V3 no están validados por backtest todavía.** Se implementaron porque el diagnóstico de sección 8 los señala como las dos hipótesis más baratas de descartar, no porque ya se haya confirmado que mejoran el resultado — correr backtest antes de asumir que V3 es mejor que V2.

## 7. En qué activos funciona esta estrategia (evidencia de mercado, no backtest)

Se consultó el MCP de cTrader (cuenta Pepperstone conectada) para contrastar los parámetros calibrados del bot (spread máx. 1.2 pips, banda de ATR 3-18 pips en M15) contra condiciones reales de mercado (2026-08-27 a 2026-09-02, ~480 velas M15 por símbolo). **Esto no es un backtest de la estrategia — es una verificación de compatibilidad estructural** (spread/volatilidad del activo vs. los límites que el bot ya trae hardcodeados).

| Símbolo | Spread actual (Pepperstone) | Rango M15 típico (fuera de noticias) | Compatibilidad con el bot tal cual está |
|---|---|---|---|
| **EURUSD** | ~0.0-0.2 pips | ~2-5 pips | Diseño nativo — compatible sin cambios. |
| **GBPUSD** | ~0.2 pips | ~3-6 pips (algo más ancho que EUR) | Estructuralmente compatible; SL/TP fijos (8/12) probablemente subóptimos por mayor volatilidad típica — recalibrar antes de vivo. |
| **USDJPY** | spread no disponible en la consulta (bid/ask null) | ~3-5 pips (con pip=0.01) | Estructuralmente compatible; validar spread real y recalibrar SL/TP en backtest. |
| **GBPJPY** (cruce) | ~0.8 pips | ~5-10 pips, con colas más largas | Roza/excede seguido el techo de ATR (18 pips) → el bot se quedaría fuera de mercado con frecuencia, o requiere ensanchar la banda de ATR y SL/TP. |
| **AUDUSD** | spread no disponible en la consulta | ~2-4 pips (más lateral, tendencias menos limpias) | Compatible en spread/ATR, pero el edge depende de tendencias claras con pullback a EMA — AUDUSD tiende a rangos más comprimidos, penalizando el filtro de tendencia EMA50/200. |
| **XAUUSD** (oro) | ~1.4 "pips" (pip=0.1) pero rango M15 típico ronda **100-150 "pips"** en esa unidad | Escala de volatilidad totalmente distinta | **Incompatible sin reescalar todos los parámetros** (ATR, SL/TP, spread) — por eso existe un bot separado (`XAU_ADX`, ver [../XAU_ADX/strategy.md](../XAU_ADX/strategy.md)) en vez de reusar esta lógica en oro. |

**Conclusión:** la estrategia, tal como está parametrizada (spread ≤1.2 pips, ATR 3-18 pips, SL/TP fijos 8/12 pips), está calibrada específicamente para **EURUSD M15**. Se traslada razonablemente bien —en cuanto a *compatibilidad estructural*— a otros majors de spread ajustado (GBPUSD, USDJPY), siempre que se re-optimicen SL/TP y la banda de ATR contra el histórico propio de cada par antes de operar en real. Cruces con yen (GBPJPY) probablemente necesiten una banda de ATR más ancha. Oro, índices y cripto quedan fuera de alcance sin una reparametrización completa (y en la práctica, sin backtest dedicado por instrumento).

**Antes de operar en real en cualquier activo distinto a EURUSD:** correr el backtester de cTrader (Automate) sobre el histórico del par candidato con los parámetros actuales, y ajustar SL/TP/ATR band según el resultado — esta tabla es un filtro de descarte rápido (qué activos ni vale la pena probar), no una validación de rentabilidad.

## 8. Backtest real (V2, parámetros por defecto) que motivó los cambios de V3

Backtest de cTrader sobre EURUSD M15, 01/07/2026-02/09/2026 (~2 meses), capital inicial 100 EUR, 0.01 lotes, comisión $30/millón (Pepperstone). **27 operaciones — muestra estadísticamente débil, esto es diagnóstico de diseño, no una validación de rentabilidad.**

| Métrica | Valor |
|---|---|
| Resultado neto | -4.60 (100 → 95.40) |
| Win rate | 7/27 = 25.9% |
| Profit factor (todas / long / short) | 0.54 / 0.44 / 0.66 |
| Cierres por TP completo (~12 pips) | 5/27 = 18.5% |
| Cierres por SL completo (~8 pips) | 11/27 = 40.7% |
| Cierres por Time Stop (2h) | 11/27 = 40.7%, casi todos en pérdida chica o breakeven |
| Comisión total | -1.62 (≈35% del resultado negativo total) |

**Hallazgo 1 — el R:R no compensa el win rate real:** con SL/TP fijos (8/12 pips, R:R 1:1.5) el punto de equilibrio matemático es 40% de aciertos (`1/(1+1.5)`). El TP se tocó solo 18.5% de las veces, muy por debajo — de ahí el cambio a SL/TP como múltiplo de ATR (sección 4), buscando que la distancia de salida se ajuste a la volatilidad real de cada entrada en vez de competir contra una banda de ATR 6x más ancha que la distancia fija.

**Hallazgo 2 — patrón horario:** cruzando entradas por hora contra ganadoras/perdedoras, las horas 01, 02, 03, 05, 06, 07, 12, 14, 15 (UTC) tuvieron **0 ganadoras de 13 operaciones**; las 7 ganadoras cayeron todas en horas 08, 10, 11, 13, 20, 21 (aperturas de Londres/NY, cierre de NY). Las horas 01-07 UTC son sesión asiática (baja liquidez para EURUSD) — de ahí el filtro de sesión (sección 4), acotado a esa ventana y no a las horas 12/14/15 (que caen en sesión activa normal y su resultado en 0-de-N probablemente sea ruido de muestra, no señal real).

**Qué no se tocó y por qué:** `MaxBarsInTrade` (time stop de 2h) se dejó igual — el 40.7% de operaciones cerradas por time stop no muestra un patrón claro de "casi ganaban pero se cortaron antes de tiempo" (la mayoría son pérdidas chicas o breakeven, no ganancias interrumpidas), así que alargar la ventana no tiene una hipótesis clara detrás todavía.
