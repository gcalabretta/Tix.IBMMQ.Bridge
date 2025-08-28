using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System;

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

    public override void Load()
    {
        // Parse appsettings.config as key-value pairs (ibmmq:old:host=...)
        var channelMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(_appSettingsPath))
        {
            foreach (var line in File.ReadAllLines(_appSettingsPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                var idx = trimmed.IndexOf('=');
                if (idx < 0) continue;
                var key = trimmed.Substring(0, idx).Trim();
                var value = trimmed.Substring(idx + 1).Trim();
                // Example: ibmmq:old:host=host1
                var parts = key.Split(':');
                if (parts.Length == 3 && parts[0] == "ibmmq")
                {
                    var connKey = parts[1];
                    var prop = parts[2];
                    Data[$"MQBridge:Connections:{connKey}:{FirstCharToUpper(prop)}"] = value;
                    if (prop.Equals("channel", StringComparison.OrdinalIgnoreCase))
                        channelMap[connKey] = value;
                }
            }
        }

        // Helper to upper-case first char for property binding
        static string FirstCharToUpper(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        // Parse queuepairs.config (3 colonne: queueOld direction queueNew)
        if (File.Exists(_queuePairsPath))
        {
            var queuePairs = new List<Dictionary<string, string>>();
            foreach (var line in File.ReadAllLines(_queuePairsPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3)
                    continue; // skip invalid
                var queue1 = parts[0];
                var direction = parts[1];
                var queue2 = parts[2];
                string inboundConn, outboundConn;
                string inboundQueue, outboundQueue;
                if (direction == ">")
                {
                    inboundConn = "old";
                    inboundQueue = queue1;
                    outboundConn = "new";
                    outboundQueue = queue2;
                }
                else if (direction == "<")
                {
                    inboundConn = "new";
                    inboundQueue = queue2;
                    outboundConn = "old";
                    outboundQueue = queue1;
                }
                else
                {
                    continue; // skip invalid direction
                }
                var pair = new Dictionary<string, string>
                {
                    ["InboundConnection"] = inboundConn,
                    ["InboundQueue"] = inboundQueue,
                    ["OutboundConnection"] = outboundConn,
                    ["OutboundQueue"] = outboundQueue,
                    ["InboundChannel"] = channelMap.TryGetValue(inboundConn, out var inCh) ? inCh : "",
                    ["OutboundChannel"] = channelMap.TryGetValue(outboundConn, out var outCh) ? outCh : ""
                };
                queuePairs.Add(pair);
            }
            for (int i = 0; i < queuePairs.Count; i++)
            {
                foreach (var kv in queuePairs[i])
                {
                    Data[$"MQBridge:QueuePairs:{i}:{kv.Key}"] = kv.Value;
                }
            }
        }
    }
}
