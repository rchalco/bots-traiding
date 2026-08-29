# Plantilla de bot cTrader

Usar esta carpeta como base para cada bot nuevo:

1. Copiar `_template/` a `bots/<NOMBRE_BOT>/`.
2. Rellenar `strategy.md` con la estrategia concreta (instrumento, indicadores, reglas de entrada, SL/TP, salida por agotamiento).
3. Escribir el cBot en `<NOMBRE_BOT>.cs` (namespace `cAlgo.Robots`, clase = nombre del bot).
4. Abrir cTrader Automate → Robots → New → pegar/editar el código, o copiar el `.cs` al directorio de fuentes de cAlgo (`Documents\cAlgo\Sources\Robots\<NOMBRE_BOT>\`) y compilar (Build) desde el IDE de cTrader.
5. Backtest en el simulador antes de correr en cuenta real u operar en real solo bajo autorización explícita del usuario.
