# XAU ADX

cBot de cTrader para XAUUSD basado en ADX + Directional Movement (+DI/-DI), con perfiles Scalping / Day Trading / Swing (SL, TP y ventana horaria por perfil). Ver [strategy.md](strategy.md) para el detalle de la estrategia y el alcance actual (v1 no incluye VWAP ni detección de soporte/resistencia estructural).

## Compilar

El proyecto vive en `...\Documentos\cAlgo\Sources\Robots\XAU_ADX\` (mismo formato SDK-style .NET 6 que usan tus otros bots: `XAU_ADX.csproj` con `PackageReference` a `cTrader.Automate` / `cTrader.Automate.SourceGenerators`, que se resuelven desde nuget.org).

**Opción A — cTrader Automate:** abrirlo ahí, el robot aparece en la lista → Build (Ctrl+B).

**Opción B — VS Code / terminal (sin abrir cTrader):**
```bash
dotnet build "C:\Users\viajero\OneDrive\Documentos\cAlgo\Sources\Robots\XAU_ADX\XAU_ADX\XAU_ADX.csproj"
```
Esto genera `XAU_ADX.algo` directamente en `Sources\Robots\XAU_ADX.algo` (mismo lugar donde cTrader espera encontrarlo), sin necesidad de abrir el IDE de cTrader para compilar. Ya verificado: compila limpio (0 errores) con esta configuración. Si cTrader está abierto, puede que necesites refrescar la lista de Automate para que lo detecte.

Backtestear en el simulador antes de correr en real. Adjuntar al gráfico de XAUUSD en el timeframe correspondiente al `Mode` elegido (1M/3M para Scalping, 5M/15M para Day Trading, 4H/Diario para Swing).

## Instalar vía MCP (ctrader, Pepperstone)
Pendiente: el servidor MCP `ctrader` (`http://127.0.0.1:9876/mcp/`) aún no está cargado como tools en esta sesión de Claude Code. Una vez reiniciada la sesión y aprobado el `.mcp.json` del proyecto, se usará ese MCP para subir/instalar `XAU_ADX.cs` en la cuenta de Pepperstone.
