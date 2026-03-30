"use client";

import { memo } from "react";
import type { TradeMessage } from "@/lib/types";

interface TradeTapeProps {
  trades: TradeMessage[];
}

function formatTime(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleTimeString("en-US", {
    hour12: false,
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }) + "." + String(d.getMilliseconds()).padStart(3, "0");
}

function TradeTapeInner({ trades }: TradeTapeProps) {
  if (trades.length === 0) {
    return (
      <div className="flex items-center justify-center h-full text-terminal-muted text-xs">
        AWAITING TRADES
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between px-3 py-1.5 border-b border-terminal-border shrink-0">
        <span className="text-[10px] uppercase tracking-widest text-terminal-muted font-semibold">
          Trade Tape
        </span>
        <span className="text-[10px] text-terminal-dim">
          {trades.length} trades
        </span>
      </div>

      {/* Column headers */}
      <div className="grid grid-cols-[80px_1fr_1fr_30px] gap-1 px-3 py-1 text-[9px] uppercase tracking-wider text-terminal-dim border-b border-terminal-border shrink-0">
        <span>Time</span>
        <span className="text-right">Price</span>
        <span className="text-right">Size</span>
        <span className="text-center">S</span>
      </div>

      {/* Trade rows */}
      <div className="flex-1 overflow-y-auto">
        {trades.map((trade, i) => (
          <div
            key={`${trade.timestamp}-${i}`}
            className={`grid grid-cols-[80px_1fr_1fr_30px] gap-1 px-3 py-[2px] text-xs border-b border-terminal-border/30 ${
              i === 0 ? "fade-in-row" : ""
            }`}
          >
            <span className="text-terminal-dim text-[10px] tabular-nums">
              {formatTime(trade.timestamp)}
            </span>
            <span
              className={`text-right font-medium tabular-nums ${
                trade.isBuy ? "text-terminal-green" : "text-terminal-red"
              }`}
            >
              {trade.price.toLocaleString(undefined, {
                minimumFractionDigits: 2,
                maximumFractionDigits: 2,
              })}
            </span>
            <span className="text-right text-terminal-muted tabular-nums">
              {trade.size.toFixed(5)}
            </span>
            <span
              className={`text-center text-[10px] font-bold ${
                trade.isBuy ? "text-terminal-green" : "text-terminal-red"
              }`}
            >
              {trade.isBuy ? "B" : "S"}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

export const TradeTape = memo(TradeTapeInner);
