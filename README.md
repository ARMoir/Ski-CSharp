# Ski — C# port of the VAX/VMS game

Port of Chris Pirih's 1985 Fortran **Ski**, fetched in full from
<https://ski.ihoc.net/ski.for> on September 27, 2026. This is the text-terminal
game, not the later Windows SkiFree. The original 318-line file is included
unchanged in `original/ski.for` (SHA-256
`3FEA1F49BA7D31BDE0936B7CBF5F91114CD3B46349DC187525AE162666C38B04`).
Original notice: **Copyright 1985 by chris pirih**. The supplied source has no
license grant; this port does not assert a new license over the original work.

## Attribution

- **Original game and Fortran source:** Chris Pirih, 1985, Proverbial Software.
  [Original Ski source](https://ski.ihoc.net/ski.for) ·
  [Author's SkiFree history and homepage](https://ski.ihoc.net/).
- **C# port:** ChatGPT GPT-6 Astra Medium, at the request of
  [ARMoir](https://github.com/ARMoir).
- The original copyright notice is retained above and in
  [`original/ski.for`](original/ski.for). Porting credit does not replace
  attribution to the original author.

## Build and play

Install the .NET 10 SDK. From this folder:

```text
dotnet build -c Release
dotnet run -c Release --no-build
```

Use an interactive terminal at least **80 columns by 24 rows** (Windows Terminal,
or a console on macOS/Linux). Press **Enter** to start, **4**/**6** to rotate the
skis, and **Ctrl+Y** to quit. Escape and Ctrl+C are added quit aliases. Num Lock
must be on when using the numeric keypad. Stopping sideways waits for a key;
the race clock continues running. A completed race is 1,000 meters.

```text
dotnet run -c Release -- --no-trees
dotnet run -c Release -- --uniform --tick-rate 90 --seed 123
dotnet run -c Release -- --self-test
dotnet run -c Release -- --help
```

The quoted argument `">"` also selects no-trees mode; `"."` selects uniform
trees. Quote `>` so the shell does not interpret it as output redirection.
`--tick-rate` accepts 1–1000 and defaults to 60. A seed makes tree generation
repeatable for the same inputs, tick sequence, and .NET implementation. No
NuGet packages or network services are required to run the game or tests.

Optional framework-dependent publish:

```text
dotnet publish -c Release -o publish
dotnet publish/Ski.dll
```

## Project map

- `Game.cs`: independent simulation, single-precision physics, tree ring buffer.
- `Program.cs`: input, pacing, clock, scrolling terminal display, crash animation.
- `Scores.cs`: ten-entry personal-best scoreboard stored as JSON.
- `SelfTests.cs`: dependency-free, headless behavioral checks.
- `original/ski.for`: unmodified reference source.

## Preserved behavior

The skier starts at column 40, row 3, facing downhill. Five orientations use
the exact `=/|\=` character sequence, doubled for the two-column skier.
Acceleration uses the original constants and ordering (vertical acceleration
before horizontal acceleration). Coordinates and velocities use C# `float`,
with positive coordinate conversions truncating like Fortran `INT`.

The horizontal limit is 1–78. The 20-slot tree offset resets to **zero** only
when it exceeds 20, discarding overshoot. A changed integer offset generates
one row, even at wraparound. Normal mode retains nested random bounds and
wrapping around column 80. Uniform mode retries a tree at the skier's left
column with 50% probability; it does not retry its right column. No-trees mode
still generates trees in column 79, and a skier at column 78 can hit them.

The first marker appears at scroll 8, then every 25 scrolls. Time is sampled at
scroll 25 of each group; the race ends on the 500th scroll, before that tick's
collision check. A crash plays three 50 ms animation steps, waits
`floor(verticalVelocity * 3) + 0.2` seconds, destroys the relevant tree, resets
velocity and direction, counts a crash, and discards queued input. The clock
includes crashes and time spent stationary. The displayed split has the
original minutes:seconds.hundredths format (minutes wrap each hour).

Scores prefer strictly faster elapsed times, retain one personal best per
12-character username, preserve existing entries ahead of tied newcomers,
and keep ten entries. No-trees races display scores but do not update them.
As in the source, normal and uniform modes share a score list.

## Fortran-era ambiguities and deliberate replacements

1. **Missing code:** `secure`, `iassign`, `passall`, `nopassall`, `inpchr`,
   `astwake`, `subq`, `iqg`, `iqz`, `usrnam`, and `slow` are not defined in the
   download. The system `SYS$`, `LIB$`, and `OTS$` routines require VMS.
   Console input/output, `Stopwatch`, integer time comparisons, and the local
   username replace their apparent purposes. `secure('Ski')` was described as
   copy protection and has no portable equivalent here. The unknown `slow`
   finish pause and associated post-finish keyboard purge are omitted.
2. **Uninitialized storage:** `ix`, `ptoff`, `jc`, `meter`, `mash`, and `tree`
   are read before explicit initialization. This port assumes zeroed counters
   and trees, with the previous skier column initialized to 40. Standard
   Fortran does not guarantee those original values.
3. **Speed:** there is no delay in the original moving loop. Terminal bandwidth
   and VMS I/O therefore affect difficulty and elapsed time; the source even
   groups scores by baud-rate category. This port uses configurable wall-clock
   pacing, with separate files per nominal tick rate instead of baud rate.
   Slow rendering can reduce actual tick rate; no catch-up ticks are executed.
   Historical and modern scores are not directly comparable.
4. **Random numbers:** VAX `RAN` and its exact seed/runtime sequence are replaced
   with `System.Random`. Discrete ranges and distribution formulas are retained;
   historical tree sequences cannot be promised. Fortran may evaluate the
   operands of `.AND.` eagerly and expression operands in a different order;
   this implementation specifies left-to-right evaluation and short-circuit
   retry logic. Default seeding is modern rather than milliseconds since midnight.
5. **Floating point:** VAX single precision is not IEEE 754. C# uses IEEE binary32,
   so rounding at boundaries may differ despite matching precision and formulas.
6. **Time bug:** `fquad` contains `if (f1 .lt. 0) f1=f+xlong`, referring to the
   uninitialized `f` instead of `f1`. It can corrupt time-to-speed conversion
   when the low quadword is signed-negative. This is treated as a typo, not
   emulated. Monotonic elapsed time replaces VMS 100 ns timestamps and missing
   subtraction helpers. Average speed still truncates miles/hour using 1609
   meters per mile. Unused `long` and `mod` helpers are omitted.
7. **Terminal details:** an in-memory 80×20 display and console cursor positioning
   replace VT100 scroll regions and device-specific escape sequences. The intro
   scrolls away as in the original. Crash fragments outside columns 1–80 are
   clipped instead of emitting invalid cursor positions. The original finish
   blink and video attributes are omitted; finishing prints results. Terminal
   cursor visibility and Ctrl+C handling are restored on normal exit/error
   (the cursor is made visible). Resizing below the minimum exits with an error.
8. **Scores:** VMS shared relative files at
   `sys$sysdevice:<pirih.images.user>` become JSON under the OS local application
   data directory, `VaxSkiPort/scores-{rate}hz.json`. Files are created as needed;
   missing files no longer abort. Writes use temporary-file replacement, but
   concurrent games are not locked and the last writer can win. Bad/unwritable
   score files produce a message rather than discarding race results. No VMS
   binary record importer is provided. Entry dates use local time.

## Validation

`--self-test` checks clamped acceleration, stopping and rotation limits,
diagonal motion and slope edges, tree generation call order and wrapping,
uniform retries, two-column collision/recovery, a complete no-trees race with
exact marker/split/finish counts, and score insertion/ties/personal bests.
These are behavioral regression tests, not proof of equivalence to an executable
VMS version: the necessary external helpers and original runtime are absent.
The project was built and tests executed on Windows with .NET SDK 10.0.401.
Interactive terminal play and macOS/Linux execution have not been manually tested.
