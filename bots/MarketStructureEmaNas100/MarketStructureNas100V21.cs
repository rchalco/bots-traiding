using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    // V2.1: V2.0 entries unchanged; optional net-profit trailing with break-even floor. Demo/backtest only.
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MarketStructureNas100V21 : Robot
    {
        [Parameter("EMA tendencia M5", DefaultValue = 200, MinValue = 2, MaxValue = 500)]
        public int TrendPeriod { get; set; }
        [Parameter("Pivote M15: velas por lado", DefaultValue = 2, MinValue = 1, MaxValue = 5)]
        public int PivotWidth { get; set; }
        [Parameter("Buffer SL (pips)", DefaultValue = 2.0, MinValue = 0)]
        public double StopBufferPips { get; set; }
        [Parameter("Lotaje fijo (lotes)", DefaultValue = 0.20, MinValue = 0.01, Step = 0.01, Group = "Lotaje")]
        public double FixedLots { get; set; }
        [Parameter("Riesgo max por operacion (%) - modo protegido", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 2)]
        public double RiskPct { get; set; }
        [Parameter("Tope monetario fijo / maximo dinamico", DefaultValue = 4.0, MinValue = 0.01, Step = 0.1, Group = "Salida monetaria")]
        public double MaxLossMoney { get; set; }
        [Parameter("Salida monetaria dinamica", DefaultValue = false, Group = "Salida monetaria")]
        public bool DynamicMoneyStop { get; set; }
        [Parameter("Minimo monetario dinamico", DefaultValue = 2.0, MinValue = 0.01, Step = 0.1, Group = "Salida monetaria")]
        public double MinLossMoney { get; set; }
        [Parameter("Periodo rango verdadero M5", DefaultValue = 14, MinValue = 2, MaxValue = 100, Group = "Salida monetaria")]
        public int MoneyAtrPeriod { get; set; }
        [Parameter("Multiplicador rango verdadero", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.1, Group = "Salida monetaria")]
        public double MoneyAtrMultiplier { get; set; }
        [Parameter("Tope dinamico capital (%)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 5.0, Step = 0.05, Group = "Salida monetaria")]
        public double MoneyEquityCapPct { get; set; }
        [Parameter("Beneficio / riesgo", DefaultValue = 1.5, MinValue = 1, Step = 0.1)]
        public double RewardRisk { get; set; }
        [Parameter("Limite diario perdidas (%)", DefaultValue = 2.0, MinValue = 0.1, MaxValue = 5)]
        public double DailyLossPct { get; set; }
        [Parameter("Max operaciones diarias", DefaultValue = 3, MinValue = 1, MaxValue = 20)]
        public int MaxTrades { get; set; }
        [Parameter("Spread maximo (pips)", DefaultValue = 3.0, MinValue = 0.1)]
        public double MaxSpreadPips { get; set; }
        [Parameter("Deslizamiento maximo (pips)", DefaultValue = 2.0, MinValue = 0.1)]
        public double SlippagePips { get; set; }
        [Parameter("Solo senales (sin ordenes)", DefaultValue = false)]
        public bool SignalsOnly { get; set; }

        [Parameter("Ignorar tope riesgo solo backtest", DefaultValue = false, Group = "Pruebas")]
        public bool IgnoreRiskInBacktest { get; set; }
        [Parameter("Prueba tecnica una orden", DefaultValue = false, Group = "Pruebas")]
        public bool SmokeTest { get; set; }

        [Parameter("BOS minimo (pips, 0 desactiva)", DefaultValue = 10.0, MinValue = 0, Group = "Calidad BOS")]
        public double MinBreakoutPips { get; set; }
        [Parameter("BOS maximo (pips, 0 desactiva)", DefaultValue = 80.0, MinValue = 0, Group = "Calidad BOS")]
        public double MaxBreakoutPips { get; set; }
        [Parameter("SL monetario adicional en servidor", DefaultValue = false, Group = "Salida monetaria")]
        public bool BrokerMoneyStop { get; set; }

        [Parameter("Activar trailing monetario", DefaultValue = true, Group = "Trailing monetario")]
        public bool EnableProfitLock { get; set; }
        [Parameter("Activacion (moneda cuenta)", DefaultValue = 4.0, MinValue = 0.01, Step = 0.5, Group = "Trailing monetario")]
        public double ProfitLockActivation { get; set; }
        [Parameter("Beneficio minimo protegido", DefaultValue = 0.5, MinValue = 0, Step = 0.1, Group = "Trailing monetario")]
        public double MinimumProtectedProfit { get; set; }
        [Parameter("Retroceso desde maximo", DefaultValue = 4.0, MinValue = 0.01, Step = 0.5, Group = "Trailing monetario")]
        public double ProfitGiveback { get; set; }

        private const string Label = "MS_NAS100_V21";
        private const int StructureLookback = 120;
        private const int StopLookback = 36;
        private const double CostReserve = 0.10;
        private Bars _m15;
        private ExponentialMovingAverage _ema;
        private DateTime _lastProcessed = DateTime.MinValue;
        private DateTime _lastStructure = DateTime.MinValue;
        private DateTime _usedHigh = DateTime.MinValue, _usedLow = DateTime.MinValue;
        private Swing _high, _low, _previousHigh, _previousLow;
        private int _bars, _bos, _technical, _eligible, _orders;
        private bool _ready, _historical, _diagnostic, _smokeDone;
        private int _attempts, _brokerRejects, _protectedOrders, _smokeChecks;
        private double _fixedVolume;
        // Once the threshold is breached, keep trying to close even if the quote recovers.
        private readonly Dictionary<int, DateTime> _moneyExitRetry = new Dictionary<int, DateTime>();
        // Immutable entry-time thresholds. After a restart, recover conservatively.
        private readonly Dictionary<int, double> _moneyLimits = new Dictionary<int, double>();
        private int _moneyExitTriggers, _moneyExitRequests, _moneyExitAccepted, _moneyExitErrors;
        private readonly Dictionary<int, double> _peakNet = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _lockedNet = new Dictionary<int, double>();
        private readonly HashSet<int> _profitExitPending = new HashSet<int>();
        private int _lockActivations, _lockTriggers, _lockRequests, _lockAccepted, _lockErrors;
        private readonly Dictionary<string, int> _rejects = new Dictionary<string, int>();

        private sealed class Swing
        {
            public DateTime Time;
            public double Price;
        }

        protected override void OnStart()
        {
            if (EnableProfitLock && (!Finite(ProfitLockActivation) || ProfitLockActivation <= 0 ||
                !Finite(MinimumProtectedProfit) || MinimumProtectedProfit < 0 || MinimumProtectedProfit >= ProfitLockActivation ||
                !Finite(ProfitGiveback) || ProfitGiveback <= 0))
            { Print("ERROR: parametros trailing invalidos; piso < activacion y retroceso > 0."); Stop(); return; }
            if (!Finite(MinBreakoutPips) || !Finite(MaxBreakoutPips) || MinBreakoutPips < 0 ||
                MaxBreakoutPips < 0 || (MaxBreakoutPips > 0 && MaxBreakoutPips <= MinBreakoutPips))
            { Print("ERROR: rango BOS invalido."); Stop(); return; }
            if (!Finite(MaxLossMoney) || MaxLossMoney <= 0)
            { Print("ERROR: perdida monetaria debe ser positiva."); Stop(); return; }
            if (DynamicMoneyStop && (!Finite(MinLossMoney) || MinLossMoney <= 0 || MinLossMoney > MaxLossMoney ||
                MoneyAtrPeriod < 2 || !Finite(MoneyAtrMultiplier) || MoneyAtrMultiplier <= 0 ||
                !Finite(MoneyEquityCapPct) || MoneyEquityCapPct <= 0 || MoneyEquityCapPct > 5))
            { Print("ERROR: parametros de salida dinamica invalidos."); Stop(); return; }
            if (TimeFrame != TimeFrame.Minute5 || RiskPct > DailyLossPct)
            { Print("ERROR: usar M5 y riesgo <= limite diario."); Stop(); return; }
            if (Account.IsLive && RunningMode == RunningMode.RealTime)
            { Print("ERROR: V2.1 solo permite demo y backtesting."); Stop(); return; }
            _historical = RunningMode == RunningMode.SilentBacktesting || RunningMode == RunningMode.VisualBacktesting || RunningMode == RunningMode.Optimization;
            _diagnostic = _historical && IgnoreRiskInBacktest;
            if (!_historical && (IgnoreRiskInBacktest || SmokeTest))
            { Print("ERROR: perfil diagnostico/prueba exclusivo de backtesting. Cargar perfil protegido."); Stop(); return; }
            if (SmokeTest && (!_diagnostic || SignalsOnly))
            { Print("ERROR: prueba tecnica requiere diagnostico historico y ordenes habilitadas."); Stop(); return; }
            {
                // The parameter is in lots, not units. Never silently change the requested lot size.
                double requested = Symbol.QuantityToVolumeInUnits(FixedLots);
                double normalized = Symbol.NormalizeVolumeInUnits(requested, RoundingMode.ToNearest);
                double tolerance = Math.Max(1e-10, Symbol.VolumeInUnitsStep * 1e-7);
                if (!Finite(requested) || !Finite(normalized) || requested <= 0 ||
                    requested < Symbol.VolumeInUnitsMin - tolerance || requested > Symbol.VolumeInUnitsMax + tolerance ||
                    normalized < Symbol.VolumeInUnitsMin - tolerance || normalized > Symbol.VolumeInUnitsMax + tolerance ||
                    Math.Abs(normalized - requested) > tolerance)
                {
                    Print("ERROR: lotaje fijo {0} invalido. Min={1} Max={2} Paso={3} lotes.", FixedLots,
                        Symbol.VolumeInUnitsToQuantity(Symbol.VolumeInUnitsMin),
                        Symbol.VolumeInUnitsToQuantity(Symbol.VolumeInUnitsMax),
                        Symbol.VolumeInUnitsToQuantity(Symbol.VolumeInUnitsStep));
                    Stop(); return;
                }
                _fixedVolume = normalized;
            }
            _m15 = MarketData.GetBars(TimeFrame.Minute15, SymbolName);
            for (int k = 0; k < 30 && Bars.Count < TrendPeriod * 3 + 5; k++)
                if (Bars.LoadMoreHistory() == 0) break;
            for (int k = 0; k < 30 && _m15.Count < StructureLookback + 2 * PivotWidth + 5; k++)
                if (_m15.LoadMoreHistory() == 0) break;
            _ema = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendPeriod);
            _ready = true;
            Print("CONFIG V2.1 | M15 niveles / M5 BOS + EMA{0} | SignalsOnly={1} | RiskPct={2} | RR={3} | saldo={4}", TrendPeriod, SignalsOnly, RiskPct, RewardRisk, Account.Balance);
            Print("LOTAJE | FIJO={0} lotes | unidades={1} | modo={2} | diagnostico={3} | smoke={4}", FixedLots, _fixedVolume, RunningMode, _diagnostic, SmokeTest);
            Print("MARGEN ESTIMADO inicial | BUY={0:F2} | SELL={1:F2} | libre={2:F2}", Symbol.GetEstimatedMargin(TradeType.Buy, _fixedVolume), Symbol.GetEstimatedMargin(TradeType.Sell, _fixedVolume), Account.FreeMargin);
            if (_diagnostic) Print("AVISO: solo historico; sin tope monetario por orden ni reserva diaria prospectiva. Puede superar limite diario al cerrar una operacion.");
            Print("SALIDA MONETARIA | perdida neta por posicion={0:F2} (moneda cuenta) | OnTick | TP estructural conservado", MaxLossMoney);
            Print("MONEY_CONFIG | dinamico={0} | minimo={1:F2} | maximo={2:F2} | TRperiod={3} | multiplicador={4:F2} | capitalPct={5:F2}", DynamicMoneyStop, MinLossMoney, MaxLossMoney, MoneyAtrPeriod, MoneyAtrMultiplier, MoneyEquityCapPct);
            Print("BOS_CONFIG | minimo={0:F2} | maximo={1:F2} | serverMoneySL={2}", MinBreakoutPips, MaxBreakoutPips, BrokerMoneyStop);
            Print("TRAIL_CONFIG | activo={0} | activacion={1:F2} | piso={2:F2} | retroceso={3:F2} | virtual OnTick", EnableProfitLock, ProfitLockActivation, MinimumProtectedProfit, ProfitGiveback);
            if (EnableProfitLock && Positions.FindAll(Label, SymbolName).Length > 0)
                Print("TRAIL_RECOVERY | posiciones previas detectadas; maximo historico no recuperable; se inicia desde neto actual. SL servidor original conservado.");
            Print("CONTRATO pip={0} tick={1} minUnits={2} minLots={3} | reserva costos=10% | cuenta real bloqueada", Symbol.PipSize, Symbol.TickSize, Symbol.VolumeInUnitsMin, Symbol.VolumeInUnitsToQuantity(Symbol.VolumeInUnitsMin));
        }

        protected override void OnTick()
        {
            CheckMoneyExit();
        }

        private void CheckMoneyExit()
        {
            if (!_ready || SignalsOnly) return;
            // Scope to this strategy version and chart symbol; never close manual trades.
            var owned = Positions.FindAll(Label, SymbolName);
            foreach (int id in _moneyLimits.Keys.ToArray())
                if (!owned.Any(p => p.Id == id)) _moneyLimits.Remove(id);
            foreach (int id in _peakNet.Keys.ToArray())
                if (!owned.Any(p => p.Id == id)) _peakNet.Remove(id);
            foreach (int id in _lockedNet.Keys.ToArray())
                if (!owned.Any(p => p.Id == id)) _lockedNet.Remove(id);
            foreach (int id in _moneyExitRetry.Keys.ToArray())
                if (!owned.Any(p => p.Id == id)) _moneyExitRetry.Remove(id);
            _profitExitPending.RemoveWhere(id => !owned.Any(p => p.Id == id));
            foreach (var position in owned)
            {
                double limit;
                if (!_moneyLimits.TryGetValue(position.Id, out limit))
                {
                    // A restarted instance has lost its in-memory snapshot. Never grant
                    // a larger threshold than the dynamic floor or current capital cap.
                    limit = DynamicMoneyStop
                        ? Math.Max(0, Math.Min(MinLossMoney, Math.Min(MaxLossMoney,
                            Math.Min(Account.Balance, Account.Equity) * MoneyEquityCapPct / 100.0)))
                        : MaxLossMoney;
                    _moneyLimits[position.Id] = limit;
                    Print("MONEY_RECOVERY | id={0} | limite conservador={1:F2}", position.Id, limit);
                }
                double net = position.NetProfit;
                DateTime retryAt;
                bool pending = _moneyExitRetry.TryGetValue(position.Id, out retryAt);
                if (!pending)
                {
                    if (!Finite(net)) continue;
                    // A previously activated profit lock takes priority over a new loss trigger.
                    double floor;
                    bool trailHit = UpdateProfitLock(position.Id, net, out floor);
                    if (trailHit)
                    {
                        _profitExitPending.Add(position.Id);
                        _lockTriggers++;
                        Print("TRAIL_TRIGGER | id={0} | {1} | net={2:F2} | peak={3:F2} | floor={4:F2}",
                            position.Id, position.TradeType, net, _peakNet[position.Id], floor);
                    }
                    else if (net <= -limit)
                    {
                        _moneyExitTriggers++;
                        Print("MONEY_STOP_TRIGGER | id={0} | {1} | net={2:F2} | limite={3:F2}",
                            position.Id, position.TradeType, net, -limit);
                    }
                    else continue;
                }
                else if (Server.Time < retryAt) continue;

                // Latch the exit reason across retries even if the quote recovers.
                bool profitExit = _profitExitPending.Contains(position.Id);
                _moneyExitRetry[position.Id] = Server.Time.AddSeconds(1);
                if (profitExit) _lockRequests++; else _moneyExitRequests++;
                var result = ClosePosition(position);
                if (result.IsSuccessful)
                {
                    if (profitExit) _lockAccepted++; else _moneyExitAccepted++;
                    _moneyExitRetry.Remove(position.Id);
                    _moneyLimits.Remove(position.Id);
                    _profitExitPending.Remove(position.Id);
                    _peakNet.Remove(position.Id);
                    _lockedNet.Remove(position.Id);
                    Print("{0} | id={1} | net observado antes del cierre={2:F2} | consultar History para neto realizado",
                        profitExit ? "TRAIL_CLOSED" : "MONEY_STOP_CLOSED", position.Id, net);
                }
                else
                {
                    if (profitExit) _lockErrors++; else _moneyExitErrors++;
                    Print("{0} | id={1} | error={2} | reintento con siguiente tick elegible",
                        profitExit ? "TRAIL_RETRY" : "MONEY_STOP_RETRY", position.Id, result.Error);
                }
            }
        }

        // Net profit in account currency; no extra indicators or volume calculation.
        // A lock is never lowered. The original broker SL and TP are not modified.
        private bool UpdateProfitLock(int id, double net, out double floor)
        {
            floor = double.NegativeInfinity;
            if (!EnableProfitLock) return false;
            double peak;
            if (!_peakNet.TryGetValue(id, out peak)) peak = net;
            peak = Math.Max(peak, net);
            _peakNet[id] = peak;
            if (peak < ProfitLockActivation) return false;
            double candidate = Math.Max(MinimumProtectedProfit, peak - ProfitGiveback);
            double previous;
            if (!_lockedNet.TryGetValue(id, out previous))
            {
                _lockActivations++;
                floor = candidate;
                Print("TRAIL_ARMED | id={0} | peak={1:F2} | floor={2:F2}", id, peak, floor);
            }
            else floor = Math.Max(previous, candidate);
            _lockedNet[id] = floor;
            return net <= floor;
        }

        protected override void OnBar()
        {
            if (!_ready) return;
            int i = Bars.Count - 2; // last closed M5 candle, not the newly opened candle
            if (i < TrendPeriod * 3 || i < StopLookback + 3) return;
            DateTime start = Bars.OpenTimes[i];
            if (start <= _lastProcessed) return;
            _lastProcessed = start;
            _bars++;
            // Smoke test deliberately ignores structure/EMA; never runs outside historical mode.
            // Allow up to 10 eligible bar checks, at most one successfully opened order.
            if (SmokeTest)
            {
                if (_smokeDone || _smokeChecks >= 10) return;
                _smokeChecks++;
                Print("SMOKE CHECK {0}/10 | prueba tecnica, NO senal de estrategia", _smokeChecks);
                TryEnter(TradeType.Buy, i, 0, 0, true);
                if (_orders > 0) _smokeDone = true;
                return;
            }
            // Use only M15 candles already closed at the START of the signal M5 bar.
            // A pivot first confirmed during the signal candle cannot trigger that candle.
            int j = LastClosedAt(_m15, start, TimeSpan.FromMinutes(15));
            if (j < 2 * PivotWidth + 2) return;
            if (_m15.OpenTimes[j] != _lastStructure)
            {
                _lastStructure = _m15.OpenTimes[j];
                RefreshStructure(j);
            }
            if (_high == null || _low == null || _high.Price <= _low.Price) return;
            double previous = Bars.ClosePrices[i - 1], close = Bars.ClosePrices[i];
            bool up = previous <= _high.Price && close > _high.Price;
            bool down = previous >= _low.Price && close < _low.Price;
            if (!up && !down) return;
            bool buy = up;
            Swing level = buy ? _high : _low;
            _bos++;
            double ema = _ema.Result[i];
            Print("BOS {0} | nivel={1:F2} | cierre={2:F2} | EMA={3:F2} | estructura={4}", buy ? "BUY" : "SELL", level.Price, close, ema, DescribeStructure());
            if (!Finite(ema) || (buy ? close <= ema : close >= ema)) { Reject("EMA200"); return; }
            if ((buy ? _usedHigh : _usedLow) == level.Time) { Reject("nivel ya evaluado"); return; }
            // Evaluate displacement on closed signal candle, before consuming pivot.
            // A weak crossing does not consume the level; a later fresh crossing can qualify.
            double displacement = (buy ? close - level.Price : level.Price - close) / Symbol.PipSize;
            if (!Finite(displacement) || (MinBreakoutPips > 0 && displacement <= MinBreakoutPips + 1e-8))
            { Reject("BOS debil"); return; }
            if (MaxBreakoutPips > 0 && displacement > MaxBreakoutPips + 1e-8)
            { Reject("BOS extendido"); return; }
            Print("BOS_QUALITY | desplazamiento={0:F2} | estructura={1}", displacement, DescribeStructure());
            // One technical attempt per pivot in this run, even if risk blocks the order.
            if (buy) _usedHigh = level.Time; else _usedLow = level.Time;
            _technical++;
            Chart.DrawIcon(Label + "_signal_" + start.Ticks, buy ? ChartIconType.UpArrow : ChartIconType.DownArrow,
                start, buy ? Bars.LowPrices[i] : Bars.HighPrices[i], buy ? Color.Lime : Color.Red);
            TryEnter(buy ? TradeType.Buy : TradeType.Sell, i, level.Price, ema);
        }

        // Binary search: exclude any M15 candle whose scheduled close is after cutoff.
        private static int LastClosedAt(Bars bars, DateTime cutoff, TimeSpan duration)
        {
            int left = 0, right = bars.Count - 1, answer = -1;
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (bars.OpenTimes[mid] + duration <= cutoff) { answer = mid; left = mid + 1; }
                else right = mid - 1;
            }
            return answer;
        }

        private static List<Swing> FindSwings(Bars bars, int lastClosed, int width, int lookback, bool high, int limit)
        {
            var result = new List<Swing>();
            int first = Math.Max(width, lastClosed - lookback + 1);
            for (int p = lastClosed - width; p >= first && result.Count < limit; p--)
            {
                double price = high ? bars.HighPrices[p] : bars.LowPrices[p];
                bool valid = true;
                for (int k = 1; k <= width && valid; k++)
                {
                    // Strict pivots; equal highs/lows are not treated as new swings.
                    valid = high
                        ? price > bars.HighPrices[p - k] && price > bars.HighPrices[p + k]
                        : price < bars.LowPrices[p - k] && price < bars.LowPrices[p + k];
                }
                if (valid) result.Add(new Swing { Time = bars.OpenTimes[p], Price = price });
            }
            return result;
        }

        private void RefreshStructure(int j)
        {
            var highs = FindSwings(_m15, j, PivotWidth, StructureLookback, true, 2);
            var lows = FindSwings(_m15, j, PivotWidth, StructureLookback, false, 2);
            var oldHigh = _high; var oldLow = _low;
            _high = highs.FirstOrDefault(); _previousHigh = highs.Skip(1).FirstOrDefault();
            _low = lows.FirstOrDefault(); _previousLow = lows.Skip(1).FirstOrDefault();
            if (_high != null)
            {
                Chart.DrawHorizontalLine(Label + "_H", _high.Price, Color.Red);
                Chart.DrawText(Label + "_HT", HighLabel() + " M15", _high.Time, _high.Price, Color.Red);
            }
            else { Chart.RemoveObject(Label + "_H"); Chart.RemoveObject(Label + "_HT"); }
            if (_low != null)
            {
                Chart.DrawHorizontalLine(Label + "_L", _low.Price, Color.Lime);
                Chart.DrawText(Label + "_LT", LowLabel() + " M15", _low.Time, _low.Price, Color.Lime);
            }
            else { Chart.RemoveObject(Label + "_L"); Chart.RemoveObject(Label + "_LT"); }
            if (_high != null && _low != null && (oldHigh == null || oldLow == null || oldHigh.Time != _high.Time || oldLow.Time != _low.Time))
                Print("ESTRUCTURA M15 {0}={1:F2} | {2}={3:F2}", HighLabel(), _high.Price, LowLabel(), _low.Price);
        }

        private string HighLabel() { return _previousHigh == null ? "SH" : _high.Price > _previousHigh.Price ? "HH" : _high.Price < _previousHigh.Price ? "LH" : "EH"; }
        private string LowLabel() { return _previousLow == null ? "SL" : _low.Price > _previousLow.Price ? "HL" : _low.Price < _previousLow.Price ? "LL" : "EL"; }
        private string DescribeStructure() { return HighLabel() + "/" + LowLabel(); }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private double RoundUpPips(double pips) { return Math.Ceiling(pips * Symbol.PipSize / Symbol.TickSize) * Symbol.TickSize / Symbol.PipSize; }

        // Simple moving average of true range, in pips, using closed M5 bars only.
        // This is a volatility measure for exits, never a new technical entry filter.
        private double ClosedTrueRangePips(int i)
        {
            if (i - MoneyAtrPeriod + 1 < 1 || Symbol.PipSize <= 0) return double.NaN;
            double sum = 0;
            for (int k = i - MoneyAtrPeriod + 1; k <= i; k++)
            {
                double h = Bars.HighPrices[k], l = Bars.LowPrices[k], prev = Bars.ClosePrices[k - 1];
                double tr = Math.Max(h - l, Math.Max(Math.Abs(h - prev), Math.Abs(l - prev)));
                if (!Finite(tr) || tr < 0) return double.NaN;
                sum += tr;
            }
            return sum / MoneyAtrPeriod / Symbol.PipSize;
        }

        private double ComputeMoneyLimit(int i, double volume, double structuralRisk, double capital)
        {
            if (!DynamicMoneyStop) return MaxLossMoney; // exact fixed-stop control
            double trPips = ClosedTrueRangePips(i);
            double cap = Math.Max(0, Math.Min(MaxLossMoney, Math.Min(structuralRisk,
                capital * MoneyEquityCapPct / 100.0)));
            double raw = Finite(trPips) && trPips > 0
                ? Symbol.AmountRisked(volume, trPips * MoneyAtrMultiplier) : MinLossMoney;
            if (!Finite(raw) || raw <= 0) raw = MinLossMoney;
            // Caps take precedence over the floor. Freeze the result at entry.
            double limit = Math.Min(cap, Math.Max(MinLossMoney, raw));
            // Round down to cents, not up beyond the chosen cap.
            limit = Math.Floor(limit * 100.0 + 1e-9) / 100.0;
            Print("MONEY_CALC | TRpips={0:F2} | raw={1:F2} | cap={2:F2} | limite={3:F2} | riesgoSL={4:F2}", trPips, raw, cap, limit, structuralRisk);
            return limit;
        }

        private void TryEnter(TradeType side, int i, double level, double ema, bool smoke = false)
        {
            bool buy = side == TradeType.Buy;
            if (Positions.Any(p => p.SymbolName == SymbolName)) { Reject("posicion existente"); return; }
            double bid = Symbol.Bid, ask = Symbol.Ask, spread = ask - bid;
            if (!Finite(bid) || !Finite(ask) || bid <= 0 || spread < 0 || spread / Symbol.PipSize > MaxSpreadPips)
            { Reject("spread"); return; }
            // Bars are chart quotes; require current bid still on breakout side and EMA side.
            if (!smoke && (buy ? bid <= Math.Max(level, ema) : bid >= Math.Min(level, ema)))
            { Reject("precio retorno al nivel antes de ejecutar"); return; }
            double entry = buy ? ask : bid;
            double stop;
            if (smoke)
            {
                // Fixed synthetic stop for API connectivity test, not a strategy signal.
                stop = bid - 30 * Symbol.PipSize;
            }
            else
            {
                var swing = FindSwings(Bars, i, 1, StopLookback, !buy, 1).FirstOrDefault();
                if (swing == null) { Reject("sin pivote SL M5"); return; }
                double buffer = StopBufferPips * Symbol.PipSize;
                stop = buy ? Math.Min(swing.Price, Bars.LowPrices[i]) - buffer
                    : Math.Max(swing.Price, Bars.HighPrices[i]) + buffer + spread;
            }
            if (buy ? stop >= bid : stop <= ask) { Reject("SL estructural invalido"); return; }
            double worstEntry = buy ? entry + SlippagePips * Symbol.PipSize : entry - SlippagePips * Symbol.PipSize;
            double slPips = RoundUpPips(Math.Abs(worstEntry - stop) / Symbol.PipSize);
            double tpPips = RoundUpPips(slPips * RewardRisk);
            if (!Finite(slPips) || slPips <= 0 || !Finite(tpPips)) { Reject("distancia SL invalida"); return; }

            DateTime day = Server.Time.Date;
            var history = History.Where(h => h.Label == Label && h.SymbolName == SymbolName).ToArray();
            double losses = -history.Where(h => h.ClosingTime.Date == day && h.NetProfit < 0).Sum(h => h.NetProfit);
            double capital = Math.Min(Account.Balance, Account.Equity);
            double remaining = capital * DailyLossPct / 100.0 - losses;
            if (capital <= 0 || remaining <= 0 || history.Where(h => h.EntryTime.Date == day).Select(h => h.PositionId).Distinct().Count() >= MaxTrades)
            { Reject("limite diario"); return; }
            double budget = Math.Min(capital * RiskPct / 100.0, remaining) * (1 - CostReserve);
            double volume = _fixedVolume;
            double amount = Symbol.AmountRisked(volume, slPips);
            double margin = Symbol.GetEstimatedMargin(side, volume);
            Print("RIESGO {0} | SL={1:F2} | TP={2:F2} | riesgoSL={3:F2} | presupuestoSL={4:F2} | stopEstructural={5:F2} | diagnostico={6}", side, slPips, tpPips, amount, budget, stop, _diagnostic);
            Print("LOTAJE FIJO | lotes={0} | unidades={1} | margen={2:F2} | libre={3:F2} | riesgoPct={4:F2}", FixedLots, volume, margin, Account.FreeMargin, amount / capital * 100);
            if (!Finite(volume) || volume < Symbol.VolumeInUnitsMin || volume > Symbol.VolumeInUnitsMax || !Finite(amount) || amount <= 0)
            { Reject("lotaje/riesgo invalido"); return; }
            double moneyLimit = ComputeMoneyLimit(i, volume, amount, capital);
            if (!Finite(moneyLimit) || moneyLimit <= 0) { Reject("presupuesto salida monetaria invalido"); return; }

            // Optional closer server-side stop: never extend the structural stop.
            // The 10% reserve is approximate; gaps and execution may exceed the cash threshold.
            // Keep TP based on the original structural distance for a clean exit experiment.
            double orderSlPips = slPips;
            if (BrokerMoneyStop)
            {
                double riskPerPip = Symbol.AmountRisked(volume, 1.0);
                if (!Finite(riskPerPip) || riskPerPip <= 0) { Reject("valor pip invalido"); return; }
                double grossBudget = moneyLimit * (1 - CostReserve);
                if (!_diagnostic) grossBudget = Math.Min(grossBudget, budget);
                double rawPips = grossBudget / riskPerPip;
                double tickPips = Symbol.TickSize / Symbol.PipSize;
                double cappedPips = Math.Floor(rawPips / tickPips + 1e-9) * tickPips;
                orderSlPips = Math.Min(slPips, cappedPips);
                if (!Finite(orderSlPips) || orderSlPips <= spread / Symbol.PipSize)
                { Reject("SL monetario demasiado cerca del spread"); return; }
                Print("SERVER_SL | estructural={0:F2} | enviado={1:F2} | TP estructural={2:F2} | limite neto={3:F2}",
                    slPips, orderSlPips, tpPips, moneyLimit);
            }
            double orderRisk = Symbol.AmountRisked(volume, orderSlPips);
            if (!Finite(orderRisk) || orderRisk <= 0) { Reject("riesgo orden invalido"); return; }
            if (orderRisk > budget + 1e-8)
            {
                if (!_diagnostic) { Reject("tope riesgo modo protegido"); return; }
                Print("AVISO DIAGNOSTICO: riesgoSL={0:F2} supera presupuesto={1:F2}; permitido solo en backtest", orderRisk, budget);
            }
            if (!Finite(margin) || margin < 0 || Account.FreeMargin <= 0 || margin > Account.FreeMargin * 0.5)
            { Reject("margen insuficiente (max 50% libre)"); return; }
            _eligible++;
            Print("ELEGIBLE {0} | unidades={1} | lotes={2} | riesgoSL={3:F2} | margen={4:F2}", side, volume, Symbol.VolumeInUnitsToQuantity(volume), orderRisk, margin);
            if (SignalsOnly) return;
            _attempts++;
            Print("ORDER_REQUEST #{0} | {1} | lotes={2} | unidades={3} | SL={4:F2} | TP={5:F2}", _attempts, side, FixedLots, volume, orderSlPips, tpPips);
            var result = ExecuteMarketRangeOrder(side, SymbolName, volume, SlippagePips, entry, Label, orderSlPips, tpPips);
            if (!result.IsSuccessful) { _brokerRejects++; Reject("broker: " + result.Error); return; }
            _orders++;
            var pos = result.Position;
            if (pos == null) { Print("ERROR: respuesta sin posicion; detener y verificar."); Stop(); return; }
            Print("ORDEN {0} | id={1} | fill={2} | SL={3} | TP={4}", side, pos.Id, pos.EntryPrice, pos.StopLoss, pos.TakeProfit);
            // Relative SL/TP were attached atomically with the entry. If missing or risk
            // increased unexpectedly, attempt an emergency close and stop on failure.
            bool valid = pos.StopLoss.HasValue && pos.TakeProfit.HasValue;
            if (valid)
            {
                double actualPips = (buy ? pos.EntryPrice - pos.StopLoss.Value : pos.StopLoss.Value - pos.EntryPrice) / Symbol.PipSize;
                double actualRisk = Symbol.AmountRisked(pos.VolumeInUnits, actualPips);
                bool correctStop = BrokerMoneyStop
                    ? actualPips <= orderSlPips + Symbol.TickSize / Symbol.PipSize * 1e-5
                    : (buy ? pos.StopLoss.Value <= stop + Symbol.TickSize * 1e-6
                           : pos.StopLoss.Value >= stop - Symbol.TickSize * 1e-6);
                valid = actualPips > 0 && Finite(actualRisk) && (_diagnostic || actualRisk <= budget + 1e-6) &&
                    correctStop && (buy ? pos.TakeProfit.Value > pos.EntryPrice : pos.TakeProfit.Value < pos.EntryPrice);
            }
            if (valid)
            {
                _protectedOrders++;
                // Use the actual filled SL distance as an additional upper bound.
                // Do not enlarge the pre-order limit after execution.
                if (DynamicMoneyStop && !BrokerMoneyStop)
                {
                    double actualPips = Math.Abs(pos.EntryPrice - pos.StopLoss.Value) / Symbol.PipSize;
                    moneyLimit = Math.Min(moneyLimit, Symbol.AmountRisked(pos.VolumeInUnits, actualPips));
                }
                _moneyLimits[pos.Id] = moneyLimit;
                Print("MONEY_SNAPSHOT | id={0} | limite={1:F2} | dinamico={2}", pos.Id, moneyLimit, DynamicMoneyStop);
                // Account for the initial spread/commissions without waiting for another tick.
                CheckMoneyExit();
            }
            if (!valid)
            {
                var closed = ClosePosition(pos);
                Print("PROTECCION INVALIDA | cierre emergencia={0}", closed.IsSuccessful);
                if (!closed.IsSuccessful) { Print("ATENCION: verificar posicion manualmente"); Stop(); }
            }
        }

        private void Reject(string reason)
        {
            int n; _rejects.TryGetValue(reason, out n); _rejects[reason] = n + 1;
            Print("RECHAZO: {0}", reason);
        }
        protected override void OnStop()
        {
            Print("RESUMEN V2.1 | Velas={0} | BOS={1} | Tecnicas={2} | Elegibles={3} | Ordenes={4}", _bars, _bos, _technical, _eligible, _orders);
            Print("EJECUCION | solicitudes={0} | rechazosBroker={1} | aceptadas={2} | proteccionesValidas={3} | smokeChecks={4}", _attempts, _brokerRejects, _orders, _protectedOrders, _smokeChecks);
            Print("SALIDAS MONETARIAS | activaciones={0} | solicitudes={1} | cierres aceptados={2} | errores={3} | pendientes={4}",
                _moneyExitTriggers, _moneyExitRequests, _moneyExitAccepted, _moneyExitErrors, _moneyExitRetry.Count);
            Print("TRAIL_SUMMARY | activaciones={0} | disparos={1} | solicitudes={2} | cierres aceptados={3} | errores={4} | pendientes={5}",
                _lockActivations, _lockTriggers, _lockRequests, _lockAccepted, _lockErrors, _profitExitPending.Count);
            foreach (var item in _rejects.OrderByDescending(x => x.Value)) Print("RECHAZOS {0}: {1}", item.Key, item.Value);
        }
    }
}
