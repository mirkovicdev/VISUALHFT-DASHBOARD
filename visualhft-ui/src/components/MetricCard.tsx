"use client";

import { memo } from "react";
import type { StudyMessage } from "@/lib/types";

interface MetricCardProps {
  title: string;
  study: StudyMessage | undefined;
  min?: number;
  max?: number;
  center?: number; // for diverging indicators like LOB imbalance
  invert?: boolean; // higher = worse (like VPIN)
  suffix?: string;
}

function getBarColor(value: number, min: number, max: number, invert: boolean, center?: number): string {
  if (center !== undefined) {
    // Diverging: green for positive, red for negative
    return value >= 0 ? "#00c853" : "#ff1744";
  }
  // Sequential: green-to-red (or inverted)
  const normalized = (value - min) / (max - min);
  if (invert) {
    return normalized > 0.7 ? "#ff1744" : normalized > 0.4 ? "#ff9f1a" : "#00c853";
  }
  return normalized > 0.7 ? "#00c853" : normalized > 0.4 ? "#ff9f1a" : "#ff1744";
}

function MetricCardInner({ title, study, min = 0, max = 1, center, invert = false, suffix }: MetricCardProps) {
  const hasData = study?.hasData ?? false;
  const hasError = study?.hasError ?? false;
  const isStale = study?.isStale ?? false;
  const value = study?.value ?? 0;

  const barColor = hasData
    ? getBarColor(value, min, max, invert, center)
    : "#333";

  // Bar fill percentage
  let fillPct: number;
  if (center !== undefined) {
    fillPct = Math.abs(value - center) / (max - center) * 50;
  } else {
    fillPct = ((value - min) / (max - min)) * 100;
  }
  fillPct = Math.max(0, Math.min(100, fillPct));

  // Format value
  let displayValue: string;
  if (hasError) {
    displayValue = "ERR";
  } else if (!hasData) {
    displayValue = "---";
  } else if (isStale) {
    displayValue = "...";
  } else {
    displayValue = value.toFixed(2) + (suffix || "");
  }

  return (
    <div className="flex flex-col bg-terminal-surface border border-terminal-border h-full">
      {/* Title */}
      <div className="px-3 py-1.5 border-b border-terminal-border">
        <span className="text-[9px] uppercase tracking-[0.15em] text-terminal-muted font-semibold">
          {title}
        </span>
      </div>

      {/* Value */}
      <div className="flex-1 flex flex-col items-center justify-center px-3 py-2">
        <span
          className={`text-2xl font-bold tabular-nums tracking-tight ${
            hasError
              ? "text-terminal-red"
              : !hasData
              ? "text-terminal-dim pulse-dot"
              : isStale
              ? "text-terminal-dim"
              : "text-terminal-text"
          }`}
        >
          {displayValue}
        </span>
      </div>

      {/* Bar */}
      <div className="px-3 pb-2">
        <div className="h-1 bg-terminal-surface-2 rounded-full overflow-hidden">
          {center !== undefined ? (
            // Diverging bar
            <div className="relative h-full">
              <div
                className="absolute top-0 h-full rounded-full transition-all duration-300"
                style={{
                  backgroundColor: barColor,
                  left: value >= center ? "50%" : `${50 - fillPct}%`,
                  width: `${fillPct}%`,
                }}
              />
            </div>
          ) : (
            <div
              className="h-full rounded-full transition-all duration-300"
              style={{
                width: `${fillPct}%`,
                backgroundColor: barColor,
              }}
            />
          )}
        </div>
      </div>
    </div>
  );
}

export const MetricCard = memo(MetricCardInner);
