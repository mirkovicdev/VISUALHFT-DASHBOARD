using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VisualHFT.Commons.Helpers;
using VisualHFT.Enums;
using VisualHFT.Model;

namespace VisualHFT.DataRetriever.TestingFramework.TestCases
{
    public class PositionTests
    {
        [Fact]
        public void Test_AddOrUpdateOrder_FailsIfOrderIDNotSet()
        {
            // Arrange
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                Status = eORDERSTATUS.NEW,
                Symbol = "AAPL",
                Side = eORDERSIDE.Buy,
                Quantity = 100,
                FilledQuantity = 100,
                PricePlaced = 150,
                CreationTimeStamp = DateTime.Now
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder));
        }
        [Fact]
        public void Test_AddOrUpdateOrder_FailsIfOrderStatusNotSet()
        {
            // Arrange
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 1,
                Symbol = "AAPL",
                Side = eORDERSIDE.Buy,
                Quantity = 100,
                FilledQuantity = 100,
                PricePlaced = 150,
                CreationTimeStamp = DateTime.Now
            };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder));
        }

        [Fact]
        public void Test_AddOrUpdateOrder_AddsNewOrder()
        {
            // Arrange
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 1,
                Status = eORDERSTATUS.NEW,
                Symbol = "AAPL",
                Side = eORDERSIDE.Buy,
                Quantity = 100,
                FilledQuantity = 100,
                PricePlaced = 150,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);

            // Assert
            Assert.NotNull(addedOrder);
            Assert.Null(updatedOrder);
            Assert.Equal(1, position.GetAllOrders(null).Count);
        }

        [Fact]
        public void Test_UpdateCurrentMidPrice_UpdatesPrice()
        {
            // Arrange
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 1,
                Status = eORDERSTATUS.NEW,
                Symbol = "AAPL",
                Side = eORDERSIDE.Buy,
                Quantity = 100,
                FilledQuantity = 100,
                PricePlaced = 150,
                CreationTimeStamp = DateTime.Now
            };
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            var initialMidPrice = 150.0;
            var newMidPrice = 155.0;

            // Act
            var needToChange = position.UpdateCurrentMidPrice(initialMidPrice);
            var needToChangeAgain = position.UpdateCurrentMidPrice(newMidPrice);

            // Assert
            Assert.True(needToChange);
            Assert.True(needToChangeAgain);
            Assert.Equal(newMidPrice, position.Exposure / position.NetPosition);
        }




        [Fact]
        public void Test_Scenario1_NewToFilled()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 1,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            order.Status = eORDERSTATUS.FILLED;
            order.FilledQuantity = 1;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(updatedOrder);
            Assert.Equal(eORDERSTATUS.FILLED, updatedOrder.Status);
            Assert.Equal(1, updatedOrder.FilledQuantity);
            Assert.Equal(1, position.NetPosition);
            Assert.Equal(60000, position.Exposure);
            Assert.Equal(10000, position.PLOpen); // Corrected
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(10000, position.PLTot); // Corrected
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(1, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }
        [Fact]
        public void Test_Scenario2_NewOrderRemainsOpen()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 2,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(addedOrder);
            Assert.Null(updatedOrder);
            Assert.Equal(eORDERSTATUS.NEW, addedOrder.Status);
            Assert.Equal(0, addedOrder.FilledQuantity);
            Assert.Equal(0, position.NetPosition);
            Assert.Equal(0, position.Exposure);
            Assert.Equal(0, position.PLOpen);
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(0, position.PLTot);
            Assert.Equal(1, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(0, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }
        [Fact]
        public void Test_Scenario3_PartialFillThenCancel()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 3,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 2,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            order.Status = eORDERSTATUS.PARTIALFILLED;
            order.FilledQuantity = 1;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            order.Status = eORDERSTATUS.CANCELED;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(updatedOrder);
            Assert.Equal(eORDERSTATUS.CANCELED, updatedOrder.Status);
            Assert.Equal(1, updatedOrder.FilledQuantity);
            Assert.Equal(1, position.NetPosition);
            Assert.Equal(60000, position.Exposure);
            Assert.Equal(10000, position.PLOpen); // Corrected
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(10000, position.PLTot); // Corrected
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(1, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }
        [Fact]
        public void Test_Scenario4_PartialFillToFullyFilled()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 4,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 2,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            order.Status = eORDERSTATUS.PARTIALFILLED;
            order.FilledQuantity = 1;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            order.FilledQuantity = 2;
            order.Status = eORDERSTATUS.FILLED;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(updatedOrder);
            Assert.Equal(eORDERSTATUS.FILLED, updatedOrder.Status);
            Assert.Equal(2, updatedOrder.FilledQuantity);
            Assert.Equal(2, position.NetPosition);
            Assert.Equal(120000, position.Exposure);
            Assert.Equal(20000, position.PLOpen); // Corrected
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(20000, position.PLTot); // Corrected
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(2, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }
        [Fact]
        public void Test_Scenario5_NewOrderThenCancelled()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 5,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            order.Status = eORDERSTATUS.CANCELED;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(updatedOrder);
            Assert.Equal(eORDERSTATUS.CANCELED, updatedOrder.Status);
            Assert.Equal(0, updatedOrder.FilledQuantity);
            Assert.Equal(0, position.NetPosition);
            Assert.Equal(0, position.Exposure);
            Assert.Equal(0, position.PLOpen);
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(0, position.PLTot);
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(0, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }

        [Fact]
        public void Test_Scenario6_NewOrderThenRejected()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 6,
                Status = eORDERSTATUS.NEW,
                Symbol = "BTC/USD",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };

            // Act
            position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            order.Status = eORDERSTATUS.REJECTED;
            position.AddOrUpdateOrder(order, out addedOrder, out updatedOrder);
            position.UpdateCurrentMidPrice(60000); //change market price

            // Assert
            Assert.NotNull(updatedOrder);
            Assert.Equal(eORDERSTATUS.REJECTED, updatedOrder.Status);
            Assert.Equal(0, updatedOrder.FilledQuantity);
            Assert.Equal(0, position.NetPosition);
            Assert.Equal(0, position.Exposure);
            Assert.Equal(0, position.PLOpen);
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(0, position.PLTot);
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(0, position.TotBuy);
            Assert.Equal(0, position.TotSell);
        }

        [Fact]
        public void Test_MultipleOrderEntriesWithStages()
        {
            // Arrange
            var position = new Position("BTC/USD", PositionManagerCalculationMethod.FIFO);
            var orders = new List<Order>
            {
                new Order
                {
                    OrderID = 1,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 2,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 2,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 2,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 3,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 1,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 4,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Sell,
                    Quantity = 1,
                    FilledQuantity = 0,
                    PricePlaced = 60000,
                    CreationTimeStamp = DateTime.Now
                }
            };

            // Act
            foreach (var order in orders)
            {
                position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder);
            }

            // Update orders to next stages
            orders[0].Status = eORDERSTATUS.PARTIALFILLED;
            orders[0].FilledQuantity = 1;
            position.AddOrUpdateOrder(orders[0], out var addedOrder1, out var updatedOrder1);

            orders[0].Status = eORDERSTATUS.FILLED;
            orders[0].FilledQuantity = 2;
            position.AddOrUpdateOrder(orders[0], out addedOrder1, out updatedOrder1);

            orders[1].Status = eORDERSTATUS.PARTIALFILLED;
            orders[1].FilledQuantity = 1;
            position.AddOrUpdateOrder(orders[1], out var addedOrder2, out var updatedOrder2);

            orders[1].Status = eORDERSTATUS.CANCELED;
            position.AddOrUpdateOrder(orders[1], out addedOrder2, out updatedOrder2);

            orders[2].Status = eORDERSTATUS.REJECTED;
            position.AddOrUpdateOrder(orders[2], out var addedOrder3, out var updatedOrder3);

            orders[3].Status = eORDERSTATUS.FILLED;
            orders[3].FilledQuantity = 1;
            position.AddOrUpdateOrder(orders[3], out var addedOrder4, out var updatedOrder4);

            position.UpdateCurrentMidPrice(60000); // change market price

            // Assert
            Assert.Equal(2, position.NetPosition); // 3 buys - 1 sell
            Assert.Equal(120000, position.Exposure); // (3 buys - 1 sell) * 120000
            Assert.Equal(20000, position.PLOpen); // (60000 - 50000) * 2
            Assert.Equal(10000, position.PLRealized);
            Assert.Equal(30000, position.PLTot); // PLOpen + PLRealized
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(3, position.TotBuy); // 3 filled buys
            Assert.Equal(1, position.TotSell); // 1 filled sell
        }

        [Fact]
        public void Test_MultipleOrderEntriesWithStagesWithPositionManager()
        {
            var orders = new List<Order>
            {
                new Order
                {
                    OrderID = 1,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 2,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 2,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 2,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 3,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Buy,
                    Quantity = 1,
                    FilledQuantity = 0,
                    PricePlaced = 50000,
                    CreationTimeStamp = DateTime.Now
                },
                new Order
                {
                    OrderID = 4,
                    Status = eORDERSTATUS.NEW,
                    Symbol = "BTC/USD",
                    Side = eORDERSIDE.Sell,
                    Quantity = 1,
                    FilledQuantity = 0,
                    PricePlaced = 60000,
                    CreationTimeStamp = DateTime.Now
                }
            };
            var positionHelper = HelperPosition.Instance;

            // Act
            foreach (var order in orders)
            {
                positionHelper.UpdateData(order);
            }

            // Update orders to next stages
            orders[0].Status = eORDERSTATUS.PARTIALFILLED;
            orders[0].FilledQuantity = 1;
            positionHelper.UpdateData(orders[0]);

            orders[0].Status = eORDERSTATUS.FILLED;
            orders[0].FilledQuantity = 2;
            positionHelper.UpdateData(orders[0]);

            orders[1].Status = eORDERSTATUS.PARTIALFILLED;
            orders[1].FilledQuantity = 1;
            positionHelper.UpdateData(orders[1]);

            orders[1].Status = eORDERSTATUS.CANCELED;
            positionHelper.UpdateData(orders[1]);

            orders[2].Status = eORDERSTATUS.REJECTED;
            positionHelper.UpdateData(orders[2]);

            orders[3].Status = eORDERSTATUS.FILLED;
            orders[3].FilledQuantity = 1;
            positionHelper.UpdateData(orders[3]);


            var position = HelperPosition.Instance.GetAllPositions().FirstOrDefault();
            position.UpdateCurrentMidPrice(60000); // change market price

            // Assert
            Assert.Equal(2, position.NetPosition); // 3 buys - 1 sell
            Assert.Equal(120000, position.Exposure); // (3 buys - 1 sell) * 120000
            Assert.Equal(20000, position.PLOpen); // (60000 - 50000) * 2
            Assert.Equal(10000, position.PLRealized);
            Assert.Equal(30000, position.PLTot); // PLOpen + PLRealized
            Assert.Equal(0, position.WrkBuy);
            Assert.Equal(0, position.WrkSell);
            Assert.Equal(3, position.TotBuy); // 3 filled buys
            Assert.Equal(1, position.TotSell); // 1 filled sell

            
        }

        [Fact]
        public void Test_AddOrder_WithDifferentSymbol_ShouldFail()
        {
            // Arrange: Create a position for symbol "AAPL"
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);
            var order = new Order
            {
                OrderID = 100,
                Status = eORDERSTATUS.NEW,
                Symbol = "GOOG", // Different symbol than the position
                Side = eORDERSIDE.Buy,
                Quantity = 100,
                FilledQuantity = 0,
                PricePlaced = 150,
                CreationTimeStamp = DateTime.Now
            };

            // Act & Assert: Expect an exception because the order's symbol doesn't match the position's symbol.
            Assert.Throws<ArgumentException>(() => position.AddOrUpdateOrder(order, out var addedOrder, out var updatedOrder));
        }


        [Fact]
        public void Test_UpdateMidPrice_WithZeroNetPosition_HasNoEffect()
        {
            // Arrange: Create a position with no orders (NetPosition is zero)
            var position = new Position("AAPL", PositionManagerCalculationMethod.FIFO);

            // Act: Update the mid price even though no orders exist.
            var needToChange = position.UpdateCurrentMidPrice(200);

            // Assert: The update flag may be true to indicate a market update,
            // but with no filled orders, all position metrics remain zero.
            Assert.True(needToChange);
            Assert.Equal(0, position.NetPosition);
            Assert.Equal(0, position.Exposure);
            Assert.Equal(0, position.PLOpen);
            Assert.Equal(0, position.PLRealized);
            Assert.Equal(0, position.PLTot);
        }

        [Fact]
        public void Test_FIFOOrderAggregation_WithDifferentPrices()
        {
            // Arrange: Create a position using FIFO method for symbol "TEST"
            var position = new Position("TEST", PositionManagerCalculationMethod.FIFO);

            // Order 1: Buy 1 at 50000
            var order1 = new Order
            {
                OrderID = 10,
                Status = eORDERSTATUS.NEW,
                Symbol = "TEST",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 50000,
                CreationTimeStamp = DateTime.Now
            };
            position.AddOrUpdateOrder(order1, out var addedOrder1, out var updatedOrder1);
            order1.Status = eORDERSTATUS.FILLED;
            order1.FilledQuantity = 1;
            position.AddOrUpdateOrder(order1, out addedOrder1, out updatedOrder1);

            // Order 2: Buy 1 at 60000
            var order2 = new Order
            {
                OrderID = 11,
                Status = eORDERSTATUS.NEW,
                Symbol = "TEST",
                Side = eORDERSIDE.Buy,
                Quantity = 1,
                FilledQuantity = 0,
                PricePlaced = 60000,
                CreationTimeStamp = DateTime.Now
            };
            position.AddOrUpdateOrder(order2, out var addedOrder2, out var updatedOrder2);
            order2.Status = eORDERSTATUS.FILLED;
            order2.FilledQuantity = 1;
            position.AddOrUpdateOrder(order2, out addedOrder2, out updatedOrder2);

            // Act: Update the mid price to 65000
            position.UpdateCurrentMidPrice(65000);

            // Assert:
            // NetPosition should be 2 (two filled buys)
            Assert.Equal(2, position.NetPosition);
            // Exposure should be the current market price times net position: 2 * 65000 = 130000.
            Assert.Equal(130000, position.Exposure);
            // PLOpen calculated using each order's price:
            // (65000 - 50000) + (65000 - 60000) = 15000 + 5000 = 20000.
            Assert.Equal(20000, position.PLOpen);
        }

        [Fact]
        public void Test_UpdateNonExistingOrder_AddsAsNew()
        {
            // Arrange: Create a position for symbol "MSFT"
            var position = new Position("MSFT", PositionManagerCalculationMethod.FIFO);
            // Create an update for an order that hasn't been added yet.
            var orderUpdate = new Order
            {
                OrderID = 999,
                Status = eORDERSTATUS.FILLED,
                Symbol = "MSFT",
                Side = eORDERSIDE.Buy,
                Quantity = 10,
                FilledQuantity = 10,
                PricePlaced = 250,
                CreationTimeStamp = DateTime.Now
            };

            // Act: Attempt to update an order that doesn't exist yet.
            position.AddOrUpdateOrder(orderUpdate, out var addedOrder, out var updatedOrder);

            // Assert: The order should be treated as a new addition.
            Assert.NotNull(addedOrder);
            Assert.Null(updatedOrder);
            Assert.Single(position.GetAllOrders(null));
        }


    }
}
