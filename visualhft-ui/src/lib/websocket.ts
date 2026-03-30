import { WsEnvelope, WsMessageType } from "./types";

type Listener = (data: unknown) => void;

export class MarketWebSocket {
  private ws: WebSocket | null = null;
  private url: string;
  private listeners: Map<WsMessageType, Set<Listener>> = new Map();
  private reconnectAttempt = 0;
  private maxReconnectDelay = 30000;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private disposed = false;
  private onStatusChange?: (status: "connected" | "disconnected" | "reconnecting") => void;

  constructor(url: string, onStatusChange?: (status: "connected" | "disconnected" | "reconnecting") => void) {
    this.url = url;
    this.onStatusChange = onStatusChange;
  }

  connect() {
    if (this.disposed) return;

    try {
      this.ws = new WebSocket(this.url);

      this.ws.onopen = () => {
        this.reconnectAttempt = 0;
        this.onStatusChange?.("connected");
      };

      this.ws.onmessage = (event) => {
        try {
          const envelope: WsEnvelope = JSON.parse(event.data);
          const handlers = this.listeners.get(envelope.type);
          if (handlers) {
            handlers.forEach((fn) => fn(envelope.data));
          }
        } catch {
          // malformed message
        }
      };

      this.ws.onclose = () => {
        if (!this.disposed) {
          this.onStatusChange?.("reconnecting");
          this.scheduleReconnect();
        }
      };

      this.ws.onerror = () => {
        // onclose will fire after onerror
      };
    } catch {
      this.scheduleReconnect();
    }
  }

  private scheduleReconnect() {
    if (this.disposed) return;
    const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempt), this.maxReconnectDelay);
    this.reconnectAttempt++;
    this.onStatusChange?.("reconnecting");
    this.reconnectTimer = setTimeout(() => this.connect(), delay);
  }

  on(type: WsMessageType, listener: Listener) {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, new Set());
    }
    this.listeners.get(type)!.add(listener);
  }

  off(type: WsMessageType, listener: Listener) {
    this.listeners.get(type)?.delete(listener);
  }

  dispose() {
    this.disposed = true;
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
    if (this.ws) {
      this.ws.onclose = null;
      this.ws.close();
    }
    this.listeners.clear();
  }
}
