"use client";

import { useMarketData } from "@/lib/useMarketData";
import { OrderBook } from "@/components/OrderBook";
import { PriceChart } from "@/components/PriceChart";
import { SpreadChart } from "@/components/SpreadChart";
import { DepthChart } from "@/components/DepthChart";
import { StudyChart } from "@/components/StudyChart";
import { TradeTape } from "@/components/TradeTape";
import { MetricsPanel } from "@/components/MetricsPanel";
import { StatusBar } from "@/components/StatusBar";

export default function Dashboard() {
  const {
    orderBook,
    trades,
    studies,
    provider,
    connectionStatus,
    spreadHistory,
    studyHistory,
    priceHistory,
    selectedSymbol,
    availableSymbols,
    selectSymbol,
  } = useMarketData();

  const midPrice = orderBook?.midPrice ?? null;

  return (
    <div className="h-screen w-screen flex flex-col bg-terminal-bg overflow-hidden">
      {/* Top bar */}
      <header className="flex items-center justify-between px-4 h-9 bg-terminal-surface border-b border-terminal-border shrink-0">
        <div className="flex items-center gap-3">
          <span className="text-xs font-bold tracking-[0.2em] text-terminal-amber">VISUALHFT</span>
          <span className="text-terminal-dim text-[10px]">/</span>

          {/* Symbol selector */}
          {availableSymbols.length > 1 ? (
            <div className="flex items-center gap-1">
              {availableSymbols.map((sym) => (
                <button
                  key={sym}
                  onClick={() => selectSymbol(sym)}
                  className={`px-2 py-0.5 text-[11px] font-semibold tracking-wide border transition-colors ${
                    sym === selectedSymbol
                      ? "border-terminal-amber/40 bg-terminal-amber/10 text-terminal-amber"
                      : "border-terminal-border text-terminal-muted hover:border-terminal-muted hover:text-terminal-text"
                  }`}
                >
                  {sym}
                </button>
              ))}
            </div>
          ) : (
            <span className="text-xs text-terminal-text font-semibold tracking-wide">
              {selectedSymbol}
            </span>
          )}

          {provider && (
            <>
              <span className="text-terminal-dim text-[10px]">/</span>
              <span className="text-[10px] text-terminal-muted uppercase">{provider.providerName}</span>
            </>
          )}
        </div>
        <div className="flex items-center gap-1.5">
          <div className={`w-1.5 h-1.5 rounded-full ${connectionStatus === "connected" ? "bg-terminal-green" : connectionStatus === "reconnecting" ? "bg-terminal-amber" : "bg-terminal-red"}`} />
          <span className="text-[9px] uppercase tracking-widest text-terminal-muted">
            {connectionStatus === "connected" ? "LIVE" : connectionStatus === "reconnecting" ? "RECONNECTING" : "OFFLINE"}
          </span>
        </div>
      </header>

      {/* Main content: left order book + right grid */}
      <div className="flex-1 min-h-0 grid grid-cols-[280px_1fr] gap-px bg-terminal-border">
        {/* LEFT: Order Book full height */}
        <div className="bg-terminal-bg overflow-hidden">
          <OrderBook data={orderBook} />
        </div>

        {/* RIGHT: uniform grid of charts + panels */}
        <div className="grid grid-cols-3 grid-rows-[1fr_1fr_1fr] gap-px bg-terminal-border min-h-0">
          {/* Row 1 */}
          <div className="col-span-2 bg-terminal-bg overflow-hidden min-h-0">
            <PriceChart
              midHistory={priceHistory.mid}
              bidHistory={priceHistory.bid}
              askHistory={priceHistory.ask}
              midPrice={midPrice}
              spread={orderBook?.spread ?? null}
            />
          </div>
          <div className="bg-terminal-bg overflow-hidden min-h-0">
            <StudyChart
              title="VPIN"
              history={studyHistory["VPIN Study Plugin"] ?? []}
              currentValue={studies["VPIN Study Plugin"]?.value ?? null}
              color="#ff9f1a"
              fillColor="rgba(255, 159, 26, 0.12)"
            />
          </div>

          {/* Row 2 */}
          <div className="bg-terminal-bg overflow-hidden min-h-0">
            <SpreadChart
              history={spreadHistory}
              currentSpread={orderBook?.spread ?? null}
            />
          </div>
          <div className="bg-terminal-bg overflow-hidden min-h-0">
            <StudyChart
              title="LOB Imbalance"
              history={studyHistory["LOB Imbalance Study Plugin"] ?? []}
              currentValue={studies["LOB Imbalance Study Plugin"]?.value ?? null}
              color="#00bcd4"
              fillColor="rgba(0, 188, 212, 0.12)"
            />
          </div>
          <div className="bg-terminal-bg overflow-hidden min-h-0">
            <StudyChart
              title="OTT Ratio"
              history={studyHistory["Order To Trade Ratio Study Plugin"] ?? []}
              currentValue={studies["Order To Trade Ratio Study Plugin"]?.value ?? null}
              color="#e040fb"
              fillColor="rgba(224, 64, 251, 0.12)"
            />
          </div>

          {/* Row 3 */}
          <div className="bg-terminal-bg overflow-hidden min-h-0">
            <DepthChart data={orderBook} />
          </div>
          <div className="bg-terminal-bg overflow-hidden">
            <TradeTape trades={trades} />
          </div>
          <div className="bg-terminal-bg overflow-hidden">
            <MetricsPanel studies={studies} />
          </div>
        </div>
      </div>

      {/* Status bar */}
      <StatusBar connectionStatus={connectionStatus} provider={provider} symbol={selectedSymbol} midPrice={midPrice} />
    </div>
  );
}
