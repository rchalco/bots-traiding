# Strategy — INDEX_EMA9_21_RETEST

## 1. Objetivo

Implementar un cBot de continuación de tendencia para índices como SP500 o Nasdaq, orientado a ventanas M5-M15-H1.

## 2. Lógica

- EMA 9 y EMA 21 para definir la zona de retest.
- EMA 200 como filtro macro.
- ATR(14, Exponential) para medir impulso y volatilidad.
- Armado de setup cuando el precio ha impulsado lejos de la banda y luego realiza un pullback a la zona.
- Confirmación direccional en una vela posterior.
- Salida por SL/TP ATR y time stop.

## 3. Parámetros recomendados

- ImpulseLookback = 5
- ImpulseAtrMultiple = 1.0
- PullbackAtrTolerance = 0.3
- MaxSetupBars = 3
- MaxSpreadPips = 2.0
- SlAtrMultiple = 1.2
- RiskReward = 1.8

## 4. Consideraciones

- No asumir que el bot es rentable sin backtest.
- Ajustar los parámetros al spread real del broker y a la volatilidad del activo.
- Para índices, priorizar M5-M15 y evitar operar en rangos muy laterales.
