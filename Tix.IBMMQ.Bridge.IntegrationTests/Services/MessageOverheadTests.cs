using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using IBM.WMQ;
using Shouldly;
using Tix.IBMMQ.Bridge.Options;
using Xunit;
using Xunit.Abstractions;

namespace Tix.IBMMQ.Bridge.IntegrationTests.Services;

public class MessageOverheadTests : IAsyncLifetime
{
    private const int MqMessageOverhead = 128;
    private const int MaxMessageLength = 200;

    private readonly ITestOutputHelper _logger;
    private readonly IContainer _mqServer;

    static MessageOverheadTests()
    {
        TestcontainersSettings.ResourceReaperEnabled = false;
    }

    public MessageOverheadTests(ITestOutputHelper logger)
    {
        _logger = logger;
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };
        var image = $"ibm-mqadvanced-server-dev:9.4.0.0-{arch}";

        if (!ImageExists(image))
        {
            var buildScript = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "./build-arm-mq-image.sh",
                _ => "./build-amd-mq-image.sh"
            };
            RunScript(buildScript);
        }

        var mqscPath = Path.GetFullPath("Tix.IBMMQ.Bridge.IntegrationTests/message-overhead-test.mqsc");

        _mqServer = new ContainerBuilder()
            .WithImage(image)
            .WithEnvironment("LICENSE", "accept")
            .WithEnvironment("MQ_QMGR_NAME", "QM1")
            .WithEnvironment("MQ_APP_PASSWORD", "passw0rd")
            .WithEnvironment("MQ_ADMIN_PASSWORD", "passw0rd")
            .WithExposedPort(1414)
            .WithPortBinding(1414, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1414))
            .WithBindMount(mqscPath, "/etc/mqm/99-test.mqsc", AccessMode.ReadOnly)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _mqServer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _mqServer.DisposeAsync();
    }

    [Fact]
    public void Should_fail_when_message_is_too_long()
    {
        var serverPort = _mqServer.GetMappedPublicPort(1414);
        const string channel = "DEV.APP.SVRCONN";
        var conn = new ConnectionOptions
        {
            QueueManagerName = "QM1",
            ConnectionName = $"localhost({serverPort})",
            UserId = "app",
            Password = "passw0rd"
        };

        var props = BuildProperties(conn, channel);
        using var qMgr = new MQQueueManager(conn.QueueManagerName, props);
        using var queue = qMgr.AccessQueue("MSG.OVERHEAD.TEST", MQC.MQOO_OUTPUT | MQC.MQOO_FAIL_IF_QUIESCING);

        var messageSize = MaxMessageLength - MqMessageOverhead;
        var message = new string('a', messageSize);

        var mqMessage = new MQMessage();
        mqMessage.WriteString(message);

        // This should succeed
        queue.Put(mqMessage);

        var failingMessageSize = MaxMessageLength - MqMessageOverhead + 1;
        var failingMessage = new string('a', failingMessageSize);

        var failingMqMessage = new MQMessage();
        failingMqMessage.WriteString(failingMessage);

        // This should fail
        var ex = Assert.Throws<MQException>(() => queue.Put(failingMqMessage));
        ex.Reason.ShouldBe(MQC.MQRC_MSG_TOO_BIG_FOR_Q);
    }

    private static Hashtable BuildProperties(ConnectionOptions opts, string channel)
    {
        var (host, port) = ParseConnectionName(opts.ConnectionName);
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

    private static (string Host, int Port) ParseConnectionName(string connectionName)
    {
        var parts = connectionName.Split(new[] { '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], int.Parse(parts[1]));
    }

    private static bool ImageExists(string image)
    {
        var psi = new ProcessStartInfo("docker", $"image inspect {image}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }

    private static void RunScript(string script)
    {
        var psi = new ProcessStartInfo(script)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi);
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"Script {script} failed.");
        }
    }
}
