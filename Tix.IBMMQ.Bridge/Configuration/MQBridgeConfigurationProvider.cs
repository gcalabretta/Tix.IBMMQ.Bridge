using Microsoft.Extensions.Configuration;
using System.IO;
using System;
using System.Linq;

namespace Tix.IBMMQ.Bridge.Configuration;

public class MQBridgeConfigurationProvider : ConfigurationProvider
{
    private readonly string _appSettingsPath;
    private readonly string _queuePairsPath;

    public MQBridgeConfigurationProvider(string appSettingsPath, string queuePairsPath)
    {
        _appSettingsPath = appSettingsPath;
        _queuePairsPath = queuePairsPath;
    }

    const string OldConn = "old";
    const string NewConn = "new";

    public override void Load()
    {
        ValidateConfigFiles();

        var appSettings = ReadLines(_appSettingsPath);
        SetConnection(OldConn, appSettings, out string oldChannel);
        SetConnection(NewConn, appSettings, out string newChannel);

        var queuePairs = ReadLines(_queuePairsPath);
        SetQueuePairs(queuePairs, oldChannel, newChannel);
    }

    private void SetConnection(string connectionKey, string[] lines, out string channel)
    {
        channel = null;
        var prefix = $"ibmmq:{connectionKey}:";
        foreach (var line in lines.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var parts = line.Replace(prefix, "").Split(['=', ' '], StringSplitOptions.RemoveEmptyEntries);
            Data[$"Connections:{connectionKey}:{parts[0]}"] = parts[1];
            if (parts[0].Equals("channel", StringComparison.OrdinalIgnoreCase))
            {
                channel = parts[1];
            }
        }

        if (channel == null)
        {
            throw new InvalidDataException($"Channel not found for connection: {connectionKey}");
        }
    }

    private void SetQueuePairs(string[] lines, string oldChannel, string newChannel)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            bool oldToNew = parts[1][0] switch
            {
                '>' => true,
                '<' => false,
                _ => throw new InvalidDataException($"Invalid direction in queue pair: {lines[i]}")
            };

            if (parts[0] == ".") parts[0] = parts[2]; // Inbound queue name = Outbound queue name
            if (parts[2] == ".") parts[2] = parts[0]; // Outbound queue name = Inbound queue name

            Data[$"QueuePairs:{i}:InboundConnection"] = oldToNew ? OldConn : NewConn;
            Data[$"QueuePairs:{i}:InboundChannel"] = oldToNew ? oldChannel : newChannel;
            Data[$"QueuePairs:{i}:InboundQueue"] = oldToNew ? parts[0] : parts[2];
            Data[$"QueuePairs:{i}:OutboundConnection"] = oldToNew ? NewConn : OldConn;
            Data[$"QueuePairs:{i}:OutboundChannel"] = oldToNew ? newChannel : oldChannel;
            Data[$"QueuePairs:{i}:OutboundQueue"] = oldToNew ? parts[2] : parts[0];
        }
    }

    string[] ReadLines(string filePath)
    {
        return File.ReadAllLines(filePath)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            // Remove comments
            .Where(x => !x.StartsWith('#') && !x.StartsWith(';') && !x.StartsWith("//"))
            .ToArray();
    }

    private void ValidateConfigFiles()
    {
        if (!File.Exists(_appSettingsPath))
        {
            throw new FileNotFoundException($"App settings file not found: {_appSettingsPath}");
        }

        if (!File.Exists(_queuePairsPath))
        {
            throw new FileNotFoundException($"Queue pairs file not found: {_queuePairsPath}");
        }
    }
}
