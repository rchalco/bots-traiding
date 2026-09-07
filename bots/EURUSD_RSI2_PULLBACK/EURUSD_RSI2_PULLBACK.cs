using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(
        TimeZone = TimeZones.UTC,
        AccessRights = AccessRights.None)]
    public class EurUsdPullbackV2M15 : Robot
    {
        // ============================================================
        // ENUMS
        // ============================================================

        private enum SetupState
        {
            None,
            LongArmed,
            ShortArmed
        }


        // ============================================================
        // GENERAL
        // ============================================================

        [Parameter(
            "Label",
            DefaultValue = "EURUSD_PULLBACK_V3_M15",
            Group = "General")]
        public string BotLabel { get; set; }


        // ============================================================
        // TRADING
        // ============================================================

        [Parameter(
            "Volume (Lots)",
            DefaultValue = 0.01,
            MinValue = 0.01,
            Step = 0.01,
            Group = "Trading")]
        public double VolumeInLots { get; set; }

        [Parameter(
            "Stop Loss (Pips)",
            DefaultValue = 8.0,
            MinValue = 1.0,
            Step = 0.5,
            Group = "Trading")]
        public double StopLossPips { get; set; }

        [Parameter(
            "Take Profit (Pips)",
            DefaultValue = 12.0,
            MinValue = 1.0,
            Step = 0.5,
            Group = "Trading")]
        public double TakeProfitPips { get; set; }

        [Parameter(
            "Allow Long",
            DefaultValue = true,
            Group = "Trading")]
        public bool AllowLong { get; set; }

        [Parameter(
            "Allow Short",
            DefaultValue = true,
            Group = "Trading")]
        public bool AllowShort { get; set; }


        // ============================================================
        // RSI
        // ============================================================

        [Parameter(
            "RSI Period",
            DefaultValue = 2,
            MinValue = 2,
            MaxValue = 20,
            Group = "RSI")]
        public int RsiPeriod { get; set; }

        [Parameter(
            "RSI Oversold",
            DefaultValue = 10.0,
            MinValue = 1,
            MaxValue = 40,
            Step = 1,
            Group = "RSI")]
        public double RsiOversold { get; set; }

        [Parameter(
            "RSI Overbought",
            DefaultValue = 90.0,
            MinValue = 60,
            MaxValue = 99,
            Step = 1,
            Group = "RSI")]
        public double RsiOverbought { get; set; }

        [Parameter(
            "Require RSI Exit",
            DefaultValue = true,
            Group = "RSI")]
        public bool RequireRsiExit { get; set; }


        // ============================================================
        // TREND
        // ============================================================

        [Parameter(
            "EMA Pullback",
            DefaultValue = 20,
            MinValue = 5,
            Group = "Trend")]
        public int EmaPullbackPeriod { get; set; }

        [Parameter(
            "EMA Filter",
            DefaultValue = 50,
            MinValue = 10,
            Group = "Trend")]
        public int EmaFilterPeriod { get; set; }

        [Parameter(
            "EMA Trend",
            DefaultValue = 200,
            MinValue = 20,
            Group = "Trend")]
        public int EmaTrendPeriod { get; set; }

        [Parameter(
            "EMA Slope Bars",
            DefaultValue = 3,
            MinValue = 1,
            MaxValue = 20,
            Group = "Trend")]
        public int EmaSlopeBars { get; set; }


        // ============================================================
        // PULLBACK
        // ============================================================

        [Parameter(
            "Pullback ATR Distance",
            DefaultValue = 0.50,
            MinValue = 0.10,
            MaxValue = 2.0,
            Step = 0.05,
            Group = "Pullback")]
        public double PullbackAtrDistance { get; set; }

        [Parameter(
            "Max Setup Bars",
            DefaultValue = 3,
            MinValue = 1,
            MaxValue = 10,
            Group = "Pullback")]
        public int MaxSetupBars { get; set; }


        // ============================================================
        // VOLATILITY
        // ============================================================

        [Parameter(
            "ATR Period",
            DefaultValue = 14,
            MinValue = 2,
            Group = "Volatility")]
        public int AtrPeriod { get; set; }

        [Parameter(
            "Minimum ATR (Pips)",
            DefaultValue = 3.0,
            MinValue = 0,
            Step = 0.5,
            Group = "Volatility")]
        public double MinimumAtrPips { get; set; }

        [Parameter(
            "Maximum ATR (Pips)",
            DefaultValue = 18.0,
            MinValue = 1,
            Step = 0.5,
            Group = "Volatility")]
        public double MaximumAtrPips { get; set; }

        [Parameter(
            "Max Spread (Pips)",
            DefaultValue = 1.2,
            MinValue = 0.1,
            Step = 0.1,
            Group = "Volatility")]
        public double MaximumSpreadPips { get; set; }


        // ============================================================
        // RISK (SL/TP)
        //
        // Backtest 01/07/2026-02/09/2026 (27 trades, M15): con SL/TP
        // fijos (8/12 pips) el TP se tocó solo 18.5% de las veces
        // contra 40.7% de SL, muy por debajo del 40% de acierto que
        // necesita un R:R 1:1.5 para no perder dinero. La banda de
        // ATR admitida (3-18 pips) es demasiado ancha para un SL/TP
        // fijo — se prueba SL/TP como múltiplo de ATR en su lugar.
        // ============================================================

        [Parameter(
            "Use ATR-based SL/TP",
            DefaultValue = true,
            Group = "Risk")]
        public bool UseAtrBasedRisk { get; set; }

        [Parameter(
            "SL ATR Multiple",
            DefaultValue = 1.0,
            MinValue = 0.1,
            Step = 0.1,
            Group = "Risk")]
        public double SlAtrMultiple { get; set; }

        [Parameter(
            "TP ATR Multiple",
            DefaultValue = 1.5,
            MinValue = 0.1,
            Step = 0.1,
            Group = "Risk")]
        public double TpAtrMultiple { get; set; }


        // ============================================================
        // POSITION MANAGEMENT
        // ============================================================

        [Parameter(
            "Max Bars In Trade",
            DefaultValue = 8,
            MinValue = 0,
            MaxValue = 100,
            Group = "Position Management")]
        public int MaxBarsInTrade { get; set; }


        // ============================================================
        // SESSION FILTER
        //
        // Backtest 01/07/2026-02/09/2026: 0 operaciones ganadoras de
        // 13 en horas 01-07 UTC (sesión asiática, baja liquidez para
        // EURUSD); las 7 ganadoras cayeron todas en aperturas de
        // Londres/NY o cierre de NY. Solo gatea el armado de setups
        // NUEVOS — un setup ya armado puede seguir confirmando o
        // expirando normalmente fuera de la ventana.
        // ============================================================

        [Parameter(
            "Use Session Filter",
            DefaultValue = true,
            Group = "Session")]
        public bool UseSessionFilter { get; set; }

        [Parameter(
            "Session Start Hour (UTC)",
            DefaultValue = 7,
            MinValue = 0,
            MaxValue = 23,
            Group = "Session")]
        public int SessionStartHour { get; set; }

        [Parameter(
            "Session End Hour (UTC)",
            DefaultValue = 22,
            MinValue = 0,
            MaxValue = 23,
            Group = "Session")]
        public int SessionEndHour { get; set; }


        // ============================================================
        // CAPITAL PROTECTION
        //
        // ReferenceCapital = 0:
        //     usa Equity al arrancar el bot.
        //
        // ReferenceCapital > 0:
        //     usa ese capital como referencia.
        //
        // Ejemplo:
        // ReferenceCapital = 100
        // MaximumCapitalLoss = 50
        //
        // Stop Equity = 50
        // ============================================================

        [Parameter(
            "Reference Capital",
            DefaultValue = 100.0,
            MinValue = 0,
            Step = 10,
            Group = "Capital Protection")]
        public double ReferenceCapital { get; set; }

        [Parameter(
            "Maximum Capital Loss (%)",
            DefaultValue = 50.0,
            MinValue = 1,
            MaxValue = 99,
            Step = 1,
            Group = "Capital Protection")]
        public double MaximumCapitalLossPercent { get; set; }


        // ============================================================
        // INDICATORS
        // ============================================================

        private RelativeStrengthIndex _rsi;

        private ExponentialMovingAverage _ema20;
        private ExponentialMovingAverage _ema50;
        private ExponentialMovingAverage _ema200;

        private AverageTrueRange _atr;


        // ============================================================
        // INTERNAL STATE
        // ============================================================

        private SetupState _setupState = SetupState.None;

        private int _setupAge;

        private int _barsSinceEntry;

        private double _volumeInUnits;

        private double _capitalReference;

        private double _capitalStopLevel;

        private bool _capitalProtectionTriggered;


        // ============================================================
        // START
        // ============================================================

        protected override void OnStart()
        {
            // --------------------------------------------------------
            // Volume
            // --------------------------------------------------------

            _volumeInUnits =
                Symbol.QuantityToVolumeInUnits(VolumeInLots);

            _volumeInUnits =
                Symbol.NormalizeVolumeInUnits(
                    _volumeInUnits,
                    RoundingMode.Down);


            // --------------------------------------------------------
            // Indicators
            // --------------------------------------------------------

            _rsi =
                Indicators.RelativeStrengthIndex(
                    Bars.ClosePrices,
                    RsiPeriod);

            _ema20 =
                Indicators.ExponentialMovingAverage(
                    Bars.ClosePrices,
                    EmaPullbackPeriod);

            _ema50 =
                Indicators.ExponentialMovingAverage(
                    Bars.ClosePrices,
                    EmaFilterPeriod);

            _ema200 =
                Indicators.ExponentialMovingAverage(
                    Bars.ClosePrices,
                    EmaTrendPeriod);

            _atr =
                Indicators.AverageTrueRange(
                    AtrPeriod,
                    MovingAverageType.Exponential);


            // --------------------------------------------------------
            // Capital protection
            // --------------------------------------------------------

            _capitalReference =
                ReferenceCapital > 0
                    ? ReferenceCapital
                    : Account.Equity;

            _capitalStopLevel =
                _capitalReference *
                (1.0 - MaximumCapitalLossPercent / 100.0);


            // --------------------------------------------------------
            // Position events
            // --------------------------------------------------------

            Positions.Closed += OnPositionClosed;


            // --------------------------------------------------------
            // Logs
            // --------------------------------------------------------

            Print("================================================");
            Print("EURUSD PULLBACK V3 M15");
            Print("================================================");

            Print("Symbol: {0}", SymbolName);
            Print("TimeFrame: {0}", TimeFrame);

            Print("Volume: {0} lots", VolumeInLots);
            Print("Volume Units: {0}", _volumeInUnits);

            if (UseAtrBasedRisk)
            {
                Print("SL/TP Mode: ATR-based ({0}x / {1}x)",
                    SlAtrMultiple,
                    TpAtrMultiple);
            }
            else
            {
                Print("SL/TP Mode: Fixed ({0} / {1} pips)",
                    StopLossPips,
                    TakeProfitPips);
            }

            Print("RSI: {0}", RsiPeriod);
            Print("RSI Oversold: {0}", RsiOversold);
            Print("RSI Overbought: {0}", RsiOverbought);

            Print("EMA Pullback: {0}", EmaPullbackPeriod);
            Print("EMA Filter: {0}", EmaFilterPeriod);
            Print("EMA Trend: {0}", EmaTrendPeriod);

            Print("ATR Min: {0} pips", MinimumAtrPips);
            Print("ATR Max: {0} pips", MaximumAtrPips);

            Print("Session Filter: {0}",
                UseSessionFilter
                    ? $"{SessionStartHour:00}:00-{SessionEndHour:00}:00 UTC"
                    : "Disabled");

            Print("Capital Reference: {0:F2}",
                _capitalReference);

            Print("Capital Stop Level: {0:F2}",
                _capitalStopLevel);

            Print("Maximum Loss: {0}%",
                MaximumCapitalLossPercent);

            Print("================================================");


            if (TimeFrame != TimeFrame.Minute15)
            {
                Print(
                    "WARNING: Este bot fue diseñado para EURUSD M15. " +
                    "TimeFrame actual: {0}",
                    TimeFrame);
            }

            if (!SymbolName.StartsWith("EURUSD"))
            {
                Print(
                    "WARNING: Este bot fue diseñado para EURUSD. " +
                    "Symbol actual: {0}",
                    SymbolName);
            }
        }


        // ============================================================
        // TICK
        //
        // El único mecanismo que puede detener automáticamente
        // el bot es la pérdida del porcentaje de capital configurado.
        // ============================================================

        protected override void OnTick()
        {
            CheckCapitalProtection();
        }


        // ============================================================
        // BAR CLOSED
        // ============================================================

        protected override void OnBarClosed()
        {
            if (_capitalProtectionTriggered)
                return;


            // --------------------------------------------------------
            // Esperar suficientes barras
            // --------------------------------------------------------

            int minimumBars =
                Math.Max(
                    EmaTrendPeriod + EmaSlopeBars + 10,
                    250);

            if (Bars.Count < minimumBars)
                return;


            // --------------------------------------------------------
            // Si tenemos una posición, gestionarla
            // --------------------------------------------------------

            Position position =
                GetBotPosition();

            if (position != null)
            {
                ManagePosition(position);
                return;
            }


            // --------------------------------------------------------
            // Obtener contexto actual
            // --------------------------------------------------------

            double close =
                Bars.ClosePrices.Last(0);

            double rsi =
                _rsi.Result.Last(0);

            double atrPips =
                GetAtrPips();


            bool volatilityOk =
                IsVolatilityValid(atrPips);


            // --------------------------------------------------------
            // Si existe setup armado:
            // primero intentar confirmar.
            // --------------------------------------------------------

            if (_setupState != SetupState.None)
            {
                ManageArmedSetup(
                    close,
                    rsi,
                    atrPips);

                return;
            }


            // --------------------------------------------------------
            // No crear setup si volatilidad no es válida
            // --------------------------------------------------------

            if (!volatilityOk)
                return;


            // --------------------------------------------------------
            // No crear setup fuera de la ventana de sesión
            // --------------------------------------------------------

            if (!IsSessionValid())
                return;


            // --------------------------------------------------------
            // Detectar nueva oportunidad
            // --------------------------------------------------------

            bool bullishTrend =
                IsBullishTrend();

            bool bearishTrend =
                IsBearishTrend();


            // ========================================================
            // LONG SETUP
            // ========================================================

            if (
                AllowLong &&
                bullishTrend &&
                IsLongPullbackZone(atrPips) &&
                rsi <= RsiOversold)
            {
                ArmLongSetup();

                return;
            }


            // ========================================================
            // SHORT SETUP
            // ========================================================

            if (
                AllowShort &&
                bearishTrend &&
                IsShortPullbackZone(atrPips) &&
                rsi >= RsiOverbought)
            {
                ArmShortSetup();
            }
        }


        // ============================================================
        // TREND
        // ============================================================

        private bool IsBullishTrend()
        {
            double close =
                Bars.ClosePrices.Last(0);

            double ema50 =
                _ema50.Result.Last(0);

            double ema200 =
                _ema200.Result.Last(0);

            double ema50Past =
                _ema50.Result.Last(EmaSlopeBars);


            return
                close > ema200 &&
                ema50 > ema200 &&
                ema50 > ema50Past;
        }


        private bool IsBearishTrend()
        {
            double close =
                Bars.ClosePrices.Last(0);

            double ema50 =
                _ema50.Result.Last(0);

            double ema200 =
                _ema200.Result.Last(0);

            double ema50Past =
                _ema50.Result.Last(EmaSlopeBars);


            return
                close < ema200 &&
                ema50 < ema200 &&
                ema50 < ema50Past;
        }


        // ============================================================
        // PULLBACK ZONE
        // ============================================================

        private bool IsLongPullbackZone(double atrPips)
        {
            double ema20 =
                _ema20.Result.Last(0);

            double ema50 =
                _ema50.Result.Last(0);

            double close =
                Bars.ClosePrices.Last(0);

            double high =
                Bars.HighPrices.Last(0);

            double low =
                Bars.LowPrices.Last(0);


            double tolerance =
                atrPips *
                Symbol.PipSize *
                PullbackAtrDistance;


            // La vela toca o se aproxima a EMA20
            bool touchesEma20 =
                Math.Abs(close - ema20) <= tolerance ||
                (low <= ema20 && high >= ema20);


            // Evitamos comprar si el pullback ya rompió
            // demasiado profundamente la estructura EMA50.
            bool structureValid =
                close >= ema50 - tolerance;


            return
                touchesEma20 &&
                structureValid;
        }


        private bool IsShortPullbackZone(double atrPips)
        {
            double ema20 =
                _ema20.Result.Last(0);

            double ema50 =
                _ema50.Result.Last(0);

            double close =
                Bars.ClosePrices.Last(0);

            double high =
                Bars.HighPrices.Last(0);

            double low =
                Bars.LowPrices.Last(0);


            double tolerance =
                atrPips *
                Symbol.PipSize *
                PullbackAtrDistance;


            bool touchesEma20 =
                Math.Abs(close - ema20) <= tolerance ||
                (low <= ema20 && high >= ema20);


            bool structureValid =
                close <= ema50 + tolerance;


            return
                touchesEma20 &&
                structureValid;
        }


        // ============================================================
        // SETUP
        // ============================================================

        private void ArmLongSetup()
        {
            _setupState =
                SetupState.LongArmed;

            _setupAge = 0;


            Print(
                "[SETUP LONG ARMED] " +
                "Time={0} | RSI={1:F2} | ATR={2:F2}",
                Bars.OpenTimes.Last(0),
                _rsi.Result.Last(0),
                GetAtrPips());
        }


        private void ArmShortSetup()
        {
            _setupState =
                SetupState.ShortArmed;

            _setupAge = 0;


            Print(
                "[SETUP SHORT ARMED] " +
                "Time={0} | RSI={1:F2} | ATR={2:F2}",
                Bars.OpenTimes.Last(0),
                _rsi.Result.Last(0),
                GetAtrPips());
        }


        private void ResetSetup(string reason)
        {
            if (_setupState != SetupState.None)
            {
                Print(
                    "[SETUP RESET] {0} | Age={1}",
                    reason,
                    _setupAge);
            }


            _setupState =
                SetupState.None;

            _setupAge = 0;
        }


        // ============================================================
        // MANAGE ARMED SETUP
        // ============================================================

        private void ManageArmedSetup(
            double close,
            double rsi,
            double atrPips)
        {
            _setupAge++;


            // --------------------------------------------------------
            // Setup expirado
            // --------------------------------------------------------

            if (_setupAge > MaxSetupBars)
            {
                ResetSetup(
                    "Expired");

                return;
            }


            // --------------------------------------------------------
            // Volatilidad cambió demasiado
            // --------------------------------------------------------

            if (!IsVolatilityValid(atrPips))
            {
                ResetSetup(
                    "Invalid volatility");

                return;
            }


            // ========================================================
            // LONG
            // ========================================================

            if (_setupState == SetupState.LongArmed)
            {
                if (!IsBullishTrend())
                {
                    ResetSetup(
                        "Bullish trend invalidated");

                    return;
                }


                bool rsiConfirmed =
                    !RequireRsiExit ||
                    rsi > RsiOversold;


                if (
                    rsiConfirmed &&
                    ConfirmLongEntry())
                {
                    if (!IsSpreadValid())
                    {
                        Print(
                            "[LONG REJECTED] Spread too high");

                        return;
                    }


                    OpenPosition(
                        TradeType.Buy);

                    ResetSetup(
                        "Long entry");

                    return;
                }
            }


            // ========================================================
            // SHORT
            // ========================================================

            if (_setupState == SetupState.ShortArmed)
            {
                if (!IsBearishTrend())
                {
                    ResetSetup(
                        "Bearish trend invalidated");

                    return;
                }


                bool rsiConfirmed =
                    !RequireRsiExit ||
                    rsi < RsiOverbought;


                if (
                    rsiConfirmed &&
                    ConfirmShortEntry())
                {
                    if (!IsSpreadValid())
                    {
                        Print(
                            "[SHORT REJECTED] Spread too high");

                        return;
                    }


                    OpenPosition(
                        TradeType.Sell);

                    ResetSetup(
                        "Short entry");
                }
            }
        }


        // ============================================================
        // PRICE CONFIRMATION
        // ============================================================

        private bool ConfirmLongEntry()
        {
            double open =
                Bars.OpenPrices.Last(0);

            double close =
                Bars.ClosePrices.Last(0);

            double previousHigh =
                Bars.HighPrices.Last(1);


            bool bullishCandle =
                close > open;


            bool breakout =
                close > previousHigh;


            return
                bullishCandle &&
                breakout;
        }


        private bool ConfirmShortEntry()
        {
            double open =
                Bars.OpenPrices.Last(0);

            double close =
                Bars.ClosePrices.Last(0);

            double previousLow =
                Bars.LowPrices.Last(1);


            bool bearishCandle =
                close < open;


            bool breakout =
                close < previousLow;


            return
                bearishCandle &&
                breakout;
        }


        // ============================================================
        // ATR
        // ============================================================

        private double GetAtrPips()
        {
            return
                _atr.Result.Last(0) /
                Symbol.PipSize;
        }


        private bool IsVolatilityValid(
            double atrPips)
        {
            if (atrPips < MinimumAtrPips)
            {
                Print(
                    "[NO TRADE] ATR LOW: {0:F2}",
                    atrPips);

                return false;
            }


            if (atrPips > MaximumAtrPips)
            {
                Print(
                    "[NO TRADE] ATR HIGH: {0:F2}",
                    atrPips);

                return false;
            }


            return true;
        }


        // ============================================================
        // SESSION
        // ============================================================

        private bool IsSessionValid()
        {
            if (!UseSessionFilter)
                return true;


            int hour =
                Bars.OpenTimes.Last(0).Hour;


            if (SessionStartHour <= SessionEndHour)
            {
                return
                    hour >= SessionStartHour &&
                    hour < SessionEndHour;
            }


            // Ventana que cruza medianoche (ej. 22 a 6).
            return
                hour >= SessionStartHour ||
                hour < SessionEndHour;
        }


        // ============================================================
        // SPREAD
        // ============================================================

        private bool IsSpreadValid()
        {
            double spreadPips =
                (Symbol.Ask - Symbol.Bid) /
                Symbol.PipSize;


            if (spreadPips > MaximumSpreadPips)
            {
                Print(
                    "[NO TRADE] Spread={0:F2} pips | Max={1:F2}",
                    spreadPips,
                    MaximumSpreadPips);

                return false;
            }


            return true;
        }


        // ============================================================
        // OPEN POSITION
        // ============================================================

        private void OpenPosition(
            TradeType tradeType)
        {
            // --------------------------------------------------------
            // Verificar capital antes de abrir
            // --------------------------------------------------------

            if (!CheckCapitalProtection())
                return;


            // --------------------------------------------------------
            // Solo una posición
            // --------------------------------------------------------

            if (GetBotPosition() != null)
                return;


            // --------------------------------------------------------
            // Volume check
            // --------------------------------------------------------

            if (_volumeInUnits <
                Symbol.VolumeInUnitsMin)
            {
                Print(
                    "ERROR: Volume {0} < minimum {1}",
                    _volumeInUnits,
                    Symbol.VolumeInUnitsMin);

                return;
            }


            // --------------------------------------------------------
            // SL / TP
            //
            // ATR-based: escala con la volatilidad de entrada en vez
            // de usar una distancia fija dentro de una banda de ATR
            // de 6x (3-18 pips) — ver nota en el grupo "Risk".
            // --------------------------------------------------------

            double slPips =
                StopLossPips;

            double tpPips =
                TakeProfitPips;

            if (UseAtrBasedRisk)
            {
                double atrPips =
                    GetAtrPips();

                slPips =
                    atrPips *
                    SlAtrMultiple;

                tpPips =
                    atrPips *
                    TpAtrMultiple;
            }


            // --------------------------------------------------------
            // Execute
            // --------------------------------------------------------

            TradeResult result =
                ExecuteMarketOrder(
                    tradeType,
                    SymbolName,
                    _volumeInUnits,
                    BotLabel,
                    slPips,
                    tpPips);


            if (!result.IsSuccessful)
            {
                Print(
                    "[ORDER ERROR] {0}",
                    result.Error);

                return;
            }


            _barsSinceEntry = 0;


            Print("================================================");

            Print(
                "POSITION OPENED");

            Print(
                "Type: {0}",
                tradeType);

            Print(
                "Entry: {0}",
                result.Position.EntryPrice);

            Print(
                "Volume: {0}",
                result.Position.VolumeInUnits);

            Print(
                "SL: {0:F1} pips",
                slPips);

            Print(
                "TP: {0:F1} pips",
                tpPips);

            Print(
                "Equity: {0:F2}",
                Account.Equity);

            Print("================================================");
        }


        // ============================================================
        // POSITION MANAGEMENT
        // ============================================================

        private void ManagePosition(
            Position position)
        {
            _barsSinceEntry++;


            // --------------------------------------------------------
            // MaxBarsInTrade = 0
            // desactiva Time Stop.
            // --------------------------------------------------------

            if (MaxBarsInTrade <= 0)
                return;


            if (_barsSinceEntry <
                MaxBarsInTrade)
            {
                return;
            }


            Print(
                "[TIME STOP] Closing position after {0} M15 bars.",
                _barsSinceEntry);


            TradeResult result =
                ClosePosition(position);


            if (!result.IsSuccessful)
            {
                Print(
                    "[TIME STOP ERROR] {0}",
                    result.Error);
            }
        }


        // ============================================================
        // GET BOT POSITION
        // ============================================================

        private Position GetBotPosition()
        {
            Position[] positions =
                Positions.FindAll(
                    BotLabel,
                    SymbolName);


            if (positions.Length == 0)
                return null;


            return positions[0];
        }


        // ============================================================
        // POSITION CLOSED
        // ============================================================

        private void OnPositionClosed(
            PositionClosedEventArgs args)
        {
            if (
                args.Position.Label != BotLabel ||
                args.Position.SymbolName != SymbolName)
            {
                return;
            }


            _barsSinceEntry = 0;


            Print("================================================");

            Print(
                "POSITION CLOSED");

            Print(
                "Type: {0}",
                args.Position.TradeType);

            Print(
                "Net Profit: {0:F2}",
                args.Position.NetProfit);

            Print(
                "Pips: {0:F2}",
                args.Position.Pips);

            Print(
                "Balance: {0:F2}",
                Account.Balance);

            Print(
                "Equity: {0:F2}",
                Account.Equity);

            Print("================================================");
        }


        // ============================================================
        // CAPITAL PROTECTION
        //
        // ESTA ES LA ÚNICA PARADA AUTOMÁTICA DEL BOT.
        // ============================================================

        private bool CheckCapitalProtection()
        {
            if (_capitalProtectionTriggered)
                return false;


            if (Account.Equity >
                _capitalStopLevel)
            {
                return true;
            }


            _capitalProtectionTriggered =
                true;


            Print("================================================");
            Print("CAPITAL PROTECTION TRIGGERED");
            Print("================================================");

            Print(
                "Capital Reference: {0:F2}",
                _capitalReference);

            Print(
                "Current Equity: {0:F2}",
                Account.Equity);

            Print(
                "Stop Level: {0:F2}",
                _capitalStopLevel);

            Print(
                "Loss: {0:F2}%",
                MaximumCapitalLossPercent);

            Print(
                "Closing bot positions...");


            // --------------------------------------------------------
            // Cerrar cualquier posición del bot
            // --------------------------------------------------------

            Position[] positions =
                Positions.FindAll(
                    BotLabel,
                    SymbolName);


            foreach (
                Position position in positions)
            {
                TradeResult closeResult =
                    ClosePosition(position);


                if (!closeResult.IsSuccessful)
                {
                    Print(
                        "[CAPITAL STOP CLOSE ERROR] Position={0} Error={1}",
                        position.Id,
                        closeResult.Error);
                }
            }


            Print(
                "Bot stopped because maximum capital loss was reached.");

            Print("================================================");


            // ÚNICO Stop() automático de todo el código.
            Stop();


            return false;
        }


        // ============================================================
        // STOP
        // ============================================================

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;


            Print("================================================");
            Print("EURUSD PULLBACK V3 M15 STOPPED");
            Print("Balance: {0:F2}", Account.Balance);
            Print("Equity: {0:F2}", Account.Equity);
            Print("================================================");
        }
    }
}