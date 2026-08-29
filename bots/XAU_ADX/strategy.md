# XAU ADX - Requerimientos

## 1. Contexto y Objetivo
* **Instrumento / Activo:** XAUUSD (Oro spot, USD por onza)
* **Temporalidad (TF):** Multi-perfil — 1M/3M (Scalping), 5M/15M (Day Trading), 4H/Diario (Swing). El perfil activo se elige con el parámetro `Mode`; el gráfico donde se adjunta el bot debe corresponder al TF del modo elegido.
* **Objetivo Principal:** Seguimiento de tendencia en oro usando ADX + Directional Movement (+DI/-DI), con gestión de riesgo y ventana horaria (hora Bolivia) específicas por perfil de trading.

## 2. Indicadores y Variables Técnicas
* **Indicador Principal:** ADX (Average Directional Index) vía Directional Movement System de cAlgo
  * **Parámetros:** Periodo = `14` (parametrizable), Umbral de Tendencia = `25` (parametrizable)
* **Indicadores de Apoyo / Filtros:**
  * +DI / -DI (del mismo Directional Movement System) para dirección de la señal.
  * Filtro de sesión horaria (hora Bolivia, UTC-4) según perfil.

### Perfiles de trading (según tabla del usuario)

| Estrategia    | Horario (Bolivia)                              | Entrada preferida                                                  | SL promedio          | TP promedio            |
|---------------|-------------------------------------------------|---------------------------------------------------------------------|-----------------------|-------------------------|
| Scalping      | 09:30–11:30 (apertura NY)                       | Continuación de impulso 1m/3m o ruptura de rango de apertura        | 1.5–3.0 USD (15–30 pips) | 3.0–6.0 USD (30–60 pips)  |
| Day Trading   | 03:00–05:00 (Londres) o 09:30–12:00 (NY)        | Retroceso a VWAP o rechazo en niveles estructurales ($4,400/$4,600) en 5m/15m | 5.0–10.0 USD (50–100 pips) | 12.0–25.0 USD (120–250 pips) |
| Swing Trading | Sin restricción horaria (gráfico 4H/Diario)     | Reacción en soportes clave, incorporación a tendencia primaria      | 25.0–40.0 USD          | 75.0–120.0 USD           |

> Nota de alcance: la v1 del bot implementa el motor de señal ADX/+DI/-DI (igual para los 3 perfiles) con SL/TP y ventana horaria configurados por perfil según la tabla. La lógica específica de VWAP y rechazo en niveles estructurales (Day Trading) y de soporte clave discrecional (Swing) **no** está implementada todavía — son mejoras futuras candidatas, no asumidas como completas.

## 3. Lógica de Entrada (Reglas de Apertura)
* **Condición de Compra (Buy):**
  * ADX > Umbral (25) y ascendente (ADX actual > ADX de la vela anterior).
  * +DI cruza por encima de -DI en la vela actual (vela anterior +DI <= -DI).
* **Condición de Venta (Sell):**
  * ADX > Umbral (25) y ascendente.
  * -DI cruza por encima de +DI en la vela actual (vela anterior -DI <= +DI).
* Solo una posición abierta a la vez (`Positions.Count == 0` antes de abrir).
* Fuera de la ventana horaria del perfil activo, no se abren nuevas posiciones (Swing no tiene restricción horaria).

## 4. Gestión de Riesgo y Salida
* **Stop Loss (SL) / Take Profit (TP):** definidos en USD (distancia de precio directa, no en "pips" de plataforma, para evitar ambigüedad de tamaño de pip por bróker), con default = punto medio del rango de la tabla según `Mode`.
* **Filtros de Salida por Agotamiento:** si el ADX cae por debajo del umbral, se cierra cualquier posición abierta por el bot (independiente de SL/TP).

## 5. Requerimientos de Código (cTrader / C#)
* **Estructura Esperada:** cBot en `cAlgo.Robots`, uso de `protected override void OnBar()`, parámetros vía atributos `[Parameter]` agrupados (Estrategia/ADX/Riesgo/Sesión).
* **Control de Órdenes:** valida `Positions.Count(label) == 0` antes de abrir una nueva operación; una sola posición simultánea por el bot.
