# [NOMBRE DEL BOT / ESTRATEGIA] - Plantilla de Requerimientos

## 1. Contexto y Objetivo
* **Instrumento / Activo:** [Ej. EURUSD, Criptomonedas, Acciones]
* **Temporalidad (TF):** [Ej. 1H, 15M]
* **Objetivo Principal:** [Ej. Automatizar seguimiento de tendencia con ADX, estrategia de ruptura, etc.]

## 2. Indicadores y Variables Técnicas
* **Indicador Principal:** [Ej. ADX - Average Directional Index]
  * **Parámetros:** Periodo = `14`, Umbral de Tendencia = `25`
* **Indicadores de Apoyo / Filtros:** [Ej. Medias Móviles, RSI, Volumen]

## 3. Lógica de Entrada (Reglas de Apertura)
* **Condición de Compra (Buy):**
  * [Describir regla 1, ej. ADX > 25 y ascendente]
  * [Describir regla 2, ej. +DI cruza por encima de -DI]
* **Condición de Venta (Sell):**
  * [Describir regla 1]
  * [Describir regla 2]

## 4. Gestión de Riesgo y Salida
* **Stop Loss (SL):** [Ej. Basado en ATR, puntos fijos, o mínimo anterior]
* **Take Profit (TP):** [Ej. Relación riesgo-recompensa 1:2 o trailing stop]
* **Filtros de Salida por Agotamiento:** [Ej. Cerrar o reducir posición si el ADX decae por debajo del umbral]

## 5. Requerimientos de Código (cTrader / C#)
* **Estructura Esperada:** [Ej. Plantilla cBot limpia, uso de `OnBar`, manejo de parámetros mediante atributos `[Parameter]`].
* **Control de Órdenes:** [Ej. Validar que `Positions.Count == 0` antes de abrir nueva operaciones].
