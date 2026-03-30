using System;
using System.Linq;
using System.Threading;
using VisualHFT.Model;
using Studies.MarketResilience.Model;
using VisualHFT.Commons.Model;
using VisualHFT.Studies.MarketResilience.Model;
using VisualHFT.Enums;
using Xunit;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Studies.MarketResilience.Test")]

namespace Studies.MarketResilience.Tests
{
    public class MarketResilienceTests
    {
        private PlugInSettings _settings;
        public MarketResilienceTests()
        {
            _settings = new PlugInSettings()
            {
                MaxShockMsTimeout = 500
            };
        }
        private OrderBookSnapshot CreateOrderBook(decimal spread, decimal bidPrice, decimal askPrice)
        {
            var ob = new OrderBook();
            
            ob.LoadData(new[] { new BookItem { Price = (double?)askPrice, Size = 100 } },
                        new[] { new BookItem { Price = (double?)bidPrice, Size = 100 } });
            var newSnapshot = new OrderBookSnapshot();
            newSnapshot.UpdateFrom(ob);
            return newSnapshot;
        }
        private OrderBookSnapshot BuildLOB((decimal px, double sz)[] asks, (decimal px, double sz)[] bids)
        {
            var ob = new OrderBook();

            var askItems = asks.Select(a => new BookItem
            {
                Price = (double)a.px,
                Size = a.sz,
                IsBid = false,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now
            }).ToArray();

            var bidItems = bids.Select(b => new BookItem
            {
                Price = (double)b.px,
                Size = b.sz,
                IsBid = true,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now
            }).ToArray();

            ob.LoadData(askItems, bidItems);
            var snapshot = new OrderBookSnapshot();
            snapshot.UpdateFrom(ob);
            return snapshot;
        }

        private void WarmUp(MarketResilienceCalculator calc, int frames = 300)
        {
            // Warm up with realistic micro-noise to properly train the MAD estimator
            // This ensures the detector learns what "normal" market variance looks like
            var random = new Random(42); // Fixed seed for test reproducibility

            for (int i = 0; i < frames; i++)
            {
                var lob = BuildLOB(
                    asks: new[] {
                (100.50m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.51m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.52m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    },
                    bids: new[] {
                (100.49m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.48m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.47m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    }
                );

                // ✅ Train BOTH order book AND trade baselines
                calc.OnOrderBookUpdate(lob);  // Trains: recentSpreads, depth baselines

                // ✅ ADD: Train trade size baseline
                calc.OnTrade(new Trade
                {
                    Size = 100, // Normal trade size (baseline)
                    Price = 100.49m,
                    Timestamp = DateTime.Now
                });
            }
        }

        [Fact]
        public void MarketResilienceCalculator_ShouldTrigger_AfterShockAndRecovery()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Feed historical stable data
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m, Timestamp = DateTime.Now });
            }

            // Trigger shocks explicitly
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500, Timestamp = DateTime.Now });
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            Thread.Sleep(_settings.MaxShockMsTimeout.Value - 100); // Simulate time passing to mimic recovery period

            // Recover spread
            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // Verify MR recalculation occurred
            Assert.NotEqual(1m, mrCalc.CurrentMRScore);
            Assert.InRange(mrCalc.CurrentMRScore, 0, 1);
        }
        [Fact]
        public void MarketResilienceWithBias_ShouldDetectBearishBias()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 };
            var mrCalcBias = new MarketResilienceWithBias(_settings);
            WarmUp(mrCalcBias, 300);

            // ✅ SEED fast recovery baselines (make current recovery look slow)
            // Simulate 5 prior fast recoveries (100ms each) to establish historical baseline
            for (int i = 0; i < 5; i++)
            {
                mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (105.00m, 100.0), (105.01m, 100.0), (105.02m, 100.0) },
                    bids: new[] { (100.30m, 20.0), (100.29m, 15.0) }
                );
                mrCalcBias.OnOrderBookUpdate(shock);

                Thread.Sleep(100); // Fast recovery

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                mrCalcBias.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with SLOW recovery
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            var spreadShock = BuildLOB(
                asks: new[] { (105.00m, 100.0), (105.01m, 100.0), (105.02m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );
            mrCalcBias.OnOrderBookUpdate(spreadShock);

            var bidDepleted = BuildLOB(
                asks: new[] { (105.00m, 100.0), (105.01m, 100.0), (105.02m, 100.0) },
                bids: new[] { (100.30m, 20.0), (100.29m, 15.0) }
            );
            mrCalcBias.OnOrderBookUpdate(bidDepleted);

            Thread.Sleep(450); // SLOW recovery (vs 100ms baseline)

            var spreadRecovered = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.30m, 20.0), (100.29m, 15.0) }
            );
            mrCalcBias.OnOrderBookUpdate(spreadRecovered);

            Thread.Sleep(50);

            var askRecovered = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) },
                bids: new[] { (100.30m, 20.0), (100.29m, 15.0) }
            );
            mrCalcBias.OnOrderBookUpdate(askRecovered);

            // With historical baseline of 100ms, 500ms recovery gives:
            // depthScore = 100 / (100 + 500) = 0.167
            // spreadScore = 100 / (100 + 450) = 0.182
            // MR = 0.30×0.25 + 0.10×0.182 + 0.50×0.167 + 0.10×0.002 = 0.177 ≤ 0.30 ✅

            Assert.True(mrCalcBias.CurrentMRScore <= 0.30m,
                $"MR score {mrCalcBias.CurrentMRScore} should be ≤ 0.30");
            Assert.Equal(eMarketBias.Bearish, mrCalcBias.CurrentMarketBias);
        }

        [Fact]
        public void MarketResilienceWithBias_ShouldDetectBullishBias()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 }; // ✅ INCREASED timeout
            var mrCalcBias = new MarketResilienceWithBias(_settings);

            // ✅ Use WarmUp() to train depth baselines
            WarmUp(mrCalcBias, 300);

            // ✅ SEED fast recovery baselines (make current recovery look slow)
            for (int i = 0; i < 5; i++)
            {
                mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 100.50m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (100.70m, 20.0), (100.71m, 15.0) }, // ASK depletion
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                mrCalcBias.OnOrderBookUpdate(shock);

                Thread.Sleep(100); // Fast recovery

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                mrCalcBias.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with SLOW recovery
            // Trigger trade shock (required anchor)
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 100.50m, Timestamp = DateTime.Now });

            // ✅ Trigger ASK depletion (depth shock)
            var askDepleted = BuildLOB(
                asks: new[] { (100.70m, 20.0), (100.71m, 15.0) }, // Depleted
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );
            mrCalcBias.OnOrderBookUpdate(askDepleted);

            Thread.Sleep(450); // SLOW recovery (vs 100ms baseline)

            // ✅ BID side recovers (opposite side control transfer)
            var bidRecovered = BuildLOB(
                asks: new[] { (100.70m, 20.0), (100.71m, 15.0) },  // ASK still weak
                bids: new[] { (100.50m, 120.0), (100.49m, 120.0), (100.48m, 120.0) } // BID improved
            );
            mrCalcBias.OnOrderBookUpdate(bidRecovered);

            // ✅ VALIDATE: Bullish bias (buyers control)
            Assert.NotEqual(1m, mrCalcBias.CurrentMRScore);
            Assert.True(mrCalcBias.CurrentMRScore <= 0.30m,
                $"MR score {mrCalcBias.CurrentMRScore} should be ≤ 0.30");
            Assert.Equal(eMarketBias.Bullish, mrCalcBias.CurrentMarketBias);
        }

        [Fact]
        public void MarketResilienceWithBias_ShouldDetectNeutralBias_WhenFullyRecovered()
        {
            var mrCalcBias = new MarketResilienceWithBias(_settings);

            // ✅ Use WarmUp() to train depth baselines
            WarmUp(mrCalcBias, 300);

            // Trigger trade shock (required anchor)
            mrCalcBias.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            // ✅ Trigger BID depletion (depth shock)
            var bidDepleted = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // Depleted
            );
            mrCalcBias.OnOrderBookUpdate(bidDepleted);

            Thread.Sleep(250);

            // ✅ BID side recovers (same-side resilience)
            var bidRecovered = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) } // BID restored
            );
            mrCalcBias.OnOrderBookUpdate(bidRecovered);

            // ✅ VALIDATE: Neutral bias (resilient same-side recovery)
            Assert.NotEqual(1m, mrCalcBias.CurrentMRScore);
            Assert.Equal(eMarketBias.Neutral, mrCalcBias.CurrentMarketBias);
        }
        [Fact]
        public void MRCalculation_ComponentWeights_AreCorrect()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);
            WarmUp(mrCalc); // 300 samples

            // Scenario: All shocks present with known recovery times
            // Trade: 5000 shares (z=4.5 → score ≈ 0.25)
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 100.49m });

            // ✅ FIX: Create ACTUAL 5-unit spread shock
            // Spread: 5x shock (baseline ~0.01), 100ms recovery (fast → score ≈ 0.67)
            // Need: spread = 5.0 units (5x baseline of ~0.01 from WarmUp)
            // Calculate: mid = 100.49, spread = 5.0
            //           → bid = 100.49 - 2.5 = 97.99
            //           → ask = 100.49 + 2.5 = 102.99
            var spreadShock = CreateOrderBook(5m, 97.99m, 102.99m);
            mrCalc.OnOrderBookUpdate(spreadShock);

            // Depth: 100ms recovery (fast → score ≈ 0.67)
            var depthShock = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            mrCalc.OnOrderBookUpdate(depthShock);

            Thread.Sleep(100);

            // Recoveries - return to baseline spread (~0.01)
            var recovery = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );
            mrCalc.OnOrderBookUpdate(recovery);

            // ✅ VALIDATE: Score reflects proper weighting
            // Expected: 0.3×0.25 + 0.1×0.67 + 0.5×0.67 + 0.1×0.20 ≈ 0.50
            decimal expectedScore = 0.50m;
            Assert.InRange(mrCalc.CurrentMRScore, expectedScore - 0.15m, expectedScore + 0.15m);
        }

        [Fact]
        public void MRCalculation_TradeTimeout_PreventsCalculation()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Feed baseline data
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m });
            }

            // Trigger trade shock
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500 });

            // Wait for trade to timeout (500ms from settings)
            Thread.Sleep(550);

            // Trigger spread shock AFTER trade timeout
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));
            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // ✅ VALIDATE: No calculation (trade anchor missing)
            Assert.Equal(1m, mrCalc.CurrentMRScore);
        }

        [Fact]
        public void MRCalculation_SpreadTimeout_ClearsState()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Setup
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m });
            }

            // Trigger shocks
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500 });
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            // Wait for spread timeout (500ms)
            Thread.Sleep(550);

            // Trigger another order book update
            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // ✅ VALIDATE: Spread shock state cleared, no calculation
            Assert.Equal(1m, mrCalc.CurrentMRScore);
        }
        [Fact]
        public void MRCalculation_SpreadOnly_NoDepth_CalculatesCorrectly()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // Feed baseline
            for (int i = 0; i < 30; i++)
            {
                mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));
                mrCalc.OnTrade(new Trade { Size = 10, Price = 500.25m });
            }

            // Trigger trade + spread (but NO depth shock)
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 500 });
            mrCalc.OnOrderBookUpdate(CreateOrderBook(5m, 495, 500));

            Thread.Sleep(200);

            mrCalc.OnOrderBookUpdate(CreateOrderBook(0.5m, 500, 500.5m));

            // ✅ VALIDATE: Score calculated with partial data
            // Components: Trade (30%) + Spread (10%) + Magnitude (10%) = 50% weight
            Assert.NotEqual(1m, mrCalc.CurrentMRScore);
            Assert.InRange(mrCalc.CurrentMRScore, 0.3m, 0.9m);
        }
    }

    public class DepthDepletionRecoveryTests
    {
        private PlugInSettings _settings;

        public DepthDepletionRecoveryTests()
        {
            _settings = new PlugInSettings()
            {
                MaxShockMsTimeout = 500
            };
        }

        // Test utilities
        private OrderBookSnapshot BuildLOB((decimal px, double sz)[] asks, (decimal px, double sz)[] bids)
        {
            var ob = new OrderBook();
            
            var askItems = asks.Select(a => new BookItem { 
                Price = (double)a.px, 
                Size = a.sz, 
                IsBid = false,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now
            }).ToArray();
            
            var bidItems = bids.Select(b => new BookItem { 
                Price = (double)b.px, 
                Size = b.sz, 
                IsBid = true,
                LocalTimeStamp = DateTime.Now,
                ServerTimeStamp = DateTime.Now
            }).ToArray();
            
            ob.LoadData(askItems, bidItems);
            var snapshot = new OrderBookSnapshot();
            snapshot.UpdateFrom(ob);
            return snapshot;
        }

        private void WarmUp(MarketResilienceCalculator calc, int frames = 300)
        {
            // Warm up with realistic micro-noise to properly train the MAD estimator
            // This ensures the detector learns what "normal" market variance looks like
            var random = new Random(42); // Fixed seed for test reproducibility

            for (int i = 0; i < frames; i++)
            {
                var lob = BuildLOB(
                    asks: new[] { 
                // Each level gets independent micro-noise (±0.5 cent, ±2.5% size)
                (100.50m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.51m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.52m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    },
                    bids: new[] {
                (100.49m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.48m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.47m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    }
                );
                calc.IsLOBDepleted(lob); // Train both median and MAD estimators
            }
        }

        private void ActivateIfNeeded(MarketResilienceCalculator calc, OrderBookSnapshot lob)
        {
            var side = calc.IsLOBDepleted(lob);
            if (side != eLOBSIDE.NONE)
            {
                calc.ActivateDepthEvent(lob, side);
            }
        }

        // A. Depletion detection (edge-triggered)
        [Fact]
        public void IsLOBDepleted_BidInnerWipe_OuterRefill_FiresBid()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ FIXED: Create TRUE depletion scenario
            // Inner bid levels disappear, replaced by FEWER levels at WORSE prices with LESS total size
            var depletedLob = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 80.0), (100.52m, 60.0) }, // Asks unchanged
                bids: new[] {
            (100.40m, 50.0),  // 9 cents worse, much smaller
            (100.39m, 30.0)   // Even worse, even smaller
                              // Total: 80 vs baseline ~280 → clear depletion
                }
            );

            var result = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.BID, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }
        [Fact]
        public void IsLOBDepleted_AskInnerWipe_OuterRefill_FiresAsk()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ FIXED: Create TRUE depletion scenario
            // Inner ask levels disappear, replaced by FEWER levels at WORSE prices with LESS total size
            var depletedLob = BuildLOB(
                asks: new[] {
            (100.60m, 50.0),  // 10 cents worse, much smaller
            (100.61m, 30.0)   // Even worse, even smaller
                              // Total: 80 vs baseline ~280 → clear depletion
                },
                bids: new[] { (100.49m, 100.0), (100.48m, 80.0), (100.47m, 60.0) } // Bids unchanged
            );

            var result = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.ASK, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        [Fact]
        public void IsLOBDepleted_BothSidesThinned_FiresBoth()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Create depletion scenario: both sides significantly thinned
            var depletedLob = BuildLOB(
                asks: new[] { (100.60m, 20.0) }, // Much worse and smaller
                bids: new[] { (100.40m, 20.0) }  // Much worse and smaller
            );

            var result = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.BOTH, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        [Fact]
        public void IsLOBDepleted_DuringWarmup_DoesNotFire()
        {
            var calc = new MarketResilienceCalculator(_settings);
            
            // During warm-up, create dramatic change that would trigger depletion after warm-up
            for (int i = 0; i < 50; i++) // Less than warmup samples
            {
                var lob = i < 25 
                    ? BuildLOB(
                        asks: new[] { (100.50m, 100.0), (100.51m, 80.0) },
                        bids: new[] { (100.49m, 100.0), (100.48m, 80.0) })
                    : BuildLOB(
                        asks: new[] { (100.60m, 20.0) }, // Dramatic change
                        bids: new[] { (100.40m, 20.0) }); // Dramatic change
                
                var result = calc.IsLOBDepleted(lob);
                Assert.Equal(eLOBSIDE.NONE, result); // Should not fire during warm-up
                
                Thread.Sleep(10);
            }
        }

        [Fact]
        public void IsLOBDepleted_SmallNoisyChurn_DoesNotFire()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ FIXED: Small INDEPENDENT random changes around baseline
            // Matches the warm-up structure (3 levels) with independent micro-noise
            var random = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                // Generate independent noise for each level (same pattern as warm-up)
                var lob = BuildLOB(
                    asks: new[] {
                (100.50m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.51m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.52m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    },
                    bids: new[] {
                (100.49m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.48m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.47m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    }
                );

                var result = calc.IsLOBDepleted(lob);
                Assert.Equal(eLOBSIDE.NONE, result);

                Thread.Sleep(10);
            }
        }

        // B. Recovery (dynamic + timeout)
        [Fact]
        public void IsLOBRecovered_SameSideEarlyRecovery_ReportsBid()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ QUALITY FIX: Trigger BID depletion with realistic 3-level structure
            // Create clear depletion that will actually trigger
            var depletedLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Ask side normal (3 levels matching warm-up)
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.40m, 50.0),   // Bid depleted: 9 cents worse, half size
            (100.39m, 30.0)    // Only 2 levels vs baseline 3
                }
            );

            // Verify BID depletion triggered before testing recovery
            var depletionResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.BID, depletionResult);

            // Activate the event
            calc.ActivateDepthEvent(depletedLob, depletionResult);

            // ✅ Quickly restore bid liquidity to FULL baseline (tests same-side recovery)
            Thread.Sleep(100);
            var recoveredLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Asks unchanged
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.49m, 100.0),  // Bid restored to baseline
            (100.48m, 100.0),  // Full 3 levels
            (100.47m, 100.0)   // Same-side recovery ≥90%
                }
            );

            // ✅ TEST: BID recovery should be detected (same side that was depleted)
            var result = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.BID, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        [Fact]
        public void IsLOBRecovered_OppositeSideImprovesFirst_ReportsAsk()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ QUALITY FIX: Trigger BID depletion with realistic 3-level structure
            var depletedLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Ask side normal (3 levels matching warm-up)
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.40m, 50.0),   // Bid severely depleted (9 cents worse, half size)
            (100.39m, 30.0)    // Only 2 levels vs baseline 3
                }
            );

            // Verify BID depletion triggered
            var depletionResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.BID, depletionResult);

            // Activate the event
            calc.ActivateDepthEvent(depletedLob, depletionResult);

            // ✅ ASK side improves significantly to FULL baseline while bids stay weak
            // This tests the scenario where the OPPOSITE side recovers first
            Thread.Sleep(100);
            var recoveredLob = BuildLOB(
                asks: new[] {
            (100.49m, 110.0),  // ASK improved: 1 cent better + more size
            (100.50m, 110.0),  // Full 3 levels restored
            (100.51m, 110.0)   // Total immediacy exceeds baseline
                },
                bids: new[] {
            (100.40m, 50.0),   // Bids still weak (unchanged)
            (100.39m, 30.0)    // Still only 2 levels
                }
            );

            // ✅ TEST: ASK recovery should be detected (≥90% improvement on opposite side)
            var result = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.ASK, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        [Fact]
        public void IsLOBRecovered_BothSidesRecoverStrongly_ReportsBoth()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Trigger BOTH sides depletion with extreme scenario
            // This creates a clear, measurable depletion baseline
            var depletedLob = BuildLOB(
                asks: new[] { (100.60m, 10.0) }, // 10 cents worse, tiny size
                bids: new[] { (100.40m, 10.0) }  // 9 cents worse, tiny size
            );

            var depletionResult = calc.IsLOBDepleted(depletedLob);

            // Verify depletion actually triggered before testing recovery
            Assert.Equal(eLOBSIDE.BOTH, depletionResult);

            // Now activate the event
            if (depletionResult != eLOBSIDE.NONE)
            {
                calc.ActivateDepthEvent(depletedLob, depletionResult);
            }

            // ✅ QUALITY FIX: Both sides recover STRONGLY and COMPLETELY
            // This tests that the recovery logic properly detects:
            // 1. Sufficient depth restoration (≥90% recovery)
            // 2. Both sides recovering simultaneously
            // 3. Recovery meeting the multi-side threshold
            Thread.Sleep(200);
            var recoveredLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // L1: Full baseline restoration
            (100.51m, 100.0),  // L2: Full baseline restoration
            (100.52m, 100.0)   // L3: Full baseline restoration
                },
                bids: new[] {
            (100.49m, 100.0),  // L1: Full baseline restoration
            (100.48m, 100.0),  // L2: Full baseline restoration
            (100.47m, 100.0)   // L3: Full baseline restoration
                }
            );

            var result = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.BOTH, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }


        [Fact]
        public void IsLOBRecovered_OnTimeout_PicksDominantSide()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Trigger BID depletion
            var depletedLob = BuildLOB(
                asks: new[] { (100.50m, 100.0) },
                bids: new[] { (100.30m, 50.0) } // Severely depleted: from ~100.49@100 to 100.30@50
            );
            ActivateIfNeeded(calc, depletedLob);

            // Force timeout with partial recovery - bid recovers significantly more
            Thread.Sleep(1100); // Force timeout (1000ms + buffer)
            var partialRecoveryLob = BuildLOB(
                asks: new[] { (100.495m, 105.0) }, // Ask: minimal improvement (100.50@100 → 100.495@105)
                bids: new[] { (100.45m, 85.0) }    // Bid: major improvement (100.30@50 → 100.45@85)
            );

            var result = calc.IsLOBRecovered(partialRecoveryLob);
            // Bid has much better recovery fraction, should be reported as dominant
            Assert.Equal(eLOBSIDE.BID, result);

            // Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBRecovered(partialRecoveryLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        [Fact]
        public void IsLOBRecovered_EdgeTriggered_NoDoubleFire()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ QUALITY FIX: Create realistic depletion scenario that ACTUALLY triggers
            // Use 3 levels (matching warm-up) but with severely depleted bid side
            var depletedLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Ask side normal (3 levels)
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.40m, 50.0),   // Bid depleted: 9 cents worse, half size
            (100.39m, 30.0)    // Even worse
                               // Missing L3 - only 2 levels vs baseline 3
                }
            );

            // Explicitly verify depletion triggered before testing recovery
            var depletionResult = calc.IsLOBDepleted(depletedLob);
            Assert.Equal(eLOBSIDE.BID, depletionResult);

            // Now activate
            calc.ActivateDepthEvent(depletedLob, depletionResult);

            // ✅ Recovery: Restore bid side to baseline (3 levels)
            var recoveredLob = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.49m, 100.0),  // Restored to baseline
            (100.48m, 100.0),  // 3 levels
            (100.47m, 100.0)
                }
            );

            // ✅ TEST: First call should return BID (recovery detected)
            var firstResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.BID, firstResult);

            // ✅ TEST: Second call should return NONE (edge-triggered)
            var secondResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);
        }

        // C. Concurrency / overlap policy
        [Fact]
        public void NewDepletionWhileActive_IgnoredByPolicy()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // ✅ QUALITY FIX: First depletion - create realistic scenario that ACTUALLY triggers
            var firstDepletion = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Ask side normal (3 levels)
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.40m, 50.0),   // Bid depleted: 9 cents worse, half size
            (100.39m, 30.0)    // Only 2 levels vs baseline 3
                }
            );

            // Verify BID depletion triggered
            var firstResult = calc.IsLOBDepleted(firstDepletion);
            Assert.Equal(eLOBSIDE.BID, firstResult);

            // Policy layer: activate if no active event
            if (firstResult != eLOBSIDE.NONE)
            {
                calc.ActivateDepthEvent(firstDepletion, firstResult);
            }

            // ✅ Second depletion while first is active - create ASK depletion scenario
            // Policy layer should NOT re-activate while event is already active
            Thread.Sleep(100);
            var secondDepletion = BuildLOB(
                asks: new[] {
            (100.60m, 50.0),   // ASK depletion: 10 cents worse
            (100.61m, 30.0)    // Only 2 levels
                },
                bids: new[] {
            (100.40m, 50.0),   // Bids still depleted (unchanged)
            (100.39m, 30.0)
                }
            );

            // IsLOBDepleted might detect ASK depletion, but we don't activate again (policy)
            var secondResult = calc.IsLOBDepleted(secondDepletion);
            // Policy layer: do NOT call ActivateDepthEvent again while event is active

            // ✅ Recovery should still work for original BID event with proper 3-level structure
            Thread.Sleep(100);
            var recovery = BuildLOB(
                asks: new[] {
            (100.50m, 100.0),  // Asks back to normal
            (100.51m, 100.0),
            (100.52m, 100.0)
                },
                bids: new[] {
            (100.49m, 100.0),  // BID fully restored
            (100.48m, 100.0),  // 3 levels
            (100.47m, 100.0)
                }
            );

            var recoveryResult = calc.IsLOBRecovered(recovery);
            Assert.Equal(eLOBSIDE.BID, recoveryResult); // Original BID event finalizes correctly

            // Edge-triggered check
            var secondRecoveryResult = calc.IsLOBRecovered(recovery);
            Assert.Equal(eLOBSIDE.NONE, secondRecoveryResult);
        }
        // D. Corner cases
        [Fact]
        public void ZeroSpread_Guards_NoNaN()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Zero spread scenario - ask=bid
            var zeroSpreadLob = BuildLOB(
                asks: new[] { (100.50m, 100.0) },
                bids: new[] { (100.50m, 100.0) } // Same price = zero spread
            );

            // Should not throw or produce NaN
            var result = calc.IsLOBDepleted(zeroSpreadLob);
            // Result should be a valid enum value (no NaN/exceptions)
            Assert.True(Enum.IsDefined(typeof(eLOBSIDE), result));
        }

        [Fact]
        public void EmptyBidSide_TriggersAndRecovers()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Empty bid side (swept)
            var emptyBidLob = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new (decimal, double)[0]
            );

            var result = calc.IsLOBDepleted(emptyBidLob);
            Assert.Equal(eLOBSIDE.BID, result);

            // ✅ Manually activate with the result we already got
            if (result != eLOBSIDE.NONE)
            {
                calc.ActivateDepthEvent(emptyBidLob, result);
            }

            // Edge-triggered check
            var secondResult = calc.IsLOBDepleted(emptyBidLob);
            Assert.Equal(eLOBSIDE.NONE, secondResult);

            // Add back bid liquidity
            Thread.Sleep(200);
            var recoveredLob = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );

            var recoveryResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.BID, recoveryResult);

            // Edge-triggered check
            var secondRecoveryResult = calc.IsLOBRecovered(recoveredLob);
            Assert.Equal(eLOBSIDE.NONE, secondRecoveryResult);
        }

        [Fact]
        public void InsufficientTopN_TimesOutGracefully()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Book too thin to reach typical Q - trigger depletion with minimal visible book
            var minimalLob = BuildLOB(
                asks: new[] { (100.60m, 20.0) }, // Single level, far from market
                bids: new[] { (100.40m, 20.0) }  // Single level, far from market
            );
            ActivateIfNeeded(calc, minimalLob);

            // Wait for timeout
            Thread.Sleep(1100); // Force timeout
            var timeoutResult = calc.IsLOBRecovered(minimalLob);
            
            // Must timeout and finalize with some result (not NONE)
            Assert.NotEqual(eLOBSIDE.NONE, timeoutResult);

            // Edge-triggered check
            var secondTimeoutResult = calc.IsLOBRecovered(minimalLob);
            Assert.Equal(eLOBSIDE.NONE, secondTimeoutResult);
        }

        [Fact]
        public void BaselineShift_PostWarmup_NoSpuriousTriggers()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc); // ~300 frames at 100.49/100.50, inner sizes ~100

            // Regime shift: move the whole book up gradually BUT keep spread and inner mass stable
            // Baseline spread assumed ~0.01; preserve that throughout the shift.
            for (int i = 0; i < 60; i++)
            {
                decimal mid = 100.495m + i * 0.005m; // small +0.005 steps
                decimal bestBid = mid - 0.005m;      // spread = 0.01 preserved
                decimal bestAsk = mid + 0.005m;

                // Keep inner levels’ sizes close to baseline so immediacy near the touch doesn’t drop
                var shifted = BuildLOB(
                    asks: new[] { (bestAsk, 100d), (bestAsk + 0.01m, 100d), (bestAsk + 0.02m, 100d) },
                    bids: new[] { (bestBid, 100d), (bestBid - 0.01m, 100d), (bestBid - 0.02m, 100d) }
                );

                var side = calc.IsLOBDepleted(shifted);
                Assert.Equal(eLOBSIDE.NONE, side);
            }

            // Now prove the detector still fires on a real depletion
            // Make bids essentially empty near the touch; keep asks normal.
            // This is an unmistakable immediacy collapse on the BID side.
            var obviousDepletion = BuildLOB(
                asks: new[] { (100.505m, 100d), (100.515m, 100d), (100.525m, 100d) }, // keep asks normal
                bids: new[] { (100.475m, 5d), (100.465m, 5d), (100.455m, 5d) } // Move best bid out by 2 ticks (if your baseline spread ≈ 0.01) and slash sizes
            );
            var depletionResult = calc.IsLOBDepleted(obviousDepletion);
            Assert.NotEqual(eLOBSIDE.NONE, depletionResult); // must trigger
            Assert.Equal(eLOBSIDE.NONE, calc.IsLOBDepleted(obviousDepletion)); // edge-trigger
        }


        // E. Stress
        [Fact]
        public void HighFrequencyNoisyUpdates_StableUnderLoad()
        {
            var calc = new MarketResilienceCalculator(_settings);
            WarmUp(calc);

            // Run 1000 updates with REALISTIC but INDEPENDENT noise per level
            var random = new Random(42); // Fixed seed for reproducibility
            int depletionCount = 0;

            for (int i = 0; i < 1000; i++)
            {
                // ✅ FIX: Generate INDEPENDENT noise for each level
                // This simulates realistic order book microstructure where
                // different levels churn independently

                var lob = BuildLOB(
                    asks: new[] {
                // Each level gets its own independent noise
                (100.50m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.51m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.52m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    },
                    bids: new[] {
                // Each level gets its own independent noise
                (100.49m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.48m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05))),

                (100.47m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(90, 100.0 * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    }
                );

                var result = calc.IsLOBDepleted(lob);
                if (result != eLOBSIDE.NONE)
                {
                    depletionCount++;
                }

                Thread.Sleep(1); // High frequency updates
            }

            // ✅ With independent micro-noise, depletion triggers should be rare (< 1% of frames)
            Assert.True(depletionCount < 10,
                $"Too many depletions ({depletionCount}/1000 = {depletionCount / 10.0}%) - threshold too sensitive");
        }

        [Fact]
        public void MRCalculation_WithDepthShock_CalculatesCorrectly()
        {
            var mrCalc = new MarketResilienceCalculator(_settings);

            // ✅ FIX: Warm up ALL baselines (depth, spread, AND trade)
            var random = new Random(42);
            for (int i = 0; i < 300; i++)
            {
                var lob = BuildLOB(
                    asks: new[] {
                (100.50m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),
                (100.51m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),
                (100.52m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    },
                    bids: new[] {
                (100.49m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),
                (100.48m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05))),
                (100.47m + (decimal)(random.NextDouble() - 0.5) * 0.01m,
                 Math.Max(95, 100d * (1.0 + (random.NextDouble() - 0.5) * 0.05)))
                    }
                );
                mrCalc.OnOrderBookUpdate(lob);

                // ✅ ADD: Train trade size baseline
                mrCalc.OnTrade(new Trade
                {
                    Size = 100,  // Normal baseline trade size
                    Price = 100.49m,
                    Timestamp = DateTime.Now
                });
            }

            // Trigger trade shock (required anchor)
            mrCalc.OnTrade(new Trade { Size = 5000, Price = 100.49m });

            // Trigger depth depletion (but NO spread shock)
            var depletedLob = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // Depleted
            );
            mrCalc.OnOrderBookUpdate(depletedLob);

            Thread.Sleep(200);

            // Depth recovery
            var recoveredLob = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );
            mrCalc.OnOrderBookUpdate(recoveredLob);

            // ✅ VALIDATE: MR score calculated with depth-only data
            Assert.NotEqual(1m, mrCalc.CurrentMRScore);
            Assert.InRange(mrCalc.CurrentMRScore, 0.3m, 0.8m);
        }
    }
}
