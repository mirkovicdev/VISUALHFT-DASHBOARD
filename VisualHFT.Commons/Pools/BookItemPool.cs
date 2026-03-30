using VisualHFT.Model;
using System.Runtime.CompilerServices;

namespace VisualHFT.Commons.Pools
{
    /// <summary>
    /// Common interface for pool metrics to ensure compatibility between basic and extended metrics.
    /// </summary>
    public interface IPoolMetrics
    {
        int Available { get; }
        double CurrentUtilization { get; }
        double PeakUtilization { get; }
        long TotalGets { get; }
        long TotalReturns { get; }
        long Outstanding { get; }
        int PoolSize { get; }
        bool IsHealthy { get; }
        bool IsCritical { get; }
        bool HasLeaks { get; }

        // Extended properties with default values for basic implementations
        long ExtendedItemsReturned => 0;
        bool IsInitialized => true; // Basic pool is always "initialized"
        bool HasExtendedCleanup => ExtendedItemsReturned > 0;

        // Backward compatibility properties (mapped to extended properties)
        long L3OrdersReturned => ExtendedItemsReturned;
        bool HasCascadeCleanup => HasExtendedCleanup;
    }

    /// <summary>
    /// Extended interface for combined pool metrics.
    /// </summary>
    public interface ICombinedPoolMetrics : IPoolMetrics
    {
        int BasicPoolSize { get; }
        int ExtendedPoolSize { get; }
        long ExtendedItemsReturned { get; }
        bool IsInitialized { get; }
        bool HasExtendedCleanup { get; }
    }

    /// <summary>
    /// BOOKITEM POOL - ADVANCED OBJECT POOLING IMPLEMENTATION
    /// =======================================================
    /// 
    /// PURPOSE:
    /// --------
    /// This class provides high-performance object pooling for BookItem instances,
    /// optimized for high-frequency trading and market data processing scenarios.
    /// It supports both basic BookItem operations and can be extended for more
    /// complex functionality via partial class extensions.
    /// 
    /// DESIGN PRINCIPLES:
    /// ------------------
    /// • High Performance: Optimized for minimal allocation overhead
    /// • Thread Safety: Full concurrent access support
    /// • Extensibility: Delegate hooks for advanced functionality
    /// • Memory Efficiency: Prevents object allocation pressure
    /// • Monitoring: Comprehensive metrics and health reporting
    /// 
    /// USAGE PATTERNS:
    /// ---------------
    /// 
    /// Basic Usage:
    /// ```csharp
    /// var item = BookItemPool.Get();           // Get from pool
    /// item.Price = 100.50;
    /// item.Size = 1000;
    /// BookItemPool.Return(item);               // Return to pool
    /// ```
    /// 
    /// Advanced Usage (with extensions):
    /// ```csharp
    /// // Extensions can provide additional functionality
    /// var advancedItem = BookItemPool.GetAdvanced();  // If extension available
    /// // ... complex operations ...
    /// BookItemPool.Return(advancedItem);              // Smart routing
    /// ```
    /// 
    /// EXTENSIBILITY:
    /// --------------
    /// This class can be extended via partial classes to add advanced functionality
    /// without modifying the core implementation. Extensions can plug in via delegates
    /// to provide custom routing and processing logic.
    /// 
    /// THREAD SAFETY:
    /// ---------------
    /// All operations are thread-safe using atomic operations and lock-free patterns.
    /// Multiple threads can safely call Get/Return operations concurrently without
    /// coordination or external synchronization.
    /// 
    /// PERFORMANCE CHARACTERISTICS:
    /// ----------------------------
    /// • Pool capacity: 800,000 objects
    /// • Throughput: >200K operations/second
    /// • Memory footprint: ~160MB at full capacity
    /// • Reset time: ~1µs per object
    /// • Cache efficiency: High due to object reuse
    /// </summary>
    public static partial class BookItemPool
    {
        // Delegate for smart routing - can be set by extensions if available
        private static Action<BookItem> _smartReturnHandler = null;

        // Delegate for extended metrics - can be set by extensions if available
        private static Func<IPoolMetrics> _extendedMetricsHandler = null;

        // Delegate for extended reset - can be set by extensions if available
        private static Action _extendedResetHandler = null;

        /// <summary>
        /// Internal method to set the smart return handler (used by extensions)
        /// </summary>
        internal static void SetSmartReturnHandler(Action<BookItem> handler)
        {
            _smartReturnHandler = handler;
        }

        /// <summary>
        /// Internal method to set the extended metrics handler (used by extensions)
        /// </summary>
        internal static void SetExtendedMetricsHandler(Func<IPoolMetrics> handler)
        {
            _extendedMetricsHandler = handler;
        }

        /// <summary>
        /// Internal method to set the extended reset handler (used by extensions)
        /// </summary>
        internal static void SetExtendedResetHandler(Action handler)
        {
            _extendedResetHandler = handler;
        }

        /// <summary>
        /// Thread-safe: Gets a BookItem from the pool.
        /// This is the primary method for object pooling operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BookItem Get()
        {
            // Basic pool access - always available
            return BookItemL2Pool.Get();
        }

        /// <summary>
        /// Thread-safe: Returns a BookItem to the appropriate pool.
        /// Uses smart routing if extensions are available, otherwise routes to basic pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return(BookItem item)
        {
            if (item == null) return;

            // Use smart routing if available (extensions), otherwise basic routing
            if (_smartReturnHandler != null)
            {
                _smartReturnHandler(item);
            }
            else
            {
                // Basic pool routing - always available
                // Note: Extensions may override this with more sophisticated logic
                BookItemL2Pool.Return(item);
            }
        }

        /// <summary>
        /// Thread-safe: Gets the number of available objects in the pool.
        /// </summary>
        public static int AvailableObjects => BookItemL2Pool.AvailableObjects;

        /// <summary>
        /// Thread-safe: Gets the pool utilization percentage (0.0 to 1.0).
        /// </summary>
        public static double CurrentUtilization => BookItemL2Pool.CurrentUtilization;

        /// <summary>
        /// Thread-safe: Gets the total number of Get() operations performed.
        /// </summary>
        public static long TotalGets => BookItemL2Pool.TotalGets;

        /// <summary>
        /// Thread-safe: Gets the total number of Return() operations performed.
        /// </summary>
        public static long TotalReturns => BookItemL2Pool.TotalReturns;

        /// <summary>
        /// Thread-safe: Gets the peak utilization percentage reached.
        /// </summary>
        public static double PeakUtilization => BookItemL2Pool.PeakUtilization;

        /// <summary>
        /// Thread-safe: Gets information about current pool status.
        /// </summary>
        public static string GetPoolStatus()
        {
            return BookItemL2Pool.GetPoolStatus().Replace("BookItemL2Pool", "BookItemPool");
        }

        /// <summary>
        /// Thread-safe: Provides a health report for pool monitoring.
        /// </summary>
        public static string GetPoolHealthReport()
        {
            return BookItemL2Pool.GetPoolStatus().Replace("BookItemL2Pool", "BookItemPool") +
                   "\n  Architecture: High-Performance Object Pool";
        }

        /// <summary>
        /// Thread-safe: Resets pool statistics (for testing/debugging).
        /// </summary>
        public static void ResetStatistics()
        {
            BookItemL2Pool.ResetStatistics();

            // Extensions can handle their own reset via additional logic
            // This maintains basic pool compatibility
            // If extension is available, reset extension statistics too
            _extendedResetHandler?.Invoke();
        }

        /// <summary>
        /// Thread-safe: Gets pool metrics for performance monitoring.
        /// Returns extended metrics if extensions are available, otherwise basic metrics.
        /// </summary>
        public static IPoolMetrics GetMetrics()
        {
            // Use extended metrics if available (extensions), otherwise basic metrics
            if (_extendedMetricsHandler != null)
            {
                return _extendedMetricsHandler();
            }
            else
            {
                return BookItemL2Pool.GetMetrics();
            }
        }
    }
}
