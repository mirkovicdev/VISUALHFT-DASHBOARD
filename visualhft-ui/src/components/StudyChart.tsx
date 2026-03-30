"use client";

import { memo, useEffect, useRef, useState } from "react";
import type { ChartPoint } from "@/lib/types";

interface StudyChartProps {
  title: string;
  history: ChartPoint[];
  currentValue: number | null;
  color: string;
  fillColor?: string;
  format?: (v: number) => string;
}

function StudyChartInner({ title, history, currentValue, color, fillColor, format }: StudyChartProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const chartRef = useRef<any>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const seriesRef = useRef<any>(null);
  const [ready, setReady] = useState(false);
  const colorRef = useRef(color);
  const fillRef = useRef(fillColor);
  colorRef.current = color;
  fillRef.current = fillColor;

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
            fontSize: 9,
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
            scaleMargins: { top: 0.1, bottom: 0.05 },
          },
          timeScale: {
            borderColor: "#1e1e1e",
            timeVisible: true,
            secondsVisible: false,
            visible: false,
          },
          handleScale: false,
          handleScroll: false,
        });

        const series = chart.addAreaSeries({
          lineColor: colorRef.current,
          topColor: fillRef.current || colorRef.current.replace(")", ", 0.15)").replace("rgb(", "rgba("),
          bottomColor: "transparent",
          lineWidth: 1,
          priceLineVisible: false,
          lastValueVisible: true,
          crosshairMarkerVisible: true,
          crosshairMarkerRadius: 2,
        });

        chartRef.current = chart;
        seriesRef.current = series;
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
        seriesRef.current = null;
      }
    };
  }, []);

  useEffect(() => {
    if (!ready || !seriesRef.current || history.length === 0) return;
    seriesRef.current.setData(
      history.map((p) => ({ time: p.time as import("lightweight-charts").UTCTimestamp, value: p.value }))
    );
  }, [history, ready]);

  const fmt = format || ((v: number) => v.toFixed(2));

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between px-3 py-1 border-b border-terminal-border shrink-0">
        <span className="text-[9px] uppercase tracking-widest text-terminal-muted font-semibold">{title}</span>
        {currentValue != null && (
          <span className="text-[10px] font-semibold tabular-nums" style={{ color }}>
            {fmt(currentValue)}
          </span>
        )}
      </div>
      <div ref={containerRef} className="flex-1" style={{ minHeight: 0 }} />
    </div>
  );
}

export const StudyChart = memo(StudyChartInner);
