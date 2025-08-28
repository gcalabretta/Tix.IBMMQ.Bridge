using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Tix.IBMMQ.Bridge.E2ETests.Helpers;
using Tix.IBMMQ.Bridge.Options;
using Xunit;

namespace Tix.IBMMQ.Bridge.E2ETests
{
    public class ReliabilityE2ETestsFixtureNoDocker : IAsyncLifetime
    {
        private MqBridgeContainer _bridgeContainer;
        private MQBridgeOptions _mqBridgeOptions;

        public ConnectionOptions ConnIn => _mqBridgeOptions.Connections["In"];
        public ConnectionOptions ConnOut => _mqBridgeOptions.Connections["Out"];
        public IList<QueuePairOptions> QueuePairs => _mqBridgeOptions.QueuePairs;

        public async Task InitializeAsync()
        {
            throw new System.NotImplementedException("manage new appsettings.actual.config file , no json anymore");


            // _bridge = new MqBridgeHost(logger, opt);
            // await _bridge.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_bridgeContainer != null)
            {
                await _bridgeContainer.DisposeAsync();
            }
        }
    }
}
