"use client";

import { memo, useEffect, useRef, useState } from "react";
import type { ChartPoint } from "@/lib/types";

interface PriceChartProps {
  midHistory: ChartPoint[];
  bidHistory: ChartPoint[];
  askHistory: ChartPoint[];
  midPrice: number | null;
  spread: number | null;
}

function PriceChartInner({ midHistory, bidHistory, askHistory, midPrice, spread }: PriceChartProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const chartRef = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const seriesRefs = useRef<{ mid: any; bid: any; ask: any }>({ mid: null, bid: null, ask: null });
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const el = containerRef.current;
    if (!el) return;

    let disposed = false;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let chart: any = null;
    let observer: ResizeObserver | null = null;

    function init() {
      if (disposed || !el || chartRef.current) return;
      const rect = el.getBoundingClientRect();
      if (rect.width === 0 || rect.height === 0) return;

      import("lightweight-charts").then((lc) => {
        if (disposed || chartRef.current) return;

        chart = lc.createChart(el, {
          width: rect.width,
          height: rect.height,
          layout: {
            background: { color: "#0a0a0a" },
            textColor: "#555555",
            fontFamily: "'JetBrains Mono', monospace",
            fontSize: 10,
          },
          grid: {
            vertLines: { color: "#141414" },
            horzLines: { color: "#141414" },
          },
          crosshair: {
            vertLine: { color: "#333", labelBackgroundColor: "#222" },
            horzLine: { color: "#333", labelBackgroundColor: "#222" },
          },
          rightPriceScale: {
            borderColor: "#1e1e1e",
            scaleMargins: { top: 0.08, bottom: 0.08 },
            autoScale: true,
          },
          timeScale: {
            borderColor: "#1e1e1e",
            timeVisible: true,
            secondsVisible: true,
          },
          handleScale: false,
          handleScroll: false,
        });

        // Ask line (red, behind)
        const askSeries = chart.addLineSeries({
          color: "rgba(255, 23, 68, 0.5)",
          lineWidth: 1,
          priceLineVisible: false,
          lastValueVisible: false,
          crosshairMarkerVisible: false,
        });

        // Bid line (green, behind)
        const bidSeries = chart.addLineSeries({
          color: "rgba(0, 200, 83, 0.5)",
          lineWidth: 1,
          priceLineVisible: false,
          lastValueVisible: false,
          crosshairMarkerVisible: false,
        });

        // Mid price line (amber, on top)
        const midSeries = chart.addLineSeries({
          color: "#ff9f1a",
          lineWidth: 2,
          priceLineVisible: true,
          priceLineColor: "#ff9f1a",
          lastValueVisible: true,
          crosshairMarkerVisible: true,
          crosshairMarkerRadius: 3,
        });

        chartRef.current = chart;
        seriesRefs.current = { mid: midSeries, bid: bidSeries, ask: askSeries };
        setReady(true);
      });
    }

    observer = new ResizeObserver(() => {
      if (!chartRef.current) {
        init();
      } else if (el) {
        const r = el.getBoundingClientRect();
        chartRef.current.applyOptions({ width: r.width, height: r.height });
      }
    });
    observer.observe(el);
    init();

    return () => {
      disposed = true;
      observer?.disconnect();
      if (chart) {
        chart.remove();
        chartRef.current = null;
        seriesRefs.current = { mid: null, bid: null, ask: null };
      }
    };
  }, []);

  // Sync full history when data accumulates
  useEffect(() => {
    if (!ready) return;
    const { mid, bid, ask } = seriesRefs.current;
    if (mid && midHistory.length > 0) {
      mid.setData(midHistory.map((p) => ({ time: p.time as import("lightweight-charts").UTCTimestamp, value: p.value })));
    }
    if (bid && bidHistory.length > 0) {
      bid.setData(bidHistory.map((p) => ({ time: p.time as import("lightweight-charts").UTCTimestamp, value: p.value })));
    }
    if (ask && askHistory.length > 0) {
      ask.setData(askHistory.map((p) => ({ time: p.time as import("lightweight-charts").UTCTimestamp, value: p.value })));
    }
  }, [midHistory, bidHistory, askHistory, ready]);

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between px-3 py-1.5 border-b border-terminal-border shrink-0">
        <div className="flex items-center gap-3">
          <span className="text-[10px] uppercase tracking-widest text-terminal-muted font-semibold">Price</span>
          <span className="inline-block w-3 h-[2px] bg-terminal-green rounded" />
          <span className="text-[9px] text-terminal-muted">BID</span>
          <span className="inline-block w-3 h-[2px] bg-terminal-amber rounded" />
          <span className="text-[9px] text-terminal-muted">MID</span>
          <span className="inline-block w-3 h-[2px] bg-terminal-red rounded" />
          <span className="text-[9px] text-terminal-muted">ASK</span>
        </div>
        <div className="flex items-center gap-3">
          {spread != null && spread > 0 && (
            <span className="text-[10px] text-terminal-dim">
              SPR <span className="text-terminal-muted">{spread.toFixed(2)}</span>
            </span>
          )}
          {midPrice != null && (
            <span className="text-xs text-terminal-amber font-semibold tabular-nums">
              {midPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
            </span>
          )}
        </div>
      </div>
      <div ref={containerRef} className="flex-1" style={{ minHeight: 0 }} />
    </div>
  );
}

export const PriceChart = memo(PriceChartInner);
