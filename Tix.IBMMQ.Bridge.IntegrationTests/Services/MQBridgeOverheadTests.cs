using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using IBM.WMQ;
using Shouldly;
using Tix.IBMMQ.Bridge.IntegrationTests.Helpers;
using Tix.IBMMQ.Bridge.Options;
using Xunit;
using Xunit.Abstractions;
using DotNet.Testcontainers.Configurations;
using Tix.IBMMQ.Bridge.Services;

namespace Tix.IBMMQ.Bridge.IntegrationTests.Services
{
    public class MQBridgeOverheadTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _logger;
        private IContainer _mqServer;
        private ConnectionOptions _connection;
        private const string MqImage = "ibmcom/mq:9.2.0.0-r1";
        private const int MaxMessageLength = 200;
        private const int OverheadSize = 128;

        public MQBridgeOverheadTests(ITestOutputHelper logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var mqscPath = Path.GetFullPath("queues-overhead.mqsc");

            _mqServer = new ContainerBuilder()
                .WithImage(MqImage)
                .WithEnvironment("LICENSE", "accept")
                .WithEnvironment("MQ_QMGR_NAME", "QM1")
                .WithEnvironment("MQ_APP_PASSWORD", "passw0rd")
                .WithExposedPort(1414)
                .WithPortBinding(1414, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1414))
                .WithBindMount(mqscPath, "/etc/mqm/99-queues.mqsc", AccessMode.ReadOnly)
                .Build();

            await _mqServer.StartAsync();

            _connection = new ConnectionOptions
            {
                QueueManagerName = "QM1",
                ConnectionName = $"localhost({_mqServer.GetMappedPublicPort(1414)})",
                UserId = "app",
                Password = "passw0rd"
            };
        }

        public async Task DisposeAsync()
        {
            await _mqServer.DisposeAsync();
        }

        [Fact]
        public void Should_have_128_bytes_overhead_on_mq_9_2()
        {
            // This should succeed
            PutMessage(new string('a', MaxMessageLength - OverheadSize));

            // This should fail
            var ex = Assert.Throws<MQException>(() => PutMessage(new string('a', MaxMessageLength - OverheadSize + 1)));
            ex.Reason.ShouldBe(MQC.MQRC_MSG_TOO_BIG_FOR_Q);
        }

        private void PutMessage(string message)
        {
            var props = BuildProperties(_connection, "DEV.APP.SVRCONN");
            using var qMgr = new MQQueueManager(_connection.QueueManagerName, props);
            using var queue = qMgr.AccessQueue("OVERHEAD.TEST", MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);
            var mqMessage = new MQMessage();
            mqMessage.WriteString(message);
            queue.Put(mqMessage);
        }

        private static Hashtable BuildProperties(ConnectionOptions opts, string channel)
        {
            var (host, port) = MQBridgeService.ParseConnectionName(opts.ConnectionName);
            return new Hashtable
            {
                { MQC.HOST_NAME_PROPERTY, host },
                { MQC.PORT_PROPERTY, port },
                { MQC.CHANNEL_PROPERTY, channel },
                { MQC.USER_ID_PROPERTY, opts.UserId },
                { MQC.PASSWORD_PROPERTY, opts.Password },
                { MQC.TRANSPORT_PROPERTY, MQC.TRANSPORT_MQSERIES_MANAGED }
            };
        }
    }
}
