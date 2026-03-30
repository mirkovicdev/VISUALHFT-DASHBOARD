using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VisualHFT.DataRetriever.TestingFramework.Core
{
    /// <summary>
    /// Utility for collecting and formatting test results and error reports
    /// </summary>
    public static class TestResultFormatter
    {
        /// <summary>
        /// Formats error reports into a comprehensive text report
        /// </summary>
        public static string FormatErrorReport(List<ErrorReporting> errors, string testName = "Test")
        {
            if (!errors.Any()) return $"{testName} completed successfully with no errors or warnings.";

            var report = new StringBuilder();
            report.AppendLine();
            report.AppendLine($"=== {testName} Results ===");
            report.AppendLine();

            var errorCount = errors.Count(e => e.MessageType == ErrorMessageTypes.ERROR);
            var warningCount = errors.Count(e => e.MessageType == ErrorMessageTypes.WARNING);

            report.AppendLine($"Summary: {errorCount} errors, {warningCount} warnings");
            report.AppendLine();

            if (errors.Any())
            {
                var groupedErrors = errors.GroupBy(e => e.PluginName).OrderBy(g => g.Key);

                foreach (var pluginGroup in groupedErrors)
                {
                    report.AppendLine($"Plugin: {pluginGroup.Key}");
                    
                    var pluginErrors = pluginGroup.Where(e => e.MessageType == ErrorMessageTypes.ERROR);
                    var pluginWarnings = pluginGroup.Where(e => e.MessageType == ErrorMessageTypes.WARNING);

                    foreach (var error in pluginErrors)
                    {
                        report.AppendLine($"  ? ERROR: {error.Message}");
                    }

                    foreach (var warning in pluginWarnings)
                    {
                        report.AppendLine($"  ??  WARNING: {warning.Message}");
                    }

                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Creates a summary of test execution results
        /// </summary>
        public static string CreateTestSummary(
            string testName, 
            int totalPlugins, 
            int successfulPlugins, 
            List<ErrorReporting> errors, 
            TimeSpan duration)
        {
            var summary = new StringBuilder();
            summary.AppendLine($"=== {testName} Summary ===");
            summary.AppendLine($"Duration: {duration:mm\\:ss\\.fff}");
            summary.AppendLine($"Plugins tested: {totalPlugins}");
            summary.AppendLine($"Successful: {successfulPlugins}");
            summary.AppendLine($"Failed: {totalPlugins - successfulPlugins}");
            summary.AppendLine($"Errors: {errors.Count(e => e.MessageType == ErrorMessageTypes.ERROR)}");
            summary.AppendLine($"Warnings: {errors.Count(e => e.MessageType == ErrorMessageTypes.WARNING)}");
            
            if (totalPlugins > 0)
            {
                var successRate = (double)successfulPlugins / totalPlugins * 100;
                summary.AppendLine($"Success rate: {successRate:F1}%");
            }

            return summary.ToString();
        }

        /// <summary>
        /// Creates a detailed plugin status report
        /// </summary>
        public static string CreatePluginStatusReport(List<PluginTestSnapshot> snapshots)
        {
            if (!snapshots.Any()) return "No plugin snapshots available.";

            var report = new StringBuilder();
            report.AppendLine("Plugin Status Report:");
            report.AppendLine();

            foreach (var snapshot in snapshots.OrderBy(s => s.PluginName))
            {
                report.AppendLine($"  {snapshot.PluginName}:");
                report.AppendLine($"    Status: {snapshot.Status}");
                report.AppendLine($"    Has Data: {(snapshot.HasOrderBook ? "Yes" : "No")}");
                
                if (snapshot.LastOrderBookTimestamp.HasValue)
                {
                    report.AppendLine($"    Last Data: Sequence {snapshot.LastOrderBookTimestamp}");
                }

                if (snapshot.ExceptionCount > 0)
                {
                    report.AppendLine($"    Exceptions: {snapshot.ExceptionCount}");
                    if (!string.IsNullOrEmpty(snapshot.LastException))
                    {
                        report.AppendLine($"    Last Error: {snapshot.LastException}");
                    }
                }

                report.AppendLine();
            }

            return report.ToString();
        }
    }
}