# [NOMBRE DEL BOT / ESTRATEGIA] - Plantilla de Requerimientos (RSI + EMA)

> Basado en el patrón verificado de `bots/EURUSD_RSI2_PULLBACK` (ver su `strategy.md` para un caso completo ya implementado). Esta plantilla generaliza ese patrón para spec-ear bots nuevos que combinen RSI + EMAs.

## 1. Contexto y Objetivo
* **Instrumento / Activo:** [Ej. EURUSD, GBPUSD, USDJPY — ver sección 6 antes de elegir un activo distinto a uno ya validado].
* **Temporalidad (TF):** [Ej. M15, H1].
* **Objetivo Principal:** [Definir cuál de los dos patrones de RSI+EMA es — no son intercambiables, ver nota abajo]:
  * **(A) Continuación de tendencia vía pullback** (patrón de `EURUSD_RSI2_PULLBACK`): EMAs largas definen la tendencia, RSI corto (periodo bajo, ej. 2) detecta el retroceso puntual *dentro* de esa tendencia, precio confirma con ruptura.
  * **(B) Reversión pura** (RSI-2 clásico de Larry Connors, sin filtro EMA de tendencia, o con EMA solo como filtro de "no ir contra tendencia mayor"): apuesta a que el extremo de RSI revierte, no a que continúa.
  * No mezclar ambos objetivos en el mismo bot — el patrón (A) puede rechazar señales que (B) tomaría como reversión, y viceversa.

## 2. Indicadores y Variables Técnicas
* **RSI:** Periodo = `2` (mean-reversion rápido) o `14` (más tradicional/menos ruidoso). Umbrales típicos:
  * Periodo 2 → Sobreventa `10`, Sobrecompra `90` (extremos, pensado para ruido alto).
  * Periodo 14 → Sobreventa `30`, Sobrecompra `70` (extremos clásicos).
* **EMAs (roles separados, no una sola EMA):**
  * EMA corta (ej. `20`) — zona de pullback/retroceso donde se arma el setup.
  * EMA media (ej. `50`) — confirma tendencia junto con la EMA larga y su pendiente.
  * EMA larga (ej. `200`) — filtro de tendencia macro (solo operar a favor de esta).
  * **Definición de tendencia:** `close` vs EMA larga + EMA media vs EMA larga + pendiente de la EMA media en las últimas N velas (evita filtrar solo por cruce estático, que da falsos positivos en mercados laterales).
* **ATR:** periodo `14` (Exponential recomendado). Dos usos, no uno solo:
  * Tolerancia de la "zona de pullback" (ej. `0.5 × ATR` alrededor de la EMA corta).
  * Filtro de volatilidad válida: banda `[ATR mínimo, ATR máximo]` en pips — fuera de esa banda, no operar (ni muy plano ni muy explosivo).

## 3. Lógica de Entrada (Reglas de Apertura)
Recomendado: **máquina de estados de 2 fases** (armar → confirmar), no entrada directa en la vela que detecta la condición — reduce falsos positivos de RSI extremo que se revierten solo por una vela.

* **Fase 1 — Armado (Buy):**
  * Tendencia alcista vigente (ver definición en sección 2).
  * Precio toca/se aproxima a la EMA de pullback (dentro de la tolerancia ATR).
  * RSI en zona de sobreventa.
* **Fase 2 — Confirmación (dentro de N velas, ej. 3):**
  * Tendencia sigue vigente (si se invalida, resetear el armado, no forzar la entrada).
  * Volatilidad sigue dentro de banda ATR válida.
  * (Opcional pero recomendado) RSI debe haber salido de la zona extrema antes de confirmar — evita entrar mientras el RSI sigue "cayendo" el cuchillo.
  * Confirmación de precio: vela con cuerpo direccional que rompe el extremo de la vela anterior (no solo "RSI cruzó el umbral").
* **Condición de Venta (Sell):** simétrica.
* Solo una posición simultánea (validar count antes de abrir).
* Setup debe expirar si no confirma dentro de la ventana de N velas — no dejarlo "armado" indefinidamente.

## 4. Gestión de Riesgo y Salida
* **Stop Loss / Take Profit:** preferir **múltiplo de ATR** (ej. SL = 1×ATR, TP = 1.5×ATR) en vez de pips fijos, especialmente si el filtro de volatilidad admite una banda amplia (ej. 3-18 pips) — un SL fijo se vuelve demasiado ajustado en volatilidad alta y demasiado holgado en volatilidad baja. [Ver hallazgo #1 en `bots/EURUSD_RSI2_PULLBACK/strategy.md` — ese bot usa pips fijos pese a admitir 6x de rango de ATR].
* **Time Stop:** cierre forzado tras N velas si no se activó SL/TP — evita posiciones "muertas" que ni ganan ni pierden mientras ocupan el único slot permitido.
* **Filtro de Spread:** definir un máximo de spread en pips para la ejecución puntual (no para el armado del setup) — relevante sobre todo si el activo no es un major FX de spread ajustado.
* **Protección de Capital (circuit breaker global):** nivel de equity de corte como % del capital de referencia; al cruzarlo, cerrar todo y detener el bot. Considerar además (no asumir cubierto solo con esto):
  * Límite de pérdida diaria.
  * Lockout tras N pérdidas consecutivas.
  * Filtro de sesión horaria si el activo tiene liquidez muy distinta por sesión (ej. cruces con yen, XAU).

## 5. Requerimientos de Código (cTrader / C#)
* **Estructura Esperada:** cBot en `cAlgo.Robots`, `OnBarClosed` para la lógica de señal (no `OnTick`, salvo para protección de capital que sí debe chequearse en cada tick), parámetros vía `[Parameter]` agrupados por sección (RSI / Trend / Pullback / Volatility / Risk).
* **Control de Órdenes:** validar `Positions.Count(label) == 0` antes de abrir; usar una `label` única por instancia si se planea correr más de una variante sobre el mismo símbolo (evita que `Positions.FindAll` las trate como una sola entidad).
* **Logging:** `Print()` no persiste fuera de la sesión de cTrader — si se necesita trazabilidad histórica de operaciones más allá del journal de la plataforma, exportar a archivo o usar el historial de `Positions`/`Deals` vía MCP después de la sesión.

## 6. Cómo validar el activo antes de spec-ear el bot

No asumir que un patrón RSI+EMA que funciona en un par se traslada a otro sin verificar. Antes de fijar SL/TP/ATR band para un activo nuevo:

1. **Spread real del bróker** (vía MCP `get_symbol_details` o Market Watch) — debe ser consistentemente menor al filtro de spread que se piensa usar.
2. **Rango M15/H1 típico fuera de eventos de noticias** (vía MCP `get_trendbars`, o backtest) — para calibrar la banda de ATR mínimo/máximo, no copiar los valores de otro par sin ajustar (el `pipSize` y la escala de volatilidad cambian drásticamente entre FX majors, cruces con yen, metales e índices).
3. **Carácter del activo:** ¿tiende a formar tendencias limpias con pullback (bueno para el patrón A), o es más lateral/rango (mejor para el patrón B, o para otro indicador)? Un filtro EMA50/200 penaliza activos que no tienden.
4. **Backtest dedicado en el simulador de cTrader** con el histórico propio del activo — la compatibilidad de spread/ATR es un filtro de descarte rápido, no una validación de rentabilidad.

Ver la tabla de ejemplo (EURUSD vs GBPUSD/USDJPY/GBPJPY/AUDUSD/XAUUSD) en `bots/EURUSD_RSI2_PULLBACK/strategy.md` sección 7 como referencia de cómo documentar este análisis.
