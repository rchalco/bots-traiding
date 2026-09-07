# INDEX_EMA9_21_RETEST

Bot de cTrader para índices accionarios (SP500/Nasdaq) con lógica de pullback de continuación de tendencia usando EMA 9/21, filtro macro de EMA 200, ATR, spread, ventana horaria y gestión de riesgo por ATR.

## Alcance

- Temporalidades recomendadas: M5, M15, H1.
- Activos objetivo: SP500, Nasdaq, otros índices con liquidez alta y spread bajo.
- Enfoque: entradas de continuación en pullbacks a la banda EMA 9/21 dentro de tendencia.

## Reglas principales

1. Tendencia alcista/bajista con EMA 9 > EMA 21 y EMA 21 pendiente positiva.
2. Impulso previo con distancia respecto a EMA 21 mayor que un múltiplo de ATR.
3. Pullback actual hacia la zona EMA 9/21.
4. Confirmación direccional con vela de rechazo/ruptura dentro del máximo de barras de armado.
5. Salida por SL/TP ATR y time stop.

## Uso en cTrader

- Copiar la carpeta a la carpeta de fuentes de cAlgo o abrir el archivo `.cs` directamente en cTrader Automate.
- Adjuntar el bot al gráfico del activo deseado en M5/M15/H1.
- Probar primero en demo y con datos históricos del broker.
