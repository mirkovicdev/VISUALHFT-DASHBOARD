"use client";

import { memo, useState, useEffect } from "react";
import type { ProviderMessage, MarketState } from "@/lib/types";

interface StatusBarProps {
  connectionStatus: MarketState["connectionStatus"];
  provider: ProviderMessage | null;
  symbol: string;
  midPrice: number | null;
}

function StatusBarInner({ connectionStatus, provider, symbol, midPrice }: StatusBarProps) {
  const [clock, setClock] = useState("");

  useEffect(() => {
    const tick = () => setClock(new Date().toLocaleTimeString("en-US", { hour12: false }));
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, []);

  const statusColor =
    connectionStatus === "connected"
      ? "bg-terminal-green"
      : connectionStatus === "reconnecting"
      ? "bg-terminal-amber"
      : "bg-terminal-red";

  const statusText =
    connectionStatus === "connected"
      ? "CONNECTED"
      : connectionStatus === "reconnecting"
      ? "RECONNECTING"
      : "DISCONNECTED";

  return (
    <div className="flex items-center justify-between px-4 h-7 bg-terminal-surface border-t border-terminal-border text-[10px] tracking-wider uppercase text-terminal-muted font-medium">
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-1.5">
          <div className={`w-1.5 h-1.5 rounded-full ${statusColor}`} />
          <span>{statusText}</span>
        </div>
        {provider && (
          <span className="text-terminal-dim">
            {provider.providerName} ({provider.status})
          </span>
        )}
      </div>
      <div className="flex items-center gap-4">
        <span className="text-terminal-text">{symbol || "---"}</span>
        {midPrice != null && (
          <span className="text-terminal-amber font-semibold">
            {midPrice.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </span>
        )}
        <span className="text-terminal-dim">
          {clock}
        </span>
      </div>
    </div>
  );
}

export const StatusBar = memo(StatusBarInner);
