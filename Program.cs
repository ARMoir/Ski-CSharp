using System.Diagnostics;
using System.Globalization;

namespace Ski;

public static class Program
{
    public static int Main(string[] args)
    {
        int rate = 60;
        int? seed = null;
        var mode = TreeMode.Normal;
        bool selfTest = false;
        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--no-trees" or ">": mode = TreeMode.NoTrees; break;
                    case "--uniform" or ".": mode = TreeMode.Uniform; break;
                    case "--tick-rate": rate = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--seed": seed = int.Parse(args[++i], CultureInfo.InvariantCulture); break;
                    case "--self-test": selfTest = true; break;
                    case "--help" or "-h":
                        Console.WriteLine("Ski [--no-trees|--uniform] [--tick-rate 1..1000] [--seed integer] [--self-test]\n4/6 turn; Enter starts; Ctrl+Y quits (Escape also works). Use an 80 x 24 or larger terminal.");
                        return 0;
                    default: throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }
            if (rate is < 1 or > 1000) throw new ArgumentException("Tick rate must be 1..1000.");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            Console.Error.WriteLine($"{ex.Message}\nUse --help for options.");
            return 2;
        }
        if (selfTest) return SelfTests.Run();
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Error.WriteLine("Play in an interactive terminal, or use --self-test.");
            return 2;
        }
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        _ = random.NextDouble(); // Original discards one random value after seeding.
        var game = new Game(mode, n => random.Next(1, n + 1));
        var screen = new Screen();
        bool oldControlC = Console.TreatControlCAsInput;
        TimeSpan? finishTime = null;
        string? terminalError = null;
        try
        {
            Console.TreatControlCAsInput = true;
            Console.CursorVisible = false;
            Console.Clear();
            screen.Put(17, 2, "Proverbial Software Presents");
            screen.Put(18, 3, "Alpine Skiing and Tree Dodging v2.0");
            screen.Put(19, 4, "--a new sport for VAX/VMS users--");
            screen.Put(20, 10, "by Chris Pirih");
            screen.Put(5, 2, "Use 4 & 6 keys to turn skis.");
            screen.Put(6, 2, "Hit ENTER to start.");
            screen.Draw();
            ConsoleKeyInfo key;
            do { key = Console.ReadKey(true); if (Quit(key)) return 0; }
            while (key.Key != ConsoleKey.Enter);
            var watch = Stopwatch.StartNew();
            double nextTick = watch.Elapsed.TotalSeconds;
            int previousX = 40;
            bool waitForKey = false;
            while (!game.Finished)
            {
                ConsoleKeyInfo? input = waitForKey ? Console.ReadKey(true) : Console.KeyAvailable ? Console.ReadKey(true) : null;
                if (input.HasValue && Quit(input.Value)) return 0;
                if (waitForKey) nextTick = watch.Elapsed.TotalSeconds;
                int turn = input switch
                {
                    { KeyChar: '4' or 'a' or 'A' } or { Key: ConsoleKey.LeftArrow } => -1,
                    { KeyChar: '6' or 'd' or 'D' } or { Key: ConsoleKey.RightArrow } => 1,
                    _ => 0
                };
                var frame = game.Step(turn);
                TimeSpan sampleTime = watch.Elapsed;
                // Erase before scrolling, matching the terminal command ordering.
                screen.Put(3, previousX, "  ");
                if (frame.Scrolled)
                {
                    screen.Scroll();
                    screen.Put(20, frame.NewTree, "^");
                    if (frame.Marker) screen.Put(20, 1, game.Meters.ToString().PadLeft(4));
                }
                if (frame.Split) screen.Status = FormatTime(sampleTime);
                screen.Put(3, (int)game.X, new string(game.Shape, 2));
                screen.Draw();
                previousX = (int)game.X;
                if (game.Finished) { finishTime = sampleTime; break; }
                if (frame.Collision)
                {
                    screen.Put(3, previousX, "  ");
                    for (int j = 0; j <= 2; j++)
                    {
                        screen.Put(3, previousX - j - 1, Game.Shapes[2 - j].ToString());
                        screen.Put(3, previousX + j + 1, Game.Shapes[2 + j].ToString());
                        screen.Draw();
                        Thread.Sleep(50);
                        screen.Put(3, previousX - j - 1, " ");
                        screen.Put(3, previousX + j + 1, " ");
                    }
                    screen.Draw();
                    Thread.Sleep((int)(game.YVelocity * 3f) * 1000 + 200);
                    game.RecoverFromCrash();
                    while (Console.KeyAvailable) if (Quit(Console.ReadKey(true))) return 0;
                    nextTick = watch.Elapsed.TotalSeconds;
                    waitForKey = false; // Crash restarts at label 100, even with zero velocity.
                    continue;
                }
                waitForKey = game.Stopped;
                nextTick += 1.0 / rate;
                double remaining = nextTick - watch.Elapsed.TotalSeconds;
                if (remaining > 0) Thread.Sleep(TimeSpan.FromSeconds(remaining));
                else nextTick = watch.Elapsed.TotalSeconds; // No burst of catch-up movement.
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
        {
            terminalError = $"Terminal error: {ex.Message}";
        }
        finally
        {
            Console.TreatControlCAsInput = oldControlC;
            Console.CursorVisible = true;
            Console.Clear();
        }
        if (terminalError is not null)
        {
            Console.Error.WriteLine(terminalError);
            return 1;
        }
        if (finishTime is { } elapsed)
        {
            int mph = (int)((Game.RaceLength / 1609f) / (float)elapsed.TotalHours);
            Console.WriteLine($"Finished in {FormatTime(elapsed)}.\nYour average speed was {mph} mile{(mph == 1 ? "" : "s")} per hour.\nYou crashed {game.Crashes} time{(game.Crashes == 1 ? "" : "s")}.\n");
            Scores.Show(elapsed, rate, mode);
        }
        return 0;
    }

    private static bool Quit(ConsoleKeyInfo key) => key.Key == ConsoleKey.Escape || key.KeyChar is '\u0019' or '\u0003';
    public static string FormatTime(TimeSpan time) => $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
}

internal sealed class Screen
{
    private readonly char[,] cells = new char[20, 80];
    private readonly string[] previous = new string[20];
    private string? oldStatus;
    public string Status { get; set; } = "";
    public Screen()
    {
        for (int row = 0; row < 20; row++)
            for (int col = 0; col < 80; col++) cells[row, col] = ' ';
    }
    public void Put(int row, int column, string text)
    {
        for (int i = 0; i < text.Length; i++)
            if (row is >= 1 and <= 20 && column + i is >= 1 and <= 80)
                cells[row - 1, column + i - 1] = text[i];
    }
    public void Scroll()
    {
        for (int row = 0; row < 19; row++)
            for (int col = 0; col < 80; col++) cells[row, col] = cells[row + 1, col];
        for (int col = 0; col < 80; col++) cells[19, col] = ' ';
    }
    public void Draw()
    {
        if (Console.WindowWidth < 80 || Console.WindowHeight < 24)
            throw new IOException("Enlarge the terminal to at least 80 columns and 24 rows.");
        for (int row = 0; row < 20; row++)
        {
            var chars = new char[80];
            for (int col = 0; col < 80; col++) chars[col] = cells[row, col];
            string line = new(chars);
            if (line == previous[row]) continue;
            Console.SetCursorPosition(0, row);
            Console.Write(line);
            previous[row] = line;
        }
        if (Status == oldStatus) return;
        Console.SetCursorPosition(0, 21);
        Console.Write(Status);
        oldStatus = Status;
    }
}
