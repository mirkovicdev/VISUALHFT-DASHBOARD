using System.Text.Json;
using VisualHFT.Commons.Studies;
using VisualHFT.Model;
using VisualHFT.PluginManager;
using VisualHFT.WebServer.Models;

namespace VisualHFT.WebServer;

public class StudyBridge : IDisposable
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(StudyBridge));

    private readonly WebSocketBroadcaster _broadcaster;
    private readonly List<(IStudy study, EventHandler<BaseStudyModel> handler)> _subscriptions = new();

    public StudyBridge(WebSocketBroadcaster broadcaster, IEnumerable<IPlugin> plugins)
    {
        _broadcaster = broadcaster;
        int count = 0;

        foreach (var plugin in plugins)
        {
            if (plugin is IStudy study)
            {
                HookStudy(study, plugin);
                count++;
            }
            else if (plugin is IMultiStudy multiStudy)
            {
                foreach (var childStudy in multiStudy.Studies)
                {
                    // Find the matching IPlugin for the child study if available
                    var childPlugin = childStudy as IPlugin ?? plugin;
                    HookStudy(childStudy, childPlugin);
                    count++;
                }
            }
        }

        log.Info($"StudyBridge hooked {count} studies.");
    }

    private void HookStudy(IStudy study, IPlugin plugin)
    {
        EventHandler<BaseStudyModel> handler = (sender, model) =>
        {
            try
            {
                var dto = new StudyDto
                {
                    StudyName = plugin.Name,
                    Symbol = plugin.Settings?.Symbol ?? "",
                    Value = (double)model.Value,
                    Format = model.Format ?? "N2",
                    Timestamp = model.Timestamp.ToString("o"),
                    ValueColor = model.ValueColor,
                    MarketMidPrice = (double)model.MarketMidPrice,
                    HasData = model.HasData,
                    HasError = model.HasError,
                    IsStale = model.IsStale
                };

                var msg = new WsMessage<StudyDto> { Type = "study", Data = dto };
                var json = JsonSerializer.Serialize(msg);
                _ = _broadcaster.BroadcastAsync(json);
            }
            catch (Exception ex)
            {
                log.Error($"Error broadcasting study {plugin.Name}", ex);
            }
        };

        study.OnCalculated += handler;
        _subscriptions.Add((study, handler));
    }

    public void Dispose()
    {
        foreach (var (study, handler) in _subscriptions)
        {
            try { study.OnCalculated -= handler; }
            catch { /* already disposed */ }
        }
        _subscriptions.Clear();
    }
}
