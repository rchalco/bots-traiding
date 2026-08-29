using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    public enum TradingMode
    {
        Scalping,
        DayTrading,
        Swing
    }

    /// <summary>
    /// XAU ADX - seguimiento de tendencia en oro (XAUUSD) con ADX + Directional Movement.
    /// Ver strategy.md para el detalle de la estrategia y su alcance.
    /// ADVERTENCIA: probar siempre en cuenta demo antes de operar en real.
    /// </summary>
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class XAU_ADX : Robot
    {
        [Parameter("Modo", DefaultValue = TradingMode.DayTrading, Group = "Estrategia")]
        public TradingMode Mode { get; set; }

        [Parameter("Periodo ADX", DefaultValue = 14, MinValue = 2, Group = "ADX")]
        public int AdxPeriod { get; set; }

        [Parameter("Umbral de tendencia ADX", DefaultValue = 25.0, MinValue = 1.0, Group = "ADX")]
        public double AdxThreshold { get; set; }

        [Parameter("Volumen (lotes)", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "Riesgo")]
        public double VolumeLots { get; set; }

        [Parameter("SL Scalping (pips)", DefaultValue = 22, MinValue = 1, Group = "Riesgo - Scalping")]
        public double ScalpingSlPips { get; set; }

        [Parameter("TP Scalping (pips)", DefaultValue = 45, MinValue = 1, Group = "Riesgo - Scalping")]
        public double ScalpingTpPips { get; set; }

        [Parameter("SL Day Trading (pips)", DefaultValue = 75, MinValue = 1, Group = "Riesgo - Day Trading")]
        public double DayTradingSlPips { get; set; }

        [Parameter("TP Day Trading (pips)", DefaultValue = 185, MinValue = 1, Group = "Riesgo - Day Trading")]
        public double DayTradingTpPips { get; set; }

        [Parameter("SL Swing (pips)", DefaultValue = 325, MinValue = 1, Group = "Riesgo - Swing")]
        public double SwingSlPips { get; set; }

        [Parameter("TP Swing (pips)", DefaultValue = 975, MinValue = 1, Group = "Riesgo - Swing")]
        public double SwingTpPips { get; set; }

        [Parameter("Restringir a horario recomendado (hora Bolivia)", DefaultValue = true, Group = "Sesion")]
        public bool UseSessionFilter { get; set; }

        [Parameter("Etiqueta del bot", DefaultValue = "XAU_ADX", Group = "Identificacion")]
        public string BotLabel { get; set; }

        private DirectionalMovementSystem _dms;

        protected override void OnStart()
        {
            _dms = Indicators.DirectionalMovementSystem(AdxPeriod);

            Print("XAU_ADX iniciado. Modo=" + Mode + " Simbolo=" + SymbolName);
        }

        protected override void OnBar()
        {
            ManageExhaustionExit();

            if (!IsWithinSession())
                return;

            if (Positions.FindAll(BotLabel, SymbolName).Length > 0)
                return;

            var adx = _dms.ADX.Last(1);
            var adxPrev = _dms.ADX.Last(2);
            var diPlus = _dms.DIPlus.Last(1);
            var diMinus = _dms.DIMinus.Last(1);
            var diPlusPrev = _dms.DIPlus.Last(2);
            var diMinusPrev = _dms.DIMinus.Last(2);

            var trending = adx > AdxThreshold && adx > adxPrev;
            var bullishCross = diPlusPrev <= diMinusPrev && diPlus > diMinus;
            var bearishCross = diMinusPrev <= diPlusPrev && diMinus > diPlus;

            if (trending && bullishCross)
                OpenTrade(TradeType.Buy);
            else if (trending && bearishCross)
                OpenTrade(TradeType.Sell);
        }

        private void ManageExhaustionExit()
        {
            if (_dms.ADX.Last(1) >= AdxThreshold)
                return;

            foreach (var position in Positions.FindAll(BotLabel, SymbolName))
            {
                var result = ClosePosition(position);
                if (result.IsSuccessful)
                    Print("Cierre por agotamiento de ADX. Posicion " + position.Id);
            }
        }

        private void OpenTrade(TradeType type)
        {
            var volumeInUnits = Symbol.QuantityToVolumeInUnits(VolumeLots);
            double slPips, tpPips;
            GetRiskForMode(out slPips, out tpPips);

            var result = ExecuteMarketOrder(type, SymbolName, volumeInUnits, BotLabel, slPips, tpPips);

            if (result.IsSuccessful)
                Print("Trade abierto: " + type + " " + VolumeLots + " lotes. SL=" + slPips + "p TP=" + tpPips + "p. Posicion " + result.Position.Id);
            else
                Print("ERROR al abrir trade: " + result.Error);
        }

        private void GetRiskForMode(out double slPips, out double tpPips)
        {
            switch (Mode)
            {
                case TradingMode.Scalping:
                    slPips = ScalpingSlPips;
                    tpPips = ScalpingTpPips;
                    break;
                case TradingMode.Swing:
                    slPips = SwingSlPips;
                    tpPips = SwingTpPips;
                    break;
                default:
                    slPips = DayTradingSlPips;
                    tpPips = DayTradingTpPips;
                    break;
            }
        }

        private bool IsWithinSession()
        {
            if (!UseSessionFilter || Mode == TradingMode.Swing)
                return true;

            var boliviaTime = Server.TimeInUtc.AddHours(-4).TimeOfDay;

            switch (Mode)
            {
                case TradingMode.Scalping:
                    return boliviaTime >= new TimeSpan(9, 30, 0) && boliviaTime <= new TimeSpan(11, 30, 0);

                case TradingMode.DayTrading:
                    var london = boliviaTime >= new TimeSpan(3, 0, 0) && boliviaTime <= new TimeSpan(5, 0, 0);
                    var newYork = boliviaTime >= new TimeSpan(9, 30, 0) && boliviaTime <= new TimeSpan(12, 0, 0);
                    return london || newYork;

                default:
                    return true;
            }
        }

        protected override void OnStop()
        {
        }
    }
}
