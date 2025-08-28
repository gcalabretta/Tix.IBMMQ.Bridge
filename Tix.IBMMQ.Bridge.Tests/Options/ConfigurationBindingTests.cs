using Microsoft.Extensions.Configuration;
using Shouldly;
using Tix.IBMMQ.Bridge.Options;
using Xunit;

namespace Tix.IBMMQ.Bridge.Tests.Options;

public class ConfigurationBindingTests
{
    [Fact]
    public void Should_bind_MQBridge_options()
    {
        var config = new ConfigurationBuilder()
            .Add(new Tix.IBMMQ.Bridge.Configuration.MQBridgeConfigurationSource(
                "../../../../Tix.IBMMQ.Bridge/appsettings.config",
                "../../../../Tix.IBMMQ.Bridge/queuepairs.config"
            ))
            .Build();

        var opts = config.Get<MQBridgeOptions>();

        Should.NotThrow(() => opts.Validate());
    }
}
