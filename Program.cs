using BankBot;
using BankBot.Core;
using BankBot.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    enum ActionType { ConsoleOnly, NoGui, Gui, Error }
    static async Task Main(string[] args)
    {
        AppConfig.Initialize("cs-config.json");

        if (string.IsNullOrEmpty(AppConfig.Bot.Token))
        {
            Console.WriteLine("Critical error: TOKEN is not found in cs-config.json!");
        }

        var db = new DatabaseService();
        var money = new MoneyService(db);

        var initResult = await db.InitDbAsync();
        if (!initResult.IsSuccess)
        {
            Console.WriteLine($"DB init error: {initResult.ErrorMessage}");
            return;
        }

        bool noGuiMode = args.Contains("--nogui") || args.Contains("-ng");
        bool consoleOnlyMode = args.Contains("--consoleonly") || args.Contains("-c");

        ActionType mode = (consoleOnlyMode, noGuiMode) switch
        {
            (true, false) => ActionType.ConsoleOnly,
            (false, true) => ActionType.NoGui,
            (true, true) => ActionType.Error,
            _ => ActionType.Gui
        };

        switch (mode)
        {
            case ActionType.ConsoleOnly:
                {
                    var console = new ConsoleInterface(db, money);
                    await console.RunAsync();
                    break;
                }

            case ActionType.NoGui:
                {
                    var botService = new BankBotService(AppConfig.Bot.Token, AppConfig.Bot.Proxy);
                    using var cts = new CancellationTokenSource();
                    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
                    await botService.StartAsync(cts.Token);
                    try { await Task.Delay(-1, cts.Token); } catch (TaskCanceledException) { }
                    break;
                }

            case ActionType.Gui:
                {
                    var botService = new BankBotService(AppConfig.Bot.Token, AppConfig.Bot.Proxy);
                    using var cts = new CancellationTokenSource();
                    var console = new ConsoleInterface(db, money);
                    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };

                    Task botTask = botService.StartAsync(cts.Token);
                    await console.RunAsync();
                    cts.Cancel();

                    try { await botTask; } catch (Exception) { }
                    break;
                }


            case ActionType.Error:
                Console.WriteLine("Error, using invalid launch modifiers");
                break;
        }
    }
}