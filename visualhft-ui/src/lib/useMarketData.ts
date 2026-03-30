"use client";

import { useEffect, useReducer, useRef, useCallback, useState } from "react";
import { MarketWebSocket } from "./websocket";
import type {
  MarketState,
  OrderBookMessage,
  TradeMessage,
  StudyMessage,
  ProviderMessage,
  ChartPoint,
} from "./types";

const MAX_TRADES = 150;
const MAX_HISTORY = 600;
const WS_URL = "ws://localhost:5000/ws";
const DEFAULT_SYMBOL = "BTC/USD";

type Action =
  | { type: "orderbook"; data: OrderBookMessage }
  | { type: "trade"; data: TradeMessage }
  | { type: "study"; data: StudyMessage }
  | { type: "provider"; data: ProviderMessage }
  | { type: "status"; status: MarketState["connectionStatus"] }
  | { type: "clear" };

const initialState: MarketState = {
  orderBook: null,
  trades: [],
  studies: {},
  provider: null,
  connectionStatus: "disconnected",
  spreadHistory: [],
  studyHistory: {},
  priceHistory: { mid: [], bid: [], ask: [] },
};

function pushPoint(arr: ChartPoint[], pt: ChartPoint, max: number): ChartPoint[] {
  if (arr.length > 0 && arr[arr.length - 1].time >= pt.time) return arr;
  const next = [...arr, pt];
  return next.length > max ? next.slice(next.length - max) : next;
}

function reducer(state: MarketState, action: Action): MarketState {
  switch (action.type) {
    case "clear":
      return { ...initialState, connectionStatus: state.connectionStatus, provider: state.provider };
    case "orderbook": {
      const ob = action.data;
      const t = Math.floor(new Date(ob.timestamp).getTime() / 1000);
      return {
        ...state,
        orderBook: ob,
        spreadHistory: pushPoint(state.spreadHistory, { time: t, value: ob.spread }, MAX_HISTORY),
        priceHistory: {
          mid: pushPoint(state.priceHistory.mid, { time: t, value: ob.midPrice }, MAX_HISTORY),
          bid: pushPoint(state.priceHistory.bid, { time: t, value: ob.bestBid }, MAX_HISTORY),
          ask: pushPoint(state.priceHistory.ask, { time: t, value: ob.bestAsk }, MAX_HISTORY),
        },
      };
    }
    case "trade":
      return {
        ...state,
        trades: [action.data, ...state.trades].slice(0, MAX_TRADES),
      };
    case "study": {
      const s = action.data;
      const t = Math.floor(new Date(s.timestamp).getTime() / 1000);
      const key = s.studyName;
      const prev = state.studyHistory[key] ?? [];
      return {
        ...state,
        studies: { ...state.studies, [key]: s },
        studyHistory: {
          ...state.studyHistory,
          [key]: pushPoint(prev, { time: t, value: s.value }, MAX_HISTORY),
        },
      };
    }
    case "provider":
      return { ...state, provider: action.data };
    case "status":
      return { ...state, connectionStatus: action.status };
    default:
      return state;
  }
}

export function useMarketData() {
  const [state, dispatch] = useReducer(reducer, initialState);
  const [selectedSymbol, setSelectedSymbol] = useState(DEFAULT_SYMBOL);
  const [availableSymbols, setAvailableSymbols] = useState<string[]>([]);
  const symbolRef = useRef(selectedSymbol);
  const seenSymbolsRef = useRef(new Set<string>());
  const wsRef = useRef<MarketWebSocket | null>(null);

  // Keep ref in sync
  symbolRef.current = selectedSymbol;

  const handleStatus = useCallback(
    (status: MarketState["connectionStatus"]) => {
      dispatch({ type: "status", status });
    },
    []
  );

  // Clear history when symbol changes
  const selectSymbol = useCallback((sym: string) => {
    setSelectedSymbol(sym);
    dispatch({ type: "clear" });
  }, []);

  useEffect(() => {
    const ws = new MarketWebSocket(WS_URL, handleStatus);
    wsRef.current = ws;

    ws.on("orderbook", (raw) => {
      const data = raw as OrderBookMessage;
      // Track all symbols we see
      if (!seenSymbolsRef.current.has(data.symbol)) {
        seenSymbolsRef.current.add(data.symbol);
        setAvailableSymbols(Array.from(seenSymbolsRef.current).sort());
      }
      if (data.symbol === symbolRef.current) dispatch({ type: "orderbook", data });
    });
    ws.on("trade", (raw) => {
      const data = raw as TradeMessage;
      if (data.symbol === symbolRef.current) dispatch({ type: "trade", data });
    });
    ws.on("study", (raw) => {
      const data = raw as StudyMessage;
      dispatch({ type: "study", data });
    });
    ws.on("provider", (data) =>
      dispatch({ type: "provider", data: data as ProviderMessage })
    );

    ws.connect();

    return () => {
      ws.dispose();
      wsRef.current = null;
    };
  }, [handleStatus]);

  return { ...state, selectedSymbol, availableSymbols, selectSymbol };
}
