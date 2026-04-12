using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;

namespace BankBot;

public class AppConfig
{
    private static IConfiguration? _configuration;

    public static BotSettings Bot { get; private set; } = new();
    public static HashSet<string> TrashList { get; private set; } = new();

    public static void Initialize(string configPath = "cs-config.json")
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath, optional: false, reloadOnChange: true);

        _configuration = builder.Build();

        // Привязываем секцию "Bot"
        Bot = _configuration.GetSection("Bot").Get<BotSettings>() ?? new BotSettings();

        // Загружаем TrashList
        var trashArray = _configuration.GetSection("TrashList").Get<string[]>();
        TrashList = trashArray != null ? new HashSet<string>(trashArray) : new HashSet<string>();
    }
}

public class BotSettings
{
    public string Token { get; set; } = string.Empty;
    public string? Proxy { get; set; }
    public long[] AdminIds { get; set; } = System.Array.Empty<long>();
}