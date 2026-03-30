
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Enums;
using VisualHFT.Helpers;
using VisualHFT.Model;

namespace VisualHFT.Simulators
{
    public class ExchangeExecutionSimulator
    {
        private static readonly Random _random = new Random();
        private static readonly string[] _symbols = { "BTC/USD", "ETH/USD" };
        /*
         *SCENARIOS:
         * 1. NEW -> 100% FILLED
         * 2. NEW -> leave it (working order)
         * 3. NEW -> 50% PARTIAL FILLED -> 50% CANCELED
         * 4. NEW -> 50% PARTIAL  FILLED -> 50% PARTIAL FILLED (FULL)
         * 5. NEW -> 100% CANCELED
         * 6. NEW -> 100% REJECTED
         */


        private static readonly ExchangeExecutionSimulator instance = new ExchangeExecutionSimulator();
        public static ExchangeExecutionSimulator Instance => instance;


        public ExchangeExecutionSimulator()
        {
            HelperTrade.Instance.Subscribe(ProcessorTrades);

        }
        private static readonly object _lock = new object();

        void ProcessorTrades(Trade obj)
        {
            if (Monitor.TryEnter(_lock))
            {
                try
                {
                    // Check if the symbol is in the list
                    if (_symbols.Contains(obj.Symbol))
                    {
                        var symbol = obj.Symbol;
                        var providerId = obj.ProviderId;
                        var order_id = _random.Next(1, 100000000);
                        var side = obj.IsBuy.Value ? eORDERSIDE.Buy : eORDERSIDE.Sell;
                        var status = eORDERSTATUS.FILLED;
                        var qtyFilled = obj.Size;
                        var priceFilled = obj.Price;

                        var _scenario = _random.Next(1, 6);
                        var ordersToSend = GenerateOrdersByRandomScenarios(_scenario, symbol, order_id, providerId, side, status, qtyFilled, priceFilled);
                        foreach (var order in ordersToSend)
                        {
                            HelperPosition.Instance.UpdateData(order);
                            // Wait for a random amount of time before sending the next order
                            Thread.Sleep(_random.Next(100, 5000));
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }
        public async Task StartSimulationAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            // Run the simulation for 5 minutes
            var simulationTask = Task.Run(() => FakeLoop(cancellationToken), cancellationToken);

            // Wait for 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5));

            // Cancel the simulation
            cancellationTokenSource.Cancel();

            HelperTrade.Instance.Unsubscribe(ProcessorTrades);

            // Wait for the simulation to complete
            await simulationTask;
        }

        private void FakeLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Thread.Sleep(300);
            }

        }
        private void RunSimulation(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Generate a random order
                    /*var order = GenerateRandomOrder();

                    // Update the data in HelperPosition
                    HelperPosition.Instance.UpdateData(order);*/


                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

            }
        }

        private List<Order> GenerateOrdersByRandomScenarios(int scenrio, string symbol, long orderId, int providerId, eORDERSIDE side, eORDERSTATUS status, decimal quantity, decimal price)
        {
            var aRet = new List<Order>();
            if (scenrio == 1)//NEW -> 100% FILLED
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;

                var order2 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.FILLED, quantity, price);
                order2.Quantity = quantity.ToDouble();
                order2.PricePlaced = price.ToDouble();
                order2.FilledQuantity = order2.Quantity;
                order1.Executions.First().Status = eORDERSTATUS.FILLED;
                order2.Executions.First().Price = price;
                order2.Executions.First().QtyFilled = quantity;


                aRet.Add(order1);
                aRet.Add(order2);
            }
            else if (scenrio == 2) // NEW -> leave it (working order)
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;
                aRet.Add(order1);
            }
            else if (scenrio == 3) //NEW -> 50% PARTIAL -> 50% CANCELED
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;

                var order2 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order2.Quantity = quantity.ToDouble();
                order2.PricePlaced = price.ToDouble();
                order2.FilledQuantity = quantity.ToDouble()/2;
                order2.Executions.First().Status = eORDERSTATUS.PARTIALFILLED;
                order2.Executions.First().Price = price;
                order2.Executions.First().QtyFilled = quantity/2;

                var order3 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.CANCELED, quantity, price);
                order3.Quantity = quantity.ToDouble();
                order3.PricePlaced = price.ToDouble();
                order3.FilledQuantity = 0;
                order3.Executions.First().Status = eORDERSTATUS.CANCELED;
                order3.Executions.First().Price = 0;
                order3.Executions.First().QtyFilled = 0;


                aRet.Add(order1);
                aRet.Add(order2);
                aRet.Add(order3);
            }
            else if (scenrio == 4) //NEW -> 50% PARTIAL -> 50% PARTIAL
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;

                var order2 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order2.Quantity = quantity.ToDouble();
                order2.PricePlaced = price.ToDouble();
                order2.FilledQuantity = quantity.ToDouble() / 2;
                order2.Executions.First().Status = eORDERSTATUS.PARTIALFILLED;
                order2.Executions.First().Price = price;
                order2.Executions.First().QtyFilled = quantity / 2;

                var order3 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.CANCELED, quantity, price);
                order3.Quantity = quantity.ToDouble();
                order3.PricePlaced = price.ToDouble();
                order3.FilledQuantity = quantity.ToDouble() / 2;
                order3.Executions.First().Status = eORDERSTATUS.PARTIALFILLED;
                order3.Executions.First().Price = price;
                order3.Executions.First().QtyFilled = quantity / 2;


                aRet.Add(order1);
                aRet.Add(order2);
                aRet.Add(order3);

            }
            else if (scenrio == 5) //NEW -> 100% CANCELED
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;
                var order2 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.CANCELED, quantity, price);
                order2.Quantity = quantity.ToDouble();
                order2.PricePlaced = price.ToDouble();
                order2.FilledQuantity = 0;
                order2.Executions.First().Status = eORDERSTATUS.CANCELED;
                order2.Executions.First().Price = 0;
                order2.Executions.First().QtyFilled = 0;
            }
            else if (scenrio == 6) //NEW -> 100% REJECTED
            {
                var order1 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.NEW, quantity, price);
                order1.Quantity = quantity.ToDouble();
                order1.PricePlaced = price.ToDouble();
                order1.FilledQuantity = 0;
                order1.Executions.First().Status = eORDERSTATUS.NEW;
                order1.Executions.First().Price = price;
                order1.Executions.First().QtyFilled = 0;
                var order2 = GenerateRandomOrder(symbol, orderId, providerId, side, eORDERSTATUS.REJECTED, quantity, price);
                order2.Quantity = quantity.ToDouble();
                order2.PricePlaced = price.ToDouble();
                order2.FilledQuantity = 0;
                order2.Executions.First().Status = eORDERSTATUS.REJECTED;
                order2.Executions.First().Price = 0;
                order2.Executions.First().QtyFilled = 0;
            }
            else
            {
                throw new ArgumentException("Invalid scenario number.");
            }

            return aRet;
        }
        private Order GenerateRandomOrder(string symbol, long orderId, int providerId, eORDERSIDE side, eORDERSTATUS status, decimal quantity, decimal price)
        {
            return new Order
            {
                OrderID = orderId,
                Symbol = symbol,
                ProviderId = providerId,
                Side = side,
                Status = status,
                Quantity = quantity.ToDouble(),
                FilledQuantity = (status == eORDERSTATUS.FILLED || status == eORDERSTATUS.PARTIALFILLED) ? quantity.ToDouble(): 0,
                PricePlaced = (status == eORDERSTATUS.FILLED || status == eORDERSTATUS.PARTIALFILLED) ? 0: price.ToDouble(),


                OrderType = eORDERTYPE.LIMIT,
                TimeInForce = eORDERTIMEINFORCE.GTC,
                Currency = "USD",
                Executions = new List<Execution>
                {
                    new Execution
                    {
                        OrderID = orderId,
                        Symbol = symbol,
                        ProviderID = providerId,
                        Price = (decimal)price,
                        QtyFilled = quantity,
                        Side = side,
                        Status = status,

                        ExecutionID = _random.Next(1, 100000),
                        LocalTimeStamp = DateTime.UtcNow,
                        ServerTimeStamp = DateTime.UtcNow,
                    }
                },
                CreationTimeStamp = DateTime.UtcNow,
            };
        }
    }
}
