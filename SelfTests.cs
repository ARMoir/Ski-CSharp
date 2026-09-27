namespace Ski;

internal static class SelfTests
{
    public static int Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks++;
        }
        try
        {
            Check(Game.Approach(.99f, 1f, .1f) == 1f, "Positive overshoot");
            Check(Game.Approach(-.29f, -.3f, .1f) == -.3f, "Negative overshoot");
            Check(Game.Approach(0f, 0f, .1f) == 0f, "Sign of zero");
            var straight = new Game(TreeMode.NoTrees, _ => throw new Exception("Unexpected random call"));
            int steps = 0, markers = 0, splits = 0;
            bool wrapped = false;
            while (!straight.Finished && steps++ < 10000)
            {
                float before = straight.TreeOffset;
                var f = straight.Step();
                if (f.Marker) markers++;
                if (f.Split) splits++;
                if (before > 19f && straight.TreeOffset == 0f) wrapped = true;
                Check(!f.Collision && straight.X == 40f, "Straight no-tree race");
                if (straight.ScrollCount == 8) Check(straight.Meters == 50, "First marker at scroll eight");
            }
            Check(straight.Finished && straight.ScrollCount == 500, "Race finishes at scroll 500");
            Check(markers == 20 && splits == 20 && straight.Meters == 1000, "Meter progression");
            Check(wrapped && straight.YVelocity == 1f, "Offset reset and speed limit");
            var stopped = new Game(TreeMode.NoTrees, _ => 1);
            stopped.Step(-1); stopped.Step(-1);
            for (int i = 0; i < 200; i++) stopped.Step(-1);
            Check(stopped.Direction == 1 && stopped.Stopped, "Left rotation clamp and braking");
            for (int i = 0; i < 200; i++) stopped.Step(1);
            Check(stopped.Direction == 5 && stopped.Stopped, "Right rotation clamp and braking");
            foreach (int turn in new[] { -1, 1 })
            {
                var diagonal = new Game(TreeMode.NoTrees, _ => 1);
                diagonal.Step(turn);
                for (int i = 0; i < 1000; i++) diagonal.Step();
                Check(diagonal.X == (turn < 0 ? 1f : 78f), "Slope edge clamp");
                Check(diagonal.YVelocity == .3f && diagonal.XVelocity == turn * .3f, "Diagonal terminal speed");
            }
            var hit = new Game(TreeMode.NoTrees, _ => 1);
            hit.Trees[2] = 41;
            Check(hit.Step().Collision, "Right half of two-column skier collides");
            hit.RecoverFromCrash();
            Check(hit.Crashes == 1 && hit.Stopped && hit.Direction == 3 && hit.Trees[2] == 0, "Crash reset");
            Check(!hit.Step().Collision && hit.YVelocity > 0, "Crash resumes downhill");
            var calls = new Queue<(int Bound, int Result)>([(80, 80), (80, 80), (2, 2)]);
            var normal = new Game(TreeMode.Normal, n =>
            {
                var expected = calls.Dequeue();
                Check(n == expected.Bound, "Nested normal random bounds/order");
                return expected.Result;
            });
            Frame frame;
            do { frame = normal.Step(); } while (!frame.Scrolled);
            Check(frame.NewTree == 39 && calls.Count == 0, "Normal tree wraps beyond right edge");
            calls = new([(80, 40), (2, 1), (80, 40), (2, 2)]);
            var uniform = new Game(TreeMode.Uniform, n =>
            {
                var expected = calls.Dequeue();
                Check(n == expected.Bound, "Uniform random bounds/order");
                return expected.Result;
            });
            do { frame = uniform.Step(); } while (!frame.Scrolled);
            Check(frame.NewTree == 40 && calls.Count == 0, "Uniform retries only half of direct hits");
            var now = DateTimeOffset.UtcNow;
            var scores = Enumerable.Range(1, 10).Select(i => new Score($"user{i}", i * 100, now)).ToList();
            Check(Scores.Insert(scores, new("late", 1001, now)).SequenceEqual(scores), "Full scoreboard rejects slower time");
            var improved = Scores.Insert(scores, new("user5", 150, now));
            Check(improved.Count == 10 && improved[1].Name == "user5" && improved.Count(s => s.Name == "user5") == 2, "Faster run preserves earlier run by same user");
            var equalRun = new Score("user5", 500, now.AddSeconds(1));
            Check(Scores.Insert(scores, equalRun)[5] == equalRun, "Equal personal time retained after older tie");
            Check(Scores.Insert(scores, new("tie", 100, now))[1].Name == "tie", "Ties stay behind existing scores");
            List<Score> repeatRuns = [];
            for (int i = 12; i >= 1; i--)
                repeatRuns = Scores.Insert(repeatRuns, new("solo", i * 100, now.AddSeconds(12 - i)));
            Check(repeatRuns.Count == 10 && repeatRuns.Select(s => s.ElapsedTicks).SequenceEqual(Enumerable.Range(1, 10).Select(i => (long)i * 100)), "Ten fastest of twelve runs by one user");
            var reloaded = System.Text.Json.JsonSerializer.Deserialize<List<Score>>(System.Text.Json.JsonSerializer.Serialize(repeatRuns))!;
            Check(Scores.Insert(reloaded, new("solo", 150, now.AddSeconds(20))).Select(s => s.ElapsedTicks).SequenceEqual(new long[] { 100, 150, 200, 300, 400, 500, 600, 700, 800, 900 }), "Reloaded JSON retains multiple runs on next insertion");
            Console.WriteLine($"PASS: {checks} checks (physics, tree generation, collisions, full race, high scores).");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL after {checks} checks: {ex.Message}");
            return 1;
        }
    }
}
