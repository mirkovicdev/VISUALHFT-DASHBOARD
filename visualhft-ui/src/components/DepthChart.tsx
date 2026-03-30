"use client";

import { memo, useRef, useEffect } from "react";
import type { OrderBookMessage } from "@/lib/types";

interface DepthChartProps {
  data: OrderBookMessage | null;
}

function DepthChartInner({ data }: DepthChartProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !data) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const parent = canvas.parentElement;
    if (!parent) return;

    const w = parent.clientWidth;
    const h = parent.clientHeight;
    canvas.width = w;
    canvas.height = h;

    ctx.fillStyle = "#0a0a0a";
    ctx.fillRect(0, 0, w, h);

    const bids = data.bids.slice(0, 15);
    const asks = data.asks.slice(0, 15);
    if (bids.length === 0 && asks.length === 0) return;

    const allSizes = [...bids, ...asks].map((l) => l.size);
    const maxSize = Math.max(...allSizes, 0.001);

    // Cumulative depth
    let bidCum = 0;
    const bidCumulative = bids.map((l) => {
      bidCum += l.size;
      return { price: l.price, cum: bidCum };
    });

    let askCum = 0;
    const askCumulative = asks.map((l) => {
      askCum += l.size;
      return { price: l.price, cum: askCum };
    });

    const maxCum = Math.max(bidCum, askCum, 0.001);
    const midX = w / 2;

    // Price range for X mapping
    const lowestBid = bids.length > 0 ? bids[bids.length - 1].price : data.midPrice;
    const highestAsk = asks.length > 0 ? asks[asks.length - 1].price : data.midPrice;
    const priceRange = highestAsk - lowestBid || 1;

    function priceToX(price: number): number {
      return ((price - lowestBid) / priceRange) * w;
    }

    function cumToY(cum: number): number {
      return h - (cum / maxCum) * (h - 20);
    }

    // Draw bid area (green)
    ctx.beginPath();
    ctx.moveTo(midX, h);
    for (const pt of bidCumulative) {
      ctx.lineTo(priceToX(pt.price), cumToY(pt.cum));
    }
    if (bidCumulative.length > 0) {
      ctx.lineTo(priceToX(bidCumulative[bidCumulative.length - 1].price), h);
    }
    ctx.closePath();
    const bidGrad = ctx.createLinearGradient(0, 0, 0, h);
    bidGrad.addColorStop(0, "rgba(0, 200, 83, 0.3)");
    bidGrad.addColorStop(1, "rgba(0, 200, 83, 0.02)");
    ctx.fillStyle = bidGrad;
    ctx.fill();

    // Bid line
    ctx.beginPath();
    ctx.strokeStyle = "rgba(0, 200, 83, 0.7)";
    ctx.lineWidth = 1.5;
    for (let i = 0; i < bidCumulative.length; i++) {
      const x = priceToX(bidCumulative[i].price);
      const y = cumToY(bidCumulative[i].cum);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.stroke();

    // Draw ask area (red)
    ctx.beginPath();
    ctx.moveTo(midX, h);
    for (const pt of askCumulative) {
      ctx.lineTo(priceToX(pt.price), cumToY(pt.cum));
    }
    if (askCumulative.length > 0) {
      ctx.lineTo(priceToX(askCumulative[askCumulative.length - 1].price), h);
    }
    ctx.closePath();
    const askGrad = ctx.createLinearGradient(0, 0, 0, h);
    askGrad.addColorStop(0, "rgba(255, 23, 68, 0.3)");
    askGrad.addColorStop(1, "rgba(255, 23, 68, 0.02)");
    ctx.fillStyle = askGrad;
    ctx.fill();

    // Ask line
    ctx.beginPath();
    ctx.strokeStyle = "rgba(255, 23, 68, 0.7)";
    ctx.lineWidth = 1.5;
    for (let i = 0; i < askCumulative.length; i++) {
      const x = priceToX(askCumulative[i].price);
      const y = cumToY(askCumulative[i].cum);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    }
    ctx.stroke();

    // Volume dots at each level
    for (const bid of bids) {
      const x = priceToX(bid.price);
      const r = 2 + (bid.size / maxSize) * 6;
      ctx.beginPath();
      ctx.arc(x, h - 8, r, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(0, 200, 83, 0.6)";
      ctx.fill();
    }
    for (const ask of asks) {
      const x = priceToX(ask.price);
      const r = 2 + (ask.size / maxSize) * 6;
      ctx.beginPath();
      ctx.arc(x, h - 8, r, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(255, 23, 68, 0.6)";
      ctx.fill();
    }

    // Mid price line
    const midPx = priceToX(data.midPrice);
    ctx.beginPath();
    ctx.strokeStyle = "rgba(255, 159, 26, 0.4)";
    ctx.lineWidth = 1;
    ctx.setLineDash([3, 3]);
    ctx.moveTo(midPx, 0);
    ctx.lineTo(midPx, h);
    ctx.stroke();
    ctx.setLineDash([]);

  }, [data]);

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between px-3 py-1 border-b border-terminal-border shrink-0">
        <span className="text-[9px] uppercase tracking-widest text-terminal-muted font-semibold">
          Market Depth
        </span>
        <div className="flex items-center gap-2">
          <span className="inline-block w-2 h-2 rounded-full bg-terminal-green/60" />
          <span className="text-[8px] text-terminal-muted">BIDS</span>
          <span className="inline-block w-2 h-2 rounded-full bg-terminal-red/60" />
          <span className="text-[8px] text-terminal-muted">ASKS</span>
        </div>
      </div>
      <div className="flex-1 relative" style={{ minHeight: 0 }}>
        <canvas ref={canvasRef} className="absolute inset-0 w-full h-full" />
      </div>
    </div>
  );
}

export const DepthChart = memo(DepthChartInner);
