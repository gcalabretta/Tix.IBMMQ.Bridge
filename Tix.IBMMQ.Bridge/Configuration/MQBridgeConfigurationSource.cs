using Microsoft.Extensions.Configuration;

namespace Tix.IBMMQ.Bridge.Configuration;

public class MQBridgeConfigurationSource : IConfigurationSource
{
    public string AppSettingsPath { get; set; }
    public string QueuePairsPath { get; set; }

    public MQBridgeConfigurationSource(string appSettingsPath, string queuePairsPath)
    {
        AppSettingsPath = appSettingsPath;
        QueuePairsPath = queuePairsPath;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new MQBridgeConfigurationProvider(AppSettingsPath, QueuePairsPath);
    }
}
