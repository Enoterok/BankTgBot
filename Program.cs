using System;
using System.Threading;
using System.Threading.Tasks;
using BankBot;

class Program
{
    static async Task Main(string[] args)
    {
        // Загружаем конфигурацию
        AppConfig.Initialize("cs-config.json");

        if (string.IsNullOrEmpty(AppConfig.Bot.Token))
        {
            Console.WriteLine("Critical error: TOKEN is not found in cs-config.json!");
            return;
        }

        var botService = new BankBotService(AppConfig.Bot.Token, AppConfig.Bot.Proxy);
        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await botService.StartAsync(cts.Token);

        // Бесконечное ожидание
        try
        {
            await Task.Delay(-1, cts.Token);
        }
        catch (TaskCanceledException) { }
        finally
        {
            Console.WriteLine("Бот остановлен.");
        }
    }
}