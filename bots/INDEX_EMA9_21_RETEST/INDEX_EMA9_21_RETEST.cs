using System;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class IndexEma921Retest : Robot
    {
        private enum SetupState
        {
            None,
            LongArmed,
            ShortArmed
        }

        [Parameter("Label", DefaultValue = "INDEX_EMA9_21_RETEST", Group = "General")]
        public string BotLabel { get; set; }

        [Parameter("Volume (Lots)", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group = "Trading")]
        public double VolumeInLots { get; set; }

        [Parameter("Allow Long", DefaultValue = true, Group = "Trading")]
        public bool AllowLong { get; set; }

        [Parameter("Allow Short", DefaultValue = true, Group = "Trading")]
        public bool AllowShort { get; set; }

        [Parameter("EMA Fast", DefaultValue = 9, MinValue = 2, Group = "Trend")]
        public int EmaFastPeriod { get; set; }

        [Parameter("EMA Slow", DefaultValue = 21, MinValue = 2, Group = "Trend")]
        public int EmaSlowPeriod { get; set; }

        [Parameter("EMA Macro", DefaultValue = 200, MinValue = 20, Group = "Trend")]
        public int EmaMacroPeriod { get; set; }

        [Parameter("Slope Bars", DefaultValue = 3, MinValue = 1, MaxValue = 10, Group = "Trend")]
        public int SlopeBars { get; set; }

        [Parameter("Impulse ATR Multiple", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1, Group = "Pullback")]
        public double ImpulseAtrMultiple { get; set; }

        [Parameter("Impulse Lookback", DefaultValue = 5, MinValue = 1, MaxValue = 20, Group = "Pullback")]
        public int ImpulseLookback { get; set; }

        [Parameter("Pullback ATR Tolerance", DefaultValue = 0.3, MinValue = 0.05, Step = 0.05, Group = "Pullback")]
        public double PullbackAtrTolerance { get; set; }

        [Parameter("Max Setup Bars", DefaultValue = 5, MinValue = 1, MaxValue = 10, Group = "Pullback")]
        public int MaxSetupBars { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2, Group = "Volatility")]
        public int AtrPeriod { get; set; }

        [Parameter("Use ATR Filter", DefaultValue = false, Group = "Volatility")]
        public bool UseAtrFilter { get; set; }

        [Parameter("Minimum ATR (Pips)", DefaultValue = 1.0, MinValue = 0.0, Step = 0.5, Group = "Volatility")]
        public double MinAtrPips { get; set; }

        [Parameter("Maximum ATR (Pips)", DefaultValue = 40.0, MinValue = 1.0, Step = 0.5, Group = "Volatility")]
        public double MaxAtrPips { get; set; }

        [Parameter("Max Spread (Pips)", DefaultValue = 2.0, MinValue = 0.1, Step = 0.1, Group = "Volatility")]
        public double MaxSpreadPips { get; set; }

        [Parameter("SL ATR Multiple", DefaultValue = 1.2, MinValue = 0.1, Step = 0.1, Group = "Risk")]
        public double SlAtrMultiple { get; set; }

        [Parameter("Risk Reward", DefaultValue = 1.8, MinValue = 1.0, Step = 0.1, Group = "Risk")]
        public double RiskReward { get; set; }

        [Parameter("Max Bars In Trade", DefaultValue = 8, MinValue = 1, MaxValue = 100, Group = "Risk")]
        public int MaxBarsInTrade { get; set; }

        [Parameter("Use Macro Filter", DefaultValue = true, Group = "Trend")]
        public bool UseMacroFilter { get; set; }

        [Parameter("Timeframe Filter", DefaultValue = "M5-H1", Group = "Session")]
        public string TimeframeFilter { get; set; }

        [Parameter("Use Session Filter", DefaultValue = true, Group = "Session")]
        public bool UseSessionFilter { get; set; }

        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaSlow;
        private ExponentialMovingAverage _emaMacro;
        private AverageTrueRange _atr;
        private SetupState _setupState = SetupState.None;
        private int _setupBars = 0;

        protected override void OnStart()
        {
            _emaFast = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaFastPeriod);
            _emaSlow = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaSlowPeriod);
            _emaMacro = Indicators.ExponentialMovingAverage(Bars.ClosePrices, EmaMacroPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            Print("Index EMA 9/21 Retest started for " + SymbolName + " on " + TimeFrame);
        }

        protected override void OnBar()
        {
            if (Bars.ClosePrices.Count < Math.Max(EmaMacroPeriod, AtrPeriod) + 5)
                return;

            if (UseSessionFilter && !IsWithinSession())
                return;

            ManageExistingPositions();

            if (Positions.FindAll(BotLabel, SymbolName).Length > 0)
                return;

            if (!IsValidMarketContext())
                return;

            var trend = IsBullishTrend();
            var currentBar = Bars.ClosePrices.Count - 1;

            if (_setupState == SetupState.None)
            {
                if (trend && IsLongArmCondition(currentBar))
                {
                    if (IsLongConfirmation(currentBar))
                    {
                        OpenTrade(TradeType.Buy);
                        Print("Long confirmation met on same bar -> Buy");
                    }
                    else
                    {
                        _setupState = SetupState.LongArmed;
                        _setupBars = 0;
                        Print("Long setup armed");
                    }
                }
                else if (trend && IsShortArmCondition(currentBar))
                {
                    if (IsShortConfirmation(currentBar))
                    {
                        OpenTrade(TradeType.Sell);
                        Print("Short confirmation met on same bar -> Sell");
                    }
                    else
                    {
                        _setupState = SetupState.ShortArmed;
                        _setupBars = 0;
                        Print("Short setup armed");
                    }
                }
            }
            else
            {
                if (!trend || HasTrendFlip())
                {
                    _setupState = SetupState.None;
                    _setupBars = 0;
                    Print("Setup invalidated by trend flip/reset");
                    return;
                }

                _setupBars++;
                if (_setupBars > MaxSetupBars)
                {
                    _setupState = SetupState.None;
                    _setupBars = 0;
                    Print("Setup expired");
                    return;
                }

                if (_setupState == SetupState.LongArmed)
                {
                    if (IsLongConfirmation(currentBar))
                    {
                        OpenTrade(TradeType.Buy);
                        _setupState = SetupState.None;
                        _setupBars = 0;
                        Print("Long confirmation met -> Buy");
                    }
                }
                else if (_setupState == SetupState.ShortArmed)
                {
                    if (IsShortConfirmation(currentBar))
                    {
                        OpenTrade(TradeType.Sell);
                        _setupState = SetupState.None;
                        _setupBars = 0;
                        Print("Short confirmation met -> Sell");
                    }
                }
            }
        }

        private void ManageExistingPositions()
        {
            foreach (var position in Positions.FindAll(BotLabel, SymbolName))
            {
                if (position.Pips >= 0)
                    continue;

                var timeStop = position.EntryTime.AddMinutes(60 * MaxBarsInTrade);
                if (Server.Time < timeStop)
                    continue;

                var result = ClosePosition(position);
                if (result.IsSuccessful)
                    Print("Position closed by time stop: " + position.Id);
            }
        }

        private bool IsValidMarketContext()
        {
            if (!UseAtrFilter)
                return true;

            var atrValue = _atr.Result.Last(1);
            var atrPips = atrValue / Math.Max(Symbol.TickSize, Symbol.PipSize);
            return atrPips >= MinAtrPips && atrPips <= MaxAtrPips;
        }

        private bool IsBullishTrend()
        {
            var emaFast = _emaFast.Result.Last(1);
            var emaSlow = _emaSlow.Result.Last(1);
            var emaMacro = _emaMacro.Result.Last(1);
            var emaSlowSlope = GetSlope(_emaSlow.Result, SlopeBars);

            var bullish = emaFast >= emaSlow;
            var macroTrend = !UseMacroFilter || emaSlow >= emaMacro;
            var slopeOk = emaSlowSlope >= 0;
            return bullish && macroTrend && slopeOk;
        }

        private bool HasTrendFlip()
        {
            var emaFast = _emaFast.Result.Last(1);
            var emaSlow = _emaSlow.Result.Last(1);
            return emaFast <= emaSlow;
        }

        private bool IsLongArmCondition(int barIndex)
        {
            if (!AllowLong)
                return false;

            var atrPips = _atr.Result.Last(1) * Symbol.PipSize / Symbol.TickSize;
            var spreadPips = Symbol.Spread / Symbol.PipSize;
            if (spreadPips > MaxSpreadPips)
                return false;

            var impulseOk = false;
            var closeCurrent = Bars.ClosePrices.Last(1);
            for (int i = 1; i <= ImpulseLookback; i++)
            {
                var closeBar = Bars.ClosePrices.Last(i + 1);
                var emaSlowBar = _emaSlow.Result.Last(i + 1);
                var atrBar = _atr.Result.Last(i + 1);
                if ((closeBar - emaSlowBar) >= ImpulseAtrMultiple * atrBar)
                {
                    impulseOk = true;
                    break;
                }
            }

            var low = Bars.LowPrices.Last(1);
            var emaFast = _emaFast.Result.Last(1);
            var emaSlowCurrent = _emaSlow.Result.Last(1);
            var atrCurrent = _atr.Result.Last(1);
            var pullbackOk = low <= emaFast + PullbackAtrTolerance * atrCurrent && closeCurrent >= emaSlowCurrent - PullbackAtrTolerance * atrCurrent;
            return impulseOk && pullbackOk;
        }

        private bool IsShortArmCondition(int barIndex)
        {
            if (!AllowShort)
                return false;

            var atrPips = _atr.Result.Last(1) * Symbol.PipSize / Symbol.TickSize;
            var spreadPips = Symbol.Spread / Symbol.PipSize;
            if (spreadPips > MaxSpreadPips)
                return false;

            var impulseOk = false;
            var closeCurrent = Bars.ClosePrices.Last(1);
            for (int i = 1; i <= ImpulseLookback; i++)
            {
                var closeBar = Bars.ClosePrices.Last(i + 1);
                var emaSlowBar = _emaSlow.Result.Last(i + 1);
                var atrBar = _atr.Result.Last(i + 1);
                if ((emaSlowBar - closeBar) >= ImpulseAtrMultiple * atrBar)
                {
                    impulseOk = true;
                    break;
                }
            }

            var low = Bars.LowPrices.Last(1);
            var emaFast = _emaFast.Result.Last(1);
            var emaSlowCurrent = _emaSlow.Result.Last(1);
            var atrCurrent = _atr.Result.Last(1);
            var pullbackOk = low <= emaFast + PullbackAtrTolerance * atrCurrent && closeCurrent >= emaSlowCurrent - PullbackAtrTolerance * atrCurrent;
            return impulseOk && pullbackOk;
        }

        private bool IsLongConfirmation(int barIndex)
        {
            var close = Bars.ClosePrices.Last(1);
            var open = Bars.OpenPrices.Last(1);
            var prevClose = Bars.ClosePrices.Last(2);
            var emaFastCurrent = _emaFast.Result.Last(1);
            var emaSlowCurrent = _emaSlow.Result.Last(1);
            var spreadPips = Symbol.Spread / Symbol.PipSize;
            var atrPips = _atr.Result.Last(1) * Symbol.PipSize / Symbol.TickSize;
            var atrOk = !UseAtrFilter || (atrPips >= MinAtrPips && atrPips <= MaxAtrPips);
            var bullishBar = close > open && close > prevClose;
            var trendAligned = close > emaFastCurrent && close > emaSlowCurrent;
            return bullishBar && trendAligned && spreadPips <= MaxSpreadPips && atrOk;
        }

        private bool IsShortConfirmation(int barIndex)
        {
            var close = Bars.ClosePrices.Last(1);
            var open = Bars.OpenPrices.Last(1);
            var prevClose = Bars.ClosePrices.Last(2);
            var emaFastCurrent = _emaFast.Result.Last(1);
            var emaSlowCurrent = _emaSlow.Result.Last(1);
            var spreadPips = Symbol.Spread / Symbol.PipSize;
            var atrPips = _atr.Result.Last(1) * Symbol.PipSize / Symbol.TickSize;
            var atrOk = !UseAtrFilter || (atrPips >= MinAtrPips && atrPips <= MaxAtrPips);
            var bearishBar = close < open && close < prevClose;
            var trendAligned = close < emaFastCurrent && close < emaSlowCurrent;
            return bearishBar && trendAligned && spreadPips <= MaxSpreadPips && atrOk;
        }

        private double GetSlope(IndicatorDataSeries series, int bars)
        {
            if (series.Count < bars + 1)
                return 0;

            var value1 = series.Last(bars);
            var value2 = series.Last(bars + 1);
            return value1 - value2;
        }

        private bool IsWithinSession()
        {
            if (!UseSessionFilter)
                return true;

            var utc = Server.TimeInUtc.AddHours(0).TimeOfDay;
            return utc >= new TimeSpan(7, 0, 0) && utc <= new TimeSpan(20, 0, 0);
        }

        private void OpenTrade(TradeType type)
        {
            var volumeInUnits = Symbol.QuantityToVolumeInUnits(VolumeInLots);
            var slPips = SlAtrMultiple * 10.0;
            var tpPips = slPips * RiskReward;

            var result = ExecuteMarketOrder(type, SymbolName, volumeInUnits, BotLabel, slPips, tpPips);
            if (result.IsSuccessful)
            {
                Print("Trade opened: " + type + " with " + VolumeInLots + " lots. SL=" + slPips + "p TP=" + tpPips + "p");
            }
            else
            {
                Print("Open order error: " + result.Error);
            }
        }

        protected override void OnStop()
        {
        }
    }
}
