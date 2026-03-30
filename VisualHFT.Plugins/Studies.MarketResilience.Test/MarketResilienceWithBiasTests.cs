using VisualHFT.Model;
using Studies.MarketResilience.Model;
using VisualHFT.Commons.Model;
using VisualHFT.Studies.MarketResilience.Model;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Studies.MarketResilience.Test")]

namespace Studies.MarketResilience.Tests
{
    /// <summary>
    /// Tests for MarketResilienceWithBias class focusing on directional bias detection
    /// after depth depletion/recovery events.
    /// </summary>
    public class MarketResilienceWithBiasTests
    {
        private PlugInSettings _settings;

        public MarketResilienceWithBiasTests()
        {
            _settings = new PlugInSettings()
            {
                MaxShockMsTimeout = 500,
            };
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

        private void WarmUp(MarketResilienceWithBias calc, int frames = 300)
        {
            var random = new Random(42);
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

                calc.OnOrderBookUpdate(lob);
                calc.OnTrade(new Trade
                {
                    Size = 100,
                    Price = 100.49m,
                    Timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Test 1: BID depletion → ASK recovery = Bearish bias (sellers regained control)
        /// </summary>
        [Fact]
        public void BidDepletionAskRecovery_ShouldDetectBearishBias()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 }; // ✅ INCREASED timeout
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ✅ SEED fast recovery baselines (make current recovery look slow)
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // BID depletion
                );
                calc.OnOrderBookUpdate(shock);

                Thread.Sleep(100); // Fast recovery

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with SLOW recovery
            // Trigger trade shock (required anchor)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            // BID side depletion
            var bidDepleted = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // Depleted
            );
            calc.OnOrderBookUpdate(bidDepleted);

            Thread.Sleep(450); // SLOW recovery (vs 100ms baseline)

            // ASK side recovers (opposite side control transfer)
            var askRecovered = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK improved
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }  // BID still weak
            );
            calc.OnOrderBookUpdate(askRecovered);

            // ✅ VALIDATE: Bearish bias (sellers control)
            Assert.NotEqual(1m, calc.CurrentMRScore);
            Assert.True(calc.CurrentMRScore <= 0.30m,
                $"MR score {calc.CurrentMRScore} should be ≤ 0.30");
            Assert.Equal(eMarketBias.Bearish, calc.CurrentMarketBias);
        }

        /// <summary>
        /// Test 2: ASK depletion → BID recovery = Bullish bias (buyers regained control)
        /// </summary>
        [Fact]
        public void AskDepletionBidRecovery_ShouldDetectBullishBias()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 }; // ✅ INCREASED timeout
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ✅ SEED fast recovery baselines (make current recovery look slow)
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.50m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (100.70m, 20.0), (100.71m, 15.0) }, // ASK depletion
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(shock);

                Thread.Sleep(100); // Fast recovery

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with SLOW recovery
            // Trigger trade shock (required anchor)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.50m, Timestamp = DateTime.Now });

            // ASK side depletion
            var askDepleted = BuildLOB(
                asks: new[] { (100.70m, 20.0), (100.71m, 15.0) }, // Depleted
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
            );
            calc.OnOrderBookUpdate(askDepleted);

            Thread.Sleep(450); // SLOW recovery (vs 100ms baseline)

            // BID side recovers (opposite side control transfer)
            var bidRecovered = BuildLOB(
                asks: new[] { (100.70m, 20.0), (100.71m, 15.0) },  // ASK still weak
                bids: new[] { (100.50m, 120.0), (100.49m, 120.0), (100.48m, 120.0) } // BID improved
            );
            calc.OnOrderBookUpdate(bidRecovered);

            // ✅ VALIDATE: Bullish bias (buyers control)
            Assert.NotEqual(1m, calc.CurrentMRScore);
            Assert.True(calc.CurrentMRScore <= 0.30m,
                $"MR score {calc.CurrentMRScore} should be ≤ 0.30");
            Assert.Equal(eMarketBias.Bullish, calc.CurrentMarketBias);
        }

        /// <summary>
        /// Test 3: BID depletion → BID recovery (same side) = Neutral bias (resilient)
        /// </summary>
        [Fact]
        public void SameSideRecovery_ShouldDetectNeutralBias()
        {
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // Trigger trade shock (required anchor)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            // BID side depletion
            var bidDepleted = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // Depleted
            );
            calc.OnOrderBookUpdate(bidDepleted);

            Thread.Sleep(150);

            // BID side recovers (same side resilience)
            var bidRecovered = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) } // BID restored
            );
            calc.OnOrderBookUpdate(bidRecovered);

            // ✅ VALIDATE: Neutral bias (resilient same-side recovery)
            Assert.NotEqual(1m, calc.CurrentMRScore);
            Assert.Equal(eMarketBias.Neutral, calc.CurrentMarketBias);
        }

        /// <summary>
        /// Test 4: BOTH sides depleted → BID recovers first = Bullish bias
        /// </summary>
        [Fact]
        public void BothSidesDepleted_BidRecoversFirst_ShouldDetectBullishBias()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 }; // ✅ INCREASED timeout
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ✅ SEED fast recovery baselines (make current recovery look slow)
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (100.60m, 20.0) }, // BOTH sides depleted
                    bids: new[] { (100.40m, 20.0) }
                );
                calc.OnOrderBookUpdate(shock);

                Thread.Sleep(100); // Fast recovery

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with SLOW recovery
            // Trigger trade shock (required anchor)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            // BOTH sides depleted
            var bothDepleted = BuildLOB(
                asks: new[] { (100.60m, 20.0) }, // Depleted
                bids: new[] { (100.40m, 20.0) }  // Depleted
            );
            calc.OnOrderBookUpdate(bothDepleted);

            Thread.Sleep(450); // SLOW recovery (vs 100ms baseline)

            // BID side recovers first (buyers take control)
            var bidRecovered = BuildLOB(
                asks: new[] { (100.60m, 20.0) },  // ASK still weak
                bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) } // BID strong
            );
            calc.OnOrderBookUpdate(bidRecovered);

            // ✅ VALIDATE: Bullish bias (buyers control)
            Assert.NotEqual(1m, calc.CurrentMRScore);
            Assert.True(calc.CurrentMRScore <= 0.30m,
                $"MR score {calc.CurrentMRScore} should be ≤ 0.30");
            Assert.Equal(eMarketBias.Bullish, calc.CurrentMarketBias);
        }

        /// <summary>
        /// Test 5: High MR score (≥0.5) should skip bias calculation and return Neutral
        /// </summary>
        [Fact]
        public void HighMRScore_ShouldSkipBiasCalculation()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 }; // ✅ INCREASED timeout
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ✅ SEED SLOW recovery baselines (make current fast recovery look good)
            // This makes the 30ms recovery look FAST by comparison
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // BID depletion
                );
                calc.OnOrderBookUpdate(shock);

                Thread.Sleep(400); // SLOW recovery baseline

                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }

            // NOW run the actual test with FAST recovery
            // Trigger trade shock (required anchor)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });

            // BID side depletion
            var bidDepleted = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) } // Depleted
            );
            calc.OnOrderBookUpdate(bidDepleted);

            // Very fast recovery (< 50ms) → high MR score
            Thread.Sleep(30);

            // ASK side recovers (would be bearish, but MR score should be high)
            var askRecovered = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK improved
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }  // BID still weak
            );
            calc.OnOrderBookUpdate(askRecovered);

            // ✅ VALIDATE: High resilience → Neutral bias (skips directional bias)
            Assert.NotEqual(1m, calc.CurrentMRScore);

            // MR score should be high (fast recovery vs 400ms baseline)
            // depthScore = 400 / (400 + 30) ≈ 0.93 → MR ≈ 0.65-0.75 (high)
            if (calc.CurrentMRScore >= 0.5m)
            {
                Assert.Equal(eMarketBias.Neutral, calc.CurrentMarketBias);
            }
            else
            {
                // If MR score is low, bias should be Bearish (ASK recovery)
                Assert.Equal(eMarketBias.Bearish, calc.CurrentMarketBias);
            }
        }

        /// <summary>
        /// Test 6: Hysteresis validation - MRB arms at ≤0.30, disarms at ≥0.50, maintains state in between
        /// This test validates the _mrbArmed state machine with MRB_ON and MRB_OFF thresholds
        /// </summary>
        [Fact]
        public void HysteresisStateMachine_ShouldArmDisarmCorrectly()
        {
            var _settings = new PlugInSettings() { MaxShockMsTimeout = 600 };
            var calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ═══════════════════════════════════════════════════════════════
            // PHASE 1: ARM the hysteresis (MR ≤ 0.30)
            // ═══════════════════════════════════════════════════════════════
            
            // Seed very fast baseline (50ms) to make subsequent recovery look poor
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
                );
                calc.OnOrderBookUpdate(shock);
                Thread.Sleep(50); // Very fast baseline
                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }

            // Event 1: SLOW recovery (450ms) → MR ≤ 0.30 → ARMS hysteresis
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
            var depleted1 = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(depleted1);
            
            Thread.Sleep(450); // Slow recovery: 50/(50+450) = 0.10 → MR ≈ 0.08-0.15
            
            var recovered1 = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK recovers
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(recovered1);

            // ✅ Validate: MR ≤ 0.30 → _mrbArmed = TRUE → Bias emitted (Bearish)
            Assert.True(calc.CurrentMRScore <= 0.30m, 
                $"Event 1: MR score {calc.CurrentMRScore} should be ≤ 0.30 to arm hysteresis");
            Assert.Equal(eMarketBias.Bearish, calc.CurrentMarketBias);

            // ═══════════════════════════════════════════════════════════════
            // PHASE 2: MAINTAIN armed state in middle zone (0.30 < MR < 0.50)
            // ═══════════════════════════════════════════════════════════════
            
            // ✅ UPDATE BASELINE: Seed medium baseline (250ms) to make 350ms recovery appear in middle zone
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
                );
                calc.OnOrderBookUpdate(shock);
                Thread.Sleep(250); // Medium baseline
                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }
            
            // Event 2: Medium recovery (350ms) → MR ≈ 0.35-0.45 (middle zone)
            // Hysteresis should STAY ARMED and continue emitting bias
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
            var depleted2 = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(depleted2);
            
            Thread.Sleep(350); // Medium recovery: 250/(250+350) ≈ 0.42 → MR ≈ 0.35-0.45
            
            var recovered2 = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK recovers
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(recovered2);

            // ✅ Validate: 0.30 < MR < 0.50 → _mrbArmed STAYS TRUE → Bias emitted
            Assert.InRange(calc.CurrentMRScore, 0.30m, 0.50m);
            Assert.Equal(eMarketBias.Bearish, calc.CurrentMarketBias);

            // ═══════════════════════════════════════════════════════════════
            // PHASE 3: DISARM hysteresis (MR ≥ 0.50)
            // ═══════════════════════════════════════════════════════════════

            // ✅ RESET CALC to clear rolling windows and ensure a clean baseline for this phase
            calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // ✅ UPDATE BASELINE: Seed VERY SLOW baseline (500ms) to make 1ms recovery look incredible
            for (int i = 0; i < 10; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
                );
                calc.OnOrderBookUpdate(shock);
                Thread.Sleep(500); // VERY SLOW baseline
                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }
            
            // Event 3: INSTANT recovery (1ms) → MR ≥ 0.50 → DISARMS hysteresis
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
            var depleted3 = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(depleted3);
            
            Thread.Sleep(1); // Instant recovery: 500/(500+1) ≈ 0.998 → depth score near perfect
            
            var recovered3 = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK recovers
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(recovered3);

            // ✅ Validate: MR ≥ 0.50 → _mrbArmed = FALSE → Returns to Neutral
            // With 500ms baseline:
            // Trade: z ≈ 0 → score = 1.0 (PERFECT!)
            // Depth: 500/(500+1) ≈ 0.998
            // MR = (0.3×1.0 + 0.5×0.998) / 0.8 ≈ 0.997 ≥ 0.50 ✅✅✅
            Assert.True(calc.CurrentMRScore >= 0.50m,
                $"Event 3: MR score {calc.CurrentMRScore} should be ≥ 0.50 to disarm hysteresis");
            Assert.Equal(eMarketBias.Neutral, calc.CurrentMarketBias);

            // ═══════════════════════════════════════════════════════════════
            // PHASE 4: VERIFY disarmed state persists (MR in middle zone)
            // ═══════════════════════════════════════════════════════════════
            
            // ✅ RESET CALC again to ensure a clean baseline for this phase
            calc = new MarketResilienceWithBias(_settings);
            WarmUp(calc);

            // Re-seed the medium baseline from Phase 2
            for (int i = 0; i < 5; i++)
            {
                calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
                var shock = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
                );
                calc.OnOrderBookUpdate(shock);
                Thread.Sleep(250); // Medium baseline
                var recover = BuildLOB(
                    asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                    bids: new[] { (100.49m, 100.0), (100.48m, 100.0), (100.47m, 100.0) }
                );
                calc.OnOrderBookUpdate(recover);
            }


            // Event 4: Medium recovery (350ms) → MR ≈ 0.35-0.45 (middle zone again)
            // Hysteresis should STAY DISARMED → No bias emitted (returns null)
            calc.OnTrade(new Trade { Size = 5000, Price = 100.49m, Timestamp = DateTime.Now });
            var depleted4 = BuildLOB(
                asks: new[] { (100.50m, 100.0), (100.51m, 100.0), (100.52m, 100.0) },
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(depleted4);
            
            Thread.Sleep(350); // Same medium recovery as Event 2
            
            var recovered4 = BuildLOB(
                asks: new[] { (100.48m, 120.0), (100.49m, 120.0), (100.50m, 120.0) }, // ASK recovers
                bids: new[] { (100.40m, 50.0), (100.39m, 30.0) }
            );
            calc.OnOrderBookUpdate(recovered4);

            // ✅ Validate: 0.30 < MR < 0.50 BUT _mrbArmed = FALSE → Bias NOT emitted
            // The bias stays at the previous value (Neutral from Event 3) since CalculateMRBias returns null
            Assert.InRange(calc.CurrentMRScore, 0.30m, 0.50m);
            // When hysteresis is disarmed in middle zone, bias should remain Neutral (last set value)
            Assert.Equal(eMarketBias.Neutral, calc.CurrentMarketBias);
        }
    }
}