using Net.Leksi.ZkJson;
using org.apache.zookeeper;
using System.Text.Json;

namespace Net.Leksi.MicroService.Common;

public static class Extensions
{
    public static ConfigurationManager AddJsonFromZookeeper(this ConfigurationManager configurationManager, 
        ZooKeeper zooKeeper, 
        string path, 
        string basePropertyName = "$base"
    )
    {
        ZkJsonSerializer zkJson = new()
        {
            ZooKeeper = zooKeeper,
            Root = path,
        };
        JsonElement json = zkJson.IncrementalSerialize(basePropertyName);
        MemoryStream ms = new();
        JsonSerializer.Serialize(ms, json);
        ms.Position = 0;

        configurationManager.AddJsonStream(ms);

        return configurationManager;
    }
}
