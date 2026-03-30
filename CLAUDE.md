# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

```bash
# Build the entire solution
dotnet build VisualHFT.sln

# Build a specific project
dotnet build VisualHFT.csproj

# Run tests (xUnit v3)
dotnet test VisualHFT.DataRetriever.TestingFramework/VisualHFT.DataRetriever.TestingFramework.csproj
dotnet test VisualHFT.Plugins/Studies.MarketResilience.Test/Studies.MarketResilience.Test.csproj

# Run a single test
dotnet test --filter "FullyQualifiedName~TestClassName.TestMethodName"
```

The application can also be built in Visual Studio 2022 (`F6` / Build > Build Solution).

## Architecture Overview

**C# WPF desktop application** (.NET 8.0-windows) for real-time market microstructure analysis. Uses **MVVM** pattern with a **plugin architecture**.

### Core Projects

- **VisualHFT** — Main WPF app (entry: `App.xaml.cs`). Contains Views, ViewModels, Models, Converters, UserControls, Helpers, TriggerEngine.
- **VisualHFT.Commons** — Shared kernel: base models (`OrderBook`, `Trade`, `Order`), enums, interfaces, plugin base classes, object pools.
- **VisualHFT.Commons.WPF** — WPF-specific shared components.
- **demoTradingCore** — Demo trading simulation.

### Plugin System

Plugins live in `VisualHFT.Plugins/` and are loaded dynamically at runtime via reflection. Two categories:

- **Market Connectors** (9 plugins): Binance, Bitfinex, BitStamp, Coinbase, Gemini, Kraken, KuCoin, generic WebSocket, plus BaseDAL shared base. Each inherits `BasePluginDataRetriever`.
- **Studies** (5 plugins): VPIN, LOBImbalance, MarketResilience, OTT_Ratio. Each inherits `BasePluginStudy` or `BasePluginMultiStudy`.

Plugin interfaces are in `VisualHFT.Commons/PluginManager/` — key types: `IPlugin`, `BasePluginDataRetriever`, `BasePluginStudy`.

### Real-Time Data Flow

```
Market Connector Plugin
  → RaiseOnDataReceived()
  → HelperOrderBook / HelperTrade (central event bus)
  → Study Plugins (VPIN, LOBImbalance, etc.)
  → TriggerEngine (rules evaluation)
  → ViewModels → Views
```

`HelperOrderBook` and `HelperTrade` are the central event aggregators — plugins publish, studies and UI subscribe.

### Key Patterns

- **PropertyChanged.Fody** auto-weaves `INotifyPropertyChanged` (configured in `FodyWeavers.xml`)
- **Object Pooling** (`VisualHFT.Commons.Pools`) for Trade and OrderBook objects to reduce GC pressure
- **HelperCustomQueue\<T\>** for buffered high-frequency event processing
- **ConcurrentDictionary** throughout plugins for thread safety
- **Async reconnection** with exponential backoff and semaphore protection in connectors
- **OxyPlot** (custom SkiaSharp build, source in parent directory) for charting

### Startup Sequence (App.xaml.cs)

1. Initialize log4net
2. Load plugins via `PluginManager.LoadPlugins()`
3. Start `TriggerEngineService.StartBackgroundWorkerAsync()`
4. Periodic GC cleanup thread (35s intervals)

## Commit Message Conventions

Use conventional commit prefixes: `fix:`, `feat:`, `docs:`, `test:`, `build:`, `ci:`, `perf:`, `refactor:`, `style:`

## Key Dependencies

- **log4net** — Logging (rolling file to `logs/log.txt`)
- **Newtonsoft.Json** — JSON serialization
- **Prism.Core** — MVVM toolkit
- **MaterialDesignThemes** — WPF theming
- **Fody / PropertyChanged.Fody** — IL weaving for MVVM bindings
