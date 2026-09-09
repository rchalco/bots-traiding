using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class cBotSingleEMA : Robot
    {
        // ══════════════════════════════════════════════
        //  VOLUMEN GLOBAL
        // ══════════════════════════════════════════════
        [Parameter("Volumen (Lots)", Group = "General", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double VolumenLots { get; set; }

        // ══════════════════════════════════════════════
        //  ESTRATEGIA — EMA 46/78 | Sesión 02–04 UTC
        // ══════════════════════════════════════════════
        [Parameter("EMA Rápida", Group = "Configuración Estrategia", DefaultValue = 46, MinValue = 1)]
        public int EmaFast { get; set; }

        [Parameter("EMA Lenta", Group = "Configuración Estrategia", DefaultValue = 78, MinValue = 1)]
        public int EmaSlow { get; set; }

        [Parameter("Stop Loss (pips)", Group = "Configuración Estrategia", DefaultValue = 39, MinValue = 1)]
        public int SL { get; set; }

        [Parameter("Take Profit (pips)", Group = "Configuración Estrategia", DefaultValue = 60, MinValue = 1)]
        public int TP { get; set; }

        [Parameter("Hora inicio UTC", Group = "Configuración Estrategia", DefaultValue = 2, MinValue = 0, MaxValue = 23)]
        public int HoraInicio { get; set; }

        [Parameter("Hora fin UTC", Group = "Configuración Estrategia", DefaultValue = 4, MinValue = 0, MaxValue = 23)]
        public int HoraFin { get; set; }

        [Parameter("Máx. posiciones", Group = "Configuración Estrategia", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int MaxPos { get; set; }

        // ══════════════════════════════════════════════
        //  VARIABLES INTERNAS
        // ══════════════════════════════════════════════
        private ExponentialMovingAverage _emaRapida, _emaLenta;
        private bool _encimaPrev, _inicializado;
        private const string LABEL_BOT = "SingleEMA_E4";

        // ══════════════════════════════════════════════
        //  INICIALIZACIÓN
        // ══════════════════════════════════════════════
        protected override void OnStart()
        {
            _emaRapida = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaFast);
            _emaLenta  = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaSlow);

            Print("=== cBot Single EMA iniciado ===");
            Print("Volumen global: {0} Lots", VolumenLots);
            Print("EMA {0}/{1} | SL:{2} TP:{3} | Sesión {4}h-{5}h UTC", EmaFast, EmaSlow, SL, TP, HoraInicio, HoraFin);
        }

        // ══════════════════════════════════════════════
        //  LÓGICA PRINCIPAL — CADA BARRA CERRADA
        // ══════════════════════════════════════════════
        protected override void OnBar()
        {
            int minBars = Math.Max(EmaFast, EmaSlow) + 2;

            if (Bars.Count < minBars) return;

            EjecutarEstrategia();
        }

        // ══════════════════════════════════════════════
        //  LÓGICA DE TRADING
        // ══════════════════════════════════════════════
        private void EjecutarEstrategia()
        {
            double rap  = _emaRapida.Result.Last(0);
            double lent = _emaLenta.Result.Last(0);
            bool encima = rap > lent;

            if (!_inicializado) 
            { 
                _encimaPrev = encima; 
                _inicializado = true; 
                return; 
            }

            bool cruceAlcista = !_encimaPrev && encima;
            bool cruceBajista  =  _encimaPrev && !encima;
            _encimaPrev = encima;

            if (!EnSesion(HoraInicio, HoraFin)) return;

            if (cruceAlcista)
            {
                Print("Cruce ALCISTA | EMA{0}={1:F5} EMA{2}={3:F5}", EmaFast, rap, EmaSlow, lent);
                if (ContarPos(LABEL_BOT) < MaxPos) AbrirOrden(TradeType.Buy, SL, TP, LABEL_BOT);
            }
            if (cruceBajista)
            {
                Print("Cruce BAJISTA | EMA{0}={1:F5} EMA{2}={3:F5}", EmaFast, rap, EmaSlow, lent);
                if (ContarPos(LABEL_BOT) < MaxPos) AbrirOrden(TradeType.Sell, SL, TP, LABEL_BOT);
            }
        }

        // ══════════════════════════════════════════════
        //  FUNCIONES AUXILIARES
        // ══════════════════════════════════════════════
        private void AbrirOrden(TradeType tipo, int sl, int tp, string label)
        {
            double volumen = Symbol.QuantityToVolumeInUnits(VolumenLots);
            var r = ExecuteMarketOrder(tipo, SymbolName, volumen, label, sl, tp);
            
            if (r.IsSuccessful)
                Print("[{0}] {1} abierto | Entrada:{2} SL:{3} TP:{4}",
                      label, tipo, r.Position.EntryPrice, r.Position.StopLoss, r.Position.TakeProfit);
            else
                Print("[{0}] ERROR {1}: {2}", label, tipo, r.Error);
        }

        private bool EnSesion(int horaInicio, int horaFin)
        {
            int hora = Server.Time.Hour;
            if (horaInicio > horaFin)
                return hora >= horaInicio || hora < horaFin;
            else
                return hora >= horaInicio && hora < horaFin;
        }

        private int ContarPos(string label)
        {
            return Positions.FindAll(label, SymbolName).Length;
        }

        // ══════════════════════════════════════════════
        //  PARADA
        // ══════════════════════════════════════════════
        protected override void OnStop()
        {
            Print("=== cBot detenido | Posiciones abiertas: {0} ===", ContarPos(LABEL_BOT));
        }
    }
}