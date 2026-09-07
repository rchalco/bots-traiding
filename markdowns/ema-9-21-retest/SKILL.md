---
name: ema-9-21-retest
description: Especifica e implementa bots de retesteo de EMA 9/21 (pullback a la zona de medias en tendencia, intradía) para cTrader/C#. Úsalo cuando el usuario pida un bot, estrategia o spec basado en cruce/retesteo/pullback de EMAs rápidas (9, 21, 8/20, 10/20), "continuación de tendencia con medias móviles", o mencione EMA retest / EMA bounce / pullback a la media en TF bajos (M1-M15).
---

# EMA 9/21 Retest — Pullback de continuación de tendencia

Convierte la estrategia discrecional de "retesteo de EMA 9/21" en una especificación
mecánica y en un cBot de cTrader. La estrategia original es **discrecional y visual**;
la mayor parte del trabajo de este skill es **eliminar la discrecionalidad**, no
transcribirla.

## 1. La estrategia, en su forma original (discrecional)

1. **Tendencia:** el precio se mueve de forma clara y sostenida en una dirección,
   separándose de las EMAs 9 y 21.
2. **Pullback:** el precio corrige y regresa a la zona entre EMA9 y EMA21.
3. **Retesteo + entrada:** la zona actúa como soporte (long) o resistencia (short),
   y una **vela de confirmación** dispara la entrada.
4. **Riesgo:** SL bajo el mínimo del retroceso (long) / sobre el máximo (short).
   TP por R:R fijo, típicamente 1:1.5 o 1:2.

Es un patrón de **continuación**, no de reversión. No mezclarlo con lógica de
reversión a la media (ver `markdowns/rsi ema indicator.md` §1 para la misma
distinción aplicada a RSI+EMA).

```
        precio
          │        ╱‾‾╲          ← impulso: precio se separa de las EMAs
          │      ╱     ╲
          │    ╱        ╲___     ← pullback a la banda [EMA21, EMA9]
          │  ╱          ▓▓▓▓╲╱‾‾ ← vela de confirmación → ENTRADA
          │╱      ┈┈┈┈┈┈EMA9┈┈┈
          │    ┈┈┈┈┈┈┈┈┈EMA21┈┈
          │  ─────────────EMA200────  (filtro macro, añadido)
          └────────────────────────── tiempo
                          ▲ SL: bajo el mínimo del retroceso
```

## 2. Lo que NO está definido en la versión discrecional

**Ninguno de estos puntos puede quedar sin cerrar antes de escribir código.** Cada uno
es una fuente directa de sobreajuste o de un bot que no reproduce lo que el operador
creía estar haciendo:

| Ambigüedad                     | Decisión que hay que tomar                                                                                                |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------- |
| "movimiento claro y sostenido" | Métrica objetiva: `abs(close − EMA21) ≥ k × ATR`, o N velas consecutivas con EMA9 > EMA21, o ADX > umbral.                |
| "vuelve a acercarse o tocar"   | ¿`low` entra en la banda [EMA21, EMA9]? ¿`close` dentro? ¿tolerancia ± j × ATR?                                           |
| "vela de confirmación alcista" | Regla codificable: cuerpo direccional **y** `close > high[1]`. Un martillo o "mecha de rechazo" es interpretación humana. |
| "último mínimo relevante"      | `Bars.LowPrices.Minimum(N)` sobre una ventana N explícita, no "el que se ve en el gráfico".                               |
| Tendencia mayor                | La estrategia original **no tiene filtro macro**. Sin él, EMA9/21 en M5 opera lateralidad y sangra por whipsaw.           |
| Sesión / spread                | No mencionados. En intradía son determinantes.                                                                            |

## 3. Especificación mecánica recomendada

Usar la misma **máquina de estados de 2 fases** (armar → confirmar) del patrón ya
validado en `bots/EURUSD_RSI2_PULLBACK`. No entrar en la vela que detecta el toque:
un toque de EMA sin confirmación es la señal de mayor ruido de todo el sistema.

### Indicadores

- **EMA 9** — borde rápido de la zona de pullback.
- **EMA 21** — borde lento; juntas forman la **banda de retesteo**
  `[min(EMA9,EMA21), max(EMA9,EMA21)]`.
- **EMA 200** (o 50 según TF) — **filtro de tendencia macro. Añadido; no está en la
  estrategia original.** Solo operar a favor. Parametrizable y desactivable para poder
  medir su aporte en backtest A/B.
- **ATR(14, Exponential)** — tolerancia de la zona, banda de volatilidad válida y base
  del SL/TP.

Valores por defecto propuestos para el primer backtest:

| Parámetro            | Valor propuesto |
| -------------------- | --------------: |
| ImpulseLookback      |               5 |
| ImpulseAtrMultiple   |             1.0 |
| PullbackAtrTolerance |             0.3 |
| MinAtrPips           |               3 |
| MaxAtrPips           |              18 |
| MaxSetupBars         |               3 |
| MaxSpreadPips        |             2.0 |
| SlAtrMultiple        |             1.2 |
| RiskReward           |             1.8 |
| MaxBarsInTrade       |               8 |

### Definición de tendencia (Bullish; short es simétrico)

```
EMA9 > EMA21
close > EMA200                     (si UseMacroFilter)
EMA21 > EMA21[SlopeBars]           (pendiente ascendente, SlopeBars ≈ 3)
```

### Fase 1 — Armado (Long)

```
Tendencia alcista vigente
Impulso previo (long):  en las últimas ImpulseLookback velas hubo al menos una con
                       (close − EMA21) ≥ ImpulseAtrMultiple × ATR
Impulso previo (short): en las últimas ImpulseLookback velas hubo al menos una con
                       (EMA21 − close) ≥ ImpulseAtrMultiple × ATR
                       usando el mismo ATR(14, Exponential) definido en la sección de Indicadores
Pullback actual: low  ≤ EMA9  + PullbackAtrTolerance × ATR
                 close ≥ EMA21 − PullbackAtrTolerance × ATR
                 (tocó la zona pero no la perdió)
Volatilidad:     MinAtrPips ≤ ATR_pips ≤ MaxAtrPips
Sesión:          hora dentro de la ventana operativa
```

El chequeo de **impulso previo** es lo que separa esta estrategia de "comprar cada vez
que el precio toca la EMA9". Sin él no hay pullback: hay lateralidad pegada a las medias.

### Fase 2 — Confirmación (dentro de `MaxSetupBars` ≈ 3 velas)

```
Tendencia sigue vigente          → si no, reset (no forzar la entrada)
                                  Si durante la fase de armado EMA9 cruza EMA21 en sentido contrario, invalidar inmediatamente el setup y no evaluar nuevas señales hasta que se cumplan de nuevo las condiciones de Fase 1.
Volatilidad sigue en banda       → si no, reset
close > open  AND  close > high[1]    (vela direccional que rompe el extremo previo)
spread ≤ MaxSpreadPips           → si no, se pierde esa vela; el setup NO se resetea y, si no se confirma antes de agotar `MaxSetupBars`, expira igual
```

El setup **expira** al agotar `MaxSetupBars`, incluso si una vela se descarta por spread. Nunca dejarlo armado indefinidamente.

### Riesgo y salida

- **SL estructural (fiel al original):** `min(low, LowestLow(SwingLookback)) − BufferAtr × ATR`.
- **SL por ATR (alternativa):** `SlAtrMultiple × ATR`.
- **Elegir uno y parametrizarlo**, no mezclar. El estructural es más fiel a la
  estrategia, pero produce **riesgo variable por operación** → obliga a **sizing
  dinámico** (`volumen = riesgo_$ / distancia_SL`). Con lote fijo, un R:R nominal 1:2
  no se traduce en expectativa 1:2.
- **TP:** `RiskReward × distancia_SL` (1.5 o 2.0).
- **Time stop:** cierre forzado tras `MaxBarsInTrade` velas. En intradía, un pullback
  que no resuelve en pocas velas suele ser tendencia agotada.
- **Circuit breaker:** equity de corte + **límite de pérdida diaria** + lockout tras N
  pérdidas consecutivas. En TF bajos la frecuencia de operaciones es alta y una racha
  mala se acumula rápido — el corte diario aquí pesa más que en M15/H1.

## 4. Riesgos y limitaciones a declarar explícitamente

Al entregar un spec o un bot de esta familia, **incluir estas advertencias**; no son
opcionales:

1. **El whipsaw en rango es el modo de fallo dominante.** EMA9/21 en M1-M5 genera
   toques constantes sin tendencia. El filtro macro y el chequeo de impulso son
   mitigaciones, no soluciones.
2. **Los costos de transacción dominan en TF bajos.** Con un TP de ~1.5×ATR en M5, el
   spread puede ser una fracción no trivial del objetivo. Verificar el spread real del
   bróker (`get_symbol_details` vía MCP) contra el TP esperado antes de asumir viabilidad.
3. **La estrategia es popular y simple** → sin filtros adicionales, su edge en backtest
   suele ser marginal o negativo después de costos. Tratar cualquier resultado positivo
   con sospecha de sobreajuste hasta validarlo out-of-sample.
4. **No es production-ready sin PoC.** Requiere backtest de 6-12+ meses en el simulador
   de cTrader con datos del propio bróker antes de capital real. Y demo antes que real.

## 5. Validación del activo antes de spec-ear

Mismo procedimiento que `markdowns/rsi ema indicator.md` §6: spread real, rango típico
del TF, carácter del activo (tendencial vs lateral), backtest dedicado. Un activo
lateral penaliza esta estrategia más que a la mayoría — el filtro de impulso
simplemente no disparará, o disparará sobre ruido.

## 6. Procedimiento de trabajo

1. Cerrar **todas** las ambigüedades de §2 con el usuario antes de escribir código. Si
   el usuario no tiene preferencia, proponer los defaults de §3 y decir explícitamente
   que son defaults propuestos, no validados.
2. Escribir primero `strategy.md` documentando la implementación **tal como será**, con
   una sección de observaciones de revisión técnica (patrón de
   `bots/EURUSD_RSI2_PULLBACK/strategy.md` §6).
3. Implementar el cBot: `OnBarClosed` para la señal, `OnTick` solo para el circuit
   breaker, `[Parameter]` agrupados por sección (Trend / Pullback / Volatility / Risk /
   Session), label única por instancia, validar `Positions.Count(label) == 0` antes de abrir.
4. Backtest → registrar hallazgos en `strategy.md` → iterar. Una versión no queda
   validada por haber sido implementada.
