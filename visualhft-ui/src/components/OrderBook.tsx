"use client";

import { memo, useMemo } from "react";
import type { OrderBookMessage } from "@/lib/types";

interface OrderBookProps {
  data: OrderBookMessage | null;
}

function OrderBookInner({ data }: OrderBookProps) {
  const { bids, asks, maxSize, spreadPct } = useMemo(() => {
    if (!data) return { bids: [], asks: [], maxSize: 1, spreadPct: "" };

    const b = data.bids.slice(0, 15);
    const a = data.asks.slice(0, 15);
    const allSizes = [...b, ...a].map((l) => l.size);
    const max = Math.max(...allSizes, 1);

    const sp = data.midPrice > 0
      ? ((data.spread / data.midPrice) * 100).toFixed(3)
      : "0.000";

    return { bids: b, asks: a, maxSize: max, spreadPct: sp };
  }, [data]);

  if (!data) {
    return (
      <div className="flex items-center justify-center h-full text-terminal-muted text-xs">
        AWAITING DATA
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-1.5 border-b border-terminal-border">
        <span className="text-[10px] uppercase tracking-widest text-terminal-muted font-semibold">
          Order Book
        </span>
        <span className="text-[10px] text-terminal-muted">
          Depth {data.bids.length + data.asks.length}
        </span>
      </div>

      {/* Spread bar */}
      <div className="flex items-center justify-center gap-2 py-1 bg-terminal-surface-2 border-b border-terminal-border">
        <span className="text-[10px] text-terminal-muted">SPREAD</span>
        <span className="text-xs font-semibold text-terminal-amber">
          {data.spread.toFixed(2)}
        </span>
        <span className="text-[10px] text-terminal-dim">({spreadPct}%)</span>
      </div>

      {/* Asks (reversed — best ask at bottom) */}
      <div className="flex-1 overflow-hidden flex flex-col justify-end">
        {[...asks].reverse().map((level, i) => (
          <div key={`a-${i}`} className="relative flex items-center px-3 py-[1px] text-xs group">
            <div
              className="absolute right-0 top-0 bottom-0 bg-terminal-red/8"
              style={{ width: `${(level.size / maxSize) * 100}%` }}
            />
            <span className="w-24 text-right text-terminal-red font-medium z-10">
              {level.price.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="flex-1 text-right text-terminal-muted z-10 pr-1">
              {level.size.toFixed(4)}
            </span>
          </div>
        ))}
      </div>

      {/* Mid price divider */}
      <div className="flex items-center justify-center py-1 border-y border-terminal-amber/20 bg-terminal-amber/5">
        <span className="text-sm font-bold text-terminal-amber tracking-wide">
          {data.midPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
        </span>
      </div>

      {/* Bids */}
      <div className="flex-1 overflow-hidden">
        {bids.map((level, i) => (
          <div key={`b-${i}`} className="relative flex items-center px-3 py-[1px] text-xs group">
            <div
              className="absolute right-0 top-0 bottom-0 bg-terminal-green/8"
              style={{ width: `${(level.size / maxSize) * 100}%` }}
            />
            <span className="w-24 text-right text-terminal-green font-medium z-10">
              {level.price.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
            <span className="flex-1 text-right text-terminal-muted z-10 pr-1">
              {level.size.toFixed(4)}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

export const OrderBook = memo(OrderBookInner);
