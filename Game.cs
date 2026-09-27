namespace Ski;

public enum TreeMode { Normal, NoTrees, Uniform }
public readonly record struct Frame(bool Scrolled, int NewTree, bool Marker, bool Split, bool Collision);

/// <summary>One Step is one pass through Fortran labels 100–110. Coordinates are one-based.</summary>
public sealed class Game(TreeMode mode, Func<int, int> random)
{
    public const string Shapes = "=/|\\=";
    public const int RaceLength = 1000;
    public float X { get; private set; } = 40f;
    public float XVelocity { get; private set; }
    public float YVelocity { get; private set; }
    public float TreeOffset { get; private set; }
    public int Direction { get; private set; } = 3;
    public int PreviousOffset { get; private set; }
    public int Meters { get; private set; }
    public int Crashes { get; private set; }
    public int ScrollCount { get; private set; }
    public bool Finished { get; private set; }
    public int[] Trees { get; } = new int[20];
    public bool Stopped => XVelocity == 0f && YVelocity == 0f;
    public char Shape => Shapes[Direction - 1];
    private int meterCounter;

    public Frame Step(int turn = 0)
    {
        if (Finished) throw new InvalidOperationException("The race is over.");
        Direction = Math.Clamp(Direction + turn, 1, 5);
        switch (Direction)
        {
            case 1 or 5:
                YVelocity = Approach(YVelocity, 0f, .03f);
                XVelocity = Approach(XVelocity, 0f, .005f);
                break;
            case 2 or 4:
                YVelocity = Approach(YVelocity, .3f, .01f + .01f * YVelocity);
                XVelocity = Approach(XVelocity, Direction == 2 ? -.3f : .3f, .2f * YVelocity);
                break;
            case 3:
                YVelocity = Approach(YVelocity, 1f, .01f);
                XVelocity = Approach(XVelocity, 0f, .1f);
                break;
        }
        TreeOffset += YVelocity;
        // Deliberately discard the overshoot, as the original does (not modulo).
        if (TreeOffset > 20f) TreeOffset = 0f;
        X = Math.Clamp(X + XVelocity, 1f, 78f);
        bool scrolled = (int)TreeOffset != PreviousOffset;
        bool marker = false, split = false;
        int newTree = 0;
        if (scrolled)
        {
            int slot = (int)TreeOffset;
            if (slot == 0) slot = 20;
            newTree = GenerateTree();
            Trees[slot - 1] = newTree;
            ScrollCount++;
            meterCounter++;
            if (meterCounter == 8)
            {
                Meters += 50;
                marker = true;
            }
            else if (meterCounter == 25)
            {
                split = true;
                // Finish precedes collision detection and saving the offset in Fortran.
                if (Meters == RaceLength)
                {
                    Finished = true;
                    return new(scrolled, newTree, marker, split, false);
                }
                meterCounter = 0;
            }
            PreviousOffset = (int)TreeOffset;
        }
        int nearest = (3 + PreviousOffset - 1) % 20;
        bool collision = Trees[nearest] == (int)X || Trees[nearest] == (int)X + 1;
        return new(scrolled, newTree, marker, split, collision);
    }

    public void RecoverFromCrash()
    {
        Trees[(3 + PreviousOffset - 1) % 20] = 0;
        XVelocity = YVelocity = 0f;
        Direction = 3;
        Crashes++;
    }

    private int GenerateTree()
    {
        if (mode == TreeMode.NoTrees) return 79;
        if (mode == TreeMode.Uniform)
        {
            int column;
            do { column = random(80); }
            while (column == (int)X && random(2) == 1);
            return column;
        }
        int j = (random(random(80)) - 1) * (random(2) * 2 - 3) + (int)X;
        if (j > 80) j -= 80;
        if (j < 1) j += 80;
        return j;
    }

    public static float Approach(float value, float target, float speed)
    {
        float sign = target - value < 0f ? -1f : 1f;
        value += sign * speed;
        if (sign != (target - value < 0f ? -1f : 1f)) value = target;
        return value;
    }
}
