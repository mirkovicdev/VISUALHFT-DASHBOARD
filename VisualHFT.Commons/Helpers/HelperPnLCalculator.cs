using System;
using System.Collections.Generic;
using System.Linq;
using VisualHFT.Enums;

namespace VisualHFT.Helpers
{
    public static class HelperPnLCalculator
    {
        public static double CalculateRealizedPnL_OLD(List<VisualHFT.Model.Order> buys, List<VisualHFT.Model.Order> sells, PositionManagerCalculationMethod method)
        {
            double realizedPnL = 0;
            var buyOrders = method == PositionManagerCalculationMethod.FIFO ? buys.OrderBy(o => o.CreationTimeStamp).ToList() : buys.OrderByDescending(o => o.CreationTimeStamp).ToList();
            var sellOrders = method == PositionManagerCalculationMethod.FIFO ? sells.OrderBy(o => o.CreationTimeStamp).ToList() : sells.OrderByDescending(o => o.CreationTimeStamp).ToList();

            int buyIndex = 0;
            int sellIndex = 0;
            int maxIterations = buyOrders.Count + sellOrders.Count; // Maximum iterations to prevent infinite loop
            int iterationCount = 0;

            while (buyIndex < buyOrders.Count && sellIndex < sellOrders.Count)
            {
                if (iterationCount >= maxIterations)
                {
                    throw new InvalidOperationException("Exceeded maximum iterations while calculating realized PnL.");
                }

                var buy = buyOrders[buyIndex];
                var sell = sellOrders[sellIndex];

                var matchedQuantity = Math.Min(buy.FilledQuantity, Math.Abs(sell.FilledQuantity));

                realizedPnL += matchedQuantity * (sell.PricePlaced - buy.PricePlaced);

                buy.FilledQuantity -= matchedQuantity;
                sell.FilledQuantity += matchedQuantity; // Since sell quantity is negative, we add to reduce it

                if (buy.FilledQuantity == 0) buyIndex++;
                if (sell.FilledQuantity == 0) sellIndex++;

                iterationCount++;
            }
            return realizedPnL;
        }
        public static double CalculateRealizedPnL(List<VisualHFT.Model.Order> buys, List<VisualHFT.Model.Order> sells, PositionManagerCalculationMethod method)
        {
            double realizedPnL = 0;
            var buyOrders = method == PositionManagerCalculationMethod.FIFO ? buys.OrderBy(o => o.CreationTimeStamp).ToList() : buys.OrderByDescending(o => o.CreationTimeStamp).ToList();
            var sellOrders = method == PositionManagerCalculationMethod.FIFO ? sells.OrderBy(o => o.CreationTimeStamp).ToList() : sells.OrderByDescending(o => o.CreationTimeStamp).ToList();

            int buyIndex = 0;
            int sellIndex = 0;
            int maxIterations = buyOrders.Count + sellOrders.Count;
            int iterationCount = 0;

            double currentBuyRemainingQuantity = 0;
            double currentSellRemainingQuantity = 0;

            VisualHFT.Model.Order currentBuyOrder = null;
            VisualHFT.Model.Order currentSellOrder = null;


            while (buyIndex < buyOrders.Count && sellIndex < sellOrders.Count)
            {
                if (iterationCount >= maxIterations)
                {
                    throw new InvalidOperationException("Exceeded maximum iterations while calculating realized PnL (Immutable).");
                }

                currentBuyOrder = buyOrders[buyIndex];
                currentSellOrder = sellOrders[sellIndex];

                if (currentBuyRemainingQuantity == 0) currentBuyRemainingQuantity = currentBuyOrder.FilledQuantity;
                if (currentSellRemainingQuantity == 0) currentSellRemainingQuantity = Math.Abs(currentSellOrder.FilledQuantity);


                double matchedQuantity = Math.Min(currentBuyRemainingQuantity, currentSellRemainingQuantity);
                realizedPnL += matchedQuantity * (currentSellOrder.PricePlaced - currentBuyOrder.PricePlaced);


                currentBuyRemainingQuantity -= matchedQuantity;
                currentSellRemainingQuantity -= matchedQuantity;


                if (currentBuyRemainingQuantity == 0) buyIndex++;
                if (currentSellRemainingQuantity == 0) sellIndex++;


                iterationCount++;
            }

            return realizedPnL;
        }
        public static double CalculateOpenPnL_OLD(List<VisualHFT.Model.Order> buys, List<VisualHFT.Model.Order> sells, PositionManagerCalculationMethod method, double currentMidPrice)
        {
            double openPnL = 0;

            // Sort orders based on the method (FIFO or LIFO)
            var buyOrders = method == PositionManagerCalculationMethod.FIFO ? buys.OrderBy(o => o.CreationTimeStamp).ToList() : buys.OrderByDescending(o => o.CreationTimeStamp).ToList();
            var sellOrders = method == PositionManagerCalculationMethod.FIFO ? sells.OrderBy(o => o.CreationTimeStamp).ToList() : sells.OrderByDescending(o => o.CreationTimeStamp).ToList();

            // Match orders to determine remaining open positions
            int buyIndex = 0, sellIndex = 0;
            while (buyIndex < buyOrders.Count && sellIndex < sellOrders.Count)
            {
                var buy = buyOrders[buyIndex];
                var sell = sellOrders[sellIndex];

                var matchedQuantity = Math.Min(buy.FilledQuantity, Math.Abs(sell.FilledQuantity));

                buy.FilledQuantity -= matchedQuantity;
                sell.FilledQuantity += matchedQuantity; // Since sell quantity is negative, we add to reduce it

                if (buy.FilledQuantity == 0) buyIndex++;
                if (sell.FilledQuantity == 0) sellIndex++;
            }

            // Calculate Open PnL for remaining buy orders
            openPnL += buyOrders.Skip(buyIndex).Sum(x => x.FilledQuantity * (currentMidPrice - x.PricePlaced));

            // Calculate Open PnL for remaining sell orders
            openPnL += sellOrders.Skip(sellIndex).Sum(x => x.FilledQuantity * (x.PricePlaced - currentMidPrice));

            return openPnL;
        }
        public static double CalculateOpenPnL(List<VisualHFT.Model.Order> buys, List<VisualHFT.Model.Order> sells, PositionManagerCalculationMethod method, double currentMidPrice)
        {
            double openPnL = 0;

            // Sort orders based on the method (FIFO or LIFO) - Immutable sort
            var buyOrders = method == PositionManagerCalculationMethod.FIFO ? buys.OrderBy(o => o.CreationTimeStamp).ToList() : buys.OrderByDescending(o => o.CreationTimeStamp).ToList();
            var sellOrders = method == PositionManagerCalculationMethod.FIFO ? sells.OrderBy(o => o.CreationTimeStamp).ToList() : sells.OrderByDescending(o => o.CreationTimeStamp).ToList();

            int buyIndex = 0;
            int sellIndex = 0;
            int maxIterations = buyOrders.Count + sellOrders.Count;
            int iterationCount = 0;

            double currentBuyRemainingQuantity = 0;
            double currentSellRemainingQuantity = 0;

            VisualHFT.Model.Order currentBuyOrder = null;
            VisualHFT.Model.Order currentSellOrder = null;

            // Match orders to determine remaining open positions - Immutable matching
            while (buyIndex < buyOrders.Count && sellIndex < sellOrders.Count)
            {
                if (iterationCount >= maxIterations)
                {
                    throw new InvalidOperationException("Exceeded maximum iterations while calculating open PnL (Immutable).");
                }

                currentBuyOrder = buyOrders[buyIndex];
                currentSellOrder = sellOrders[sellIndex];

                if (currentBuyRemainingQuantity == 0) currentBuyRemainingQuantity = currentBuyOrder.FilledQuantity;
                if (currentSellRemainingQuantity == 0) currentSellRemainingQuantity = Math.Abs(currentSellOrder.FilledQuantity);


                double matchedQuantity = Math.Min(currentBuyRemainingQuantity, currentSellRemainingQuantity);

                // No realized PnL calculation here in OpenPnL method

                currentBuyRemainingQuantity -= matchedQuantity;
                currentSellRemainingQuantity -= matchedQuantity;

                if (currentBuyRemainingQuantity == 0) buyIndex++;
                if (currentSellRemainingQuantity == 0) sellIndex++;
                iterationCount++;
            }

            // Calculate Open PnL for remaining buy orders - Immutable calculation
            for (int i = buyIndex; i < buyOrders.Count; i++)
            {
                var buyOrder = buyOrders[i];
                double remainingBuyQty = buyOrder.FilledQuantity; // Use original FilledQuantity for remaining
                if (i == buyIndex && currentBuyRemainingQuantity > 0) // Add back remaining from current buy if not fully matched
                {
                    remainingBuyQty = currentBuyRemainingQuantity;
                }
                if (remainingBuyQty > 0)
                {
                    openPnL += (double)(remainingBuyQty * (currentMidPrice - buyOrder.PricePlaced));
                }
            }


            // Calculate Open PnL for remaining sell orders - Immutable calculation
            for (int i = sellIndex; i < sellOrders.Count; i++)
            {
                var sellOrder = sellOrders[i];
                double remainingSellQty = Math.Abs(sellOrder.FilledQuantity); // Use original FilledQuantity for remaining
                if (i == sellIndex && currentSellRemainingQuantity > 0) // Add back remaining from current sell if not fully matched
                {
                    remainingSellQty = currentSellRemainingQuantity;
                }
                if (remainingSellQty > 0)
                {
                    openPnL += (double)(remainingSellQty * (sellOrder.PricePlaced - currentMidPrice));
                }
            }

            return openPnL;
        }
    }
}
