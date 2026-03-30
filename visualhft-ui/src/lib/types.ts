export interface BookLevel {
  price: number;
  size: number;
}

export interface OrderBookMessage {
  symbol: string;
  providerId: number;
  providerName: string;
  midPrice: number;
  spread: number;
  imbalance: number;
  bestBid: number;
  bestAsk: number;
  sequence: number;
  timestamp: string;
  bids: BookLevel[];
  asks: BookLevel[];
}

export interface ChartPoint {
  time: number; // unix seconds
  value: number;
}

export interface DepthSnapshot {
  time: number;
  bids: BookLevel[];
  asks: BookLevel[];
}

export interface TradeMessage {
  symbol: string;
  price: number;
  size: number;
  isBuy: boolean;
  timestamp: string;
  marketMidPrice: number;
}

export interface StudyMessage {
  studyName: string;
  symbol: string;
  value: number;
  format: string;
  timestamp: string;
  valueColor: string | null;
  marketMidPrice: number;
  hasData: boolean;
  hasError: boolean;
  isStale: boolean;
}

export interface ProviderMessage {
  providerId: number;
  providerName: string;
  status: string;
  lastUpdated: string;
}

export type WsMessageType = "orderbook" | "trade" | "study" | "provider";

export interface WsEnvelope {
  type: WsMessageType;
  data: unknown;
}

export interface MarketState {
  orderBook: OrderBookMessage | null;
  trades: TradeMessage[];
  studies: Record<string, StudyMessage>;
  provider: ProviderMessage | null;
  connectionStatus: "connected" | "disconnected" | "reconnecting";
  spreadHistory: ChartPoint[];
  studyHistory: Record<string, ChartPoint[]>;
  priceHistory: { mid: ChartPoint[]; bid: ChartPoint[]; ask: ChartPoint[] };
}
