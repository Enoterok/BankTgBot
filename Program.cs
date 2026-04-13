using BankBot;
using BankBot.Core;
using BankBot.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        AppConfig.Initialize("cs-config.json");

        if (string.IsNullOrEmpty(AppConfig.Bot.Token))
        {
            Console.WriteLine("Critical error: TOKEN is not found in cs-config.json!");
            return;
        }

        var db = new DatabaseService();
        var money = new MoneyService(db);

        // Инициализация БД
        var initResult = await db.InitDbAsync();
        if (!initResult.IsSuccess)
        {
            Console.WriteLine($"DB init error: {initResult.ErrorMessage}");
            return;
        }

        bool consoleMode = args.Contains("--nogui") || args.Contains("-ng");

        if (!consoleMode)
        {
            var console = new ConsoleInterface(db, money);
            await console.RunAsync();
        }
        else
        {
            var botService = new BankBotService(AppConfig.Bot.Token, AppConfig.Bot.Proxy);
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            await botService.StartAsync(cts.Token);
            // Ожидаем завершения
            try { await Task.Delay(-1, cts.Token); } catch (TaskCanceledException) { }
        }
    }
}