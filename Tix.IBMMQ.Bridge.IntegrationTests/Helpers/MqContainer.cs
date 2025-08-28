﻿using System;
using System.IO;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Tix.IBMMQ.Bridge.Options;
using Xunit;

namespace Tix.IBMMQ.Bridge.IntegrationTests.Helpers
{
    public class MqContainer(ContainerImage image): IAsyncLifetime, IDisposable
    {
        const string MqAppUser = "app";
        const string MqAdminUser = "admin";
        const string Password = "passw0rd";
        const string MqManagerName = "QM1";
        const int MqPort = 1414;

        private IContainer _container;

        public MqContainer Build(string mqStartupScriptPath, bool exposeWebConsole)
        {
            var builder = new ContainerBuilder()
                .WithImage(image.Name)
                .WithEnvironment("LICENSE", "accept")
                .WithEnvironment("MQ_QMGR_NAME", MqManagerName)
                .WithEnvironment($"MQ_{MqAppUser}_PASSWORD", Password)
                .WithExposedPort(MqPort)
                .WithPortBinding(MqPort, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(1414));

            if (exposeWebConsole)
                builder = builder
                    .WithEnvironment($"MQ_{MqAdminUser}_PASSWORD", Password)
                    .WithExposedPort(9443)
                    .WithPortBinding(9443, true);

            if (mqStartupScriptPath != null)
                builder = File.Exists(mqStartupScriptPath) ?
                    builder.WithBindMount(mqStartupScriptPath, "/etc/mqm/startup-script.mqsc", AccessMode.ReadOnly) :
                    throw new FileNotFoundException(mqStartupScriptPath);
            
            _container = builder.Build();
            return this;
        }

        public async Task InitializeAsync()
        {
            await _container.StartAsync();

            Connection = new ConnectionOptions
            {
                QueueManagerName = MqManagerName,
                Host = "localhost",
                Port = _container.GetMappedPublicPort(MqPort),
                UserId = MqAppUser,
                Password = Password,
                UseTls = false
            };
        }

        public async Task DisposeAsync()
        {
            if (_container == null)
                return;

            await _container.DisposeAsync();
            _container = null;
        }

        public ConnectionOptions Connection {  get; private set; }

        private async Task ExecuteCommandInContainerAsync(string command)
        {
            var execResult = await _container.ExecAsync(new[] { "/bin/bash", "-c", command });
            if (execResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"Errore nell'esecuzione del comando: {command}. Codice di uscita: {execResult.ExitCode}");
            }
        }

        public async Task StartServerMq()
        {
            try
            {
                var startCommand = "strmqm QM1";
                await ExecuteCommandInContainerAsync(startCommand);
                Console.WriteLine("Server MQ avviato con successo.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'avvio del server MQ: {ex.Message}");
            }
        }

        public async Task StopServerMq()
        {
            try
            {
                var stopCommand = "endmqm QM1";
                await ExecuteCommandInContainerAsync(stopCommand);
                Console.WriteLine("Server MQ fermato con successo.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore durante l'arresto del server MQ: {ex.Message}");
            }
        }

        public void Dispose()
        {
            DisposeAsync().Wait();
        }
    }
}
