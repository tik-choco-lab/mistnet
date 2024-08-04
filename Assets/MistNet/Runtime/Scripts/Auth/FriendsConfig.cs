using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

[Serializable]
public class FriendsConfig
{
    public List<Friend> Friends;
}

[Serializable]
public class Friend
{
    public string Name;
    public string PublicKey;
}

public class FriendsConfigLoader
{
    private static readonly string ConfigFilePath = $"{Application.dataPath}/../friends.yaml";
    public FriendsConfig Config;

    public void LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            CreateDefaultConfig();
        }

        var yaml = File.ReadAllText(ConfigFilePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        Config = deserializer.Deserialize<FriendsConfig>(yaml);
    }

    public void Save()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(Config);
        File.WriteAllText(ConfigFilePath, yaml);
    }

    private void CreateDefaultConfig()
    {
        var config = new FriendsConfig
        {
            Friends = new List<Friend>()
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(config);
        File.WriteAllText(ConfigFilePath, yaml);
    }
}
