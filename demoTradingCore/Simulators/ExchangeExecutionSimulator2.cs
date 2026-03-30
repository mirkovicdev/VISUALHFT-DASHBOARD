using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Model;

namespace VisualHFT.Testing
{
    public class ExchangeTradingSimulator
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _symbols = { "BTC/USD", "ETH/USD" };
        private static readonly eORDERSIDE[] _sides = { eORDERSIDE.Buy, eORDERSIDE.Sell };
        private static readonly eORDERSTATUS[] _statuses = { eORDERSTATUS.NEW, eORDERSTATUS.PARTIALFILLED, eORDERSTATUS.FILLED, eORDERSTATUS.CANCELED };

        public async Task StartSimulationAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Run the simulation for 5 minutes
            var simulationTask = Task.Run(() => RunSimulation(cancellationToken), cancellationToken);

            // Wait for 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5));

            // Cancel the simulation
            cancellationTokenSource.Cancel();

            // Wait for the simulation to complete
            await simulationTask;
        }

        private void RunSimulation(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Generate a random order
                var order = GenerateRandomOrder();

                // Update the data in HelperPosition
                HelperPosition.Instance.UpdateData(order);

                // Wait for a random amount of time before sending the next order
                Thread.Sleep(_random.Next(100, 1000));
            }
        }

        private Order GenerateRandomOrder()
        {
            var symbol = _symbols[_random.Next(_symbols.Length)];
            var side = _sides[_random.Next(_sides.Length)];
            var status = _statuses[_random.Next(_statuses.Length)];
            var quantity = _random.NextDouble() * 10;
            var price = _random.NextDouble() * 50000;

            return new Order
            {
                ProviderName = "TestProvider",
                OrderID = _random.Next(1, 100000),
                StrategyCode = "TestStrategy",
                Symbol = symbol,
                ProviderId = _random.Next(1, 10),
                ClOrdId = Guid.NewGuid().ToString(),
                Side = side,
                OrderType = eORDERTYPE.LIMIT,
                TimeInForce = eORDERTIMEINFORCE.GTC,
                Status = status,
                Quantity = quantity,
                MinQuantity = 0,
                FilledQuantity = status == eORDERSTATUS.FILLED ? quantity : quantity * _random.NextDouble(),
                PricePlaced = price,
                Currency = "USD",
                FutSettDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd"),
                IsMM = false,
                IsEmpty = false,
                LayerName = "TestLayer",
                AttemptsToClose = 0,
                SymbolMultiplier = 1,
                SymbolDecimals = 2,
                FreeText = "TestOrder",
                OriginPartyID = "TestOrigin",
                Executions = new List<Execution>
                {
                    new Execution
                    {
                        OrderID = _random.Next(1, 100000),
                        ExecutionID = _random.Next(1, 100000),
                        ClOrdId = Guid.NewGuid().ToString(),
                        ExecID = Guid.NewGuid().ToString(),
                        LocalTimeStamp = DateTime.UtcNow,
                        ServerTimeStamp = DateTime.UtcNow,
                        Price = (decimal)price,
                        ProviderID = _random.Next(1, 10),
                        QtyFilled = (decimal)(quantity * _random.NextDouble()),
                        Side = side,
                        Status = status,
                        IsOpen = status != eORDERSTATUS.FILLED,
                        ProviderName = "TestProvider",
                        Symbol = symbol
                    }
                },
                QuoteID = _random.Next(1, 100000),
                QuoteServerTimeStamp = DateTime.UtcNow,
                QuoteLocalTimeStamp = DateTime.UtcNow,
                CreationTimeStamp = DateTime.UtcNow,
                FireSignalTimestamp = DateTime.UtcNow,
                StopLoss = 0,
                TakeProfit = 0,
                PipsTrail = false,
                UnrealizedPnL = 0,
                MaxDrowdown = 0,
                BestAsk = price + _random.NextDouble() * 10,
                BestBid = price - _random.NextDouble() * 10,
                GetAvgPrice = price,
                GetQuantity = quantity,
                FilledPercentage = status == eORDERSTATUS.FILLED ? 100 : _random.NextDouble() * 100
            };
        }
    }
}
