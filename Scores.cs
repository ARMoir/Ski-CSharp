using System.Text.Json;

namespace Ski;

public sealed record Score(string Name, long ElapsedTicks, DateTimeOffset Entered);

public static class Scores
{
    // Keep the ten fastest runs, including repeated usernames; older ties rank first.
    public static List<Score> Insert(IEnumerable<Score> existing, Score candidate)
    {
        var list = existing.Take(10).ToList();
        int index = list.FindIndex(s => s.ElapsedTicks > candidate.ElapsedTicks);
        if (index < 0) index = list.Count;
        list.Insert(index, candidate);
        return list.Take(10).ToList();
    }

    public static void Show(TimeSpan elapsed, int rate, TreeMode mode)
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaxSkiPort");
        string path = Path.Combine(directory, $"scores-{rate}hz.json");
        try
        {
            var scores = File.Exists(path)
                ? JsonSerializer.Deserialize<List<Score>>(File.ReadAllText(path)) ?? [] : [];
            if (mode != TreeMode.NoTrees)
            {
                string name = Environment.UserName;
                name = name[..Math.Min(12, name.Length)].PadRight(12);
                scores = Insert(scores, new(name, elapsed.Ticks, DateTimeOffset.Now));
                Directory.CreateDirectory(directory);
                // Replace only after a complete serialization/write; no VMS shared-record locking.
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(scores, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(temporary, path, overwrite: true);
            }
            Console.WriteLine("Rank    Name             Time        When");
            for (int i = 0; i < scores.Count; i++)
                Console.WriteLine($" {i + 1,2}      {scores[i].Name,-12}     {Program.FormatTime(TimeSpan.FromTicks(scores[i].ElapsedTicks))}    {scores[i].Entered.ToLocalTime():dd-MMM-yyyy}");
            Console.WriteLine($"\nScores: {path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.WriteLine($"Could not read or save scores: {ex.Message}");
        }
    }
}
