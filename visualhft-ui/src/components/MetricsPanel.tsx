"use client";

import { memo } from "react";
import type { StudyMessage } from "@/lib/types";
import { MetricCard } from "./MetricCard";

interface MetricsPanelProps {
  studies: Record<string, StudyMessage>;
}

function MetricsPanelInner({ studies }: MetricsPanelProps) {
  return (
    <div className="grid grid-cols-2 grid-rows-2 gap-px bg-terminal-border h-full">
      <MetricCard
        title="VPIN"
        study={studies["VPIN Study Plugin"]}
        min={0}
        max={1}
        invert={true}
      />
      <MetricCard
        title="LOB Imbal."
        study={studies["LOB Imbalance Study Plugin"]}
        min={-1}
        max={1}
        center={0}
      />
      <MetricCard
        title="Resilience"
        study={studies["Market Resilience Study"]}
        min={0}
        max={1}
        invert={false}
      />
      <MetricCard
        title="OTT Ratio"
        study={studies["Order To Trade Ratio Study Plugin"]}
        min={0}
        max={50}
        invert={true}
      />
    </div>
  );
}

export const MetricsPanel = memo(MetricsPanelInner);
