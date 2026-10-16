using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Renegade.Core;

namespace DotsAndBoxes.Core;

/// <summary>
/// The computer opponent.
/// Easy takes any box it can, otherwise plays anywhere (it will often hand you boxes).
/// Medium takes boxes, never draws a third side when a safe line exists, and when it must give
/// something away, gives the smallest chain.
/// Hard plays like Medium until few lines are left, then solves the rest of the game exactly
/// (memoised search over which lines are still open), so its endgame is perfect.
/// </summary>
public sealed class ComputerPlayer
{
	/// <summary>At or below this many open lines, Hard searches to the end.</summary>
	public const int ExactLines = 18;

	private readonly Random _random;
	private readonly Dictionary<UInt128, int> _memo = new();

	public Difficulty Difficulty { get; }

	public ComputerPlayer(Difficulty difficulty, int? seed = null)
	{
		Difficulty = difficulty;
		_random = seed is { } s ? new Random(s) : new Random();
	}

	public int ChooseLine(DotsGame game)
	{
		var drawn = game.Drawn;
		var open = game.OpenLines().ToList();
		if (open.Count == 0)
			throw new InvalidOperationException("No lines left.");

		if (Difficulty == Difficulty.Hard && open.Count <= ExactLines)
			return BestExact(game, drawn, open);

		// Take a box whenever one is there.
		var closing = open.Where(line => Closes(game, drawn, line) > 0).ToList();
		if (closing.Count > 0)
			return closing[_random.Next(closing.Count)];

		if (Difficulty == Difficulty.Easy)
			return open[_random.Next(open.Count)];

		// Safe lines don't give the opponent a box.
		var safe = open.Where(line => !GivesBox(game, drawn, line)).ToList();
		if (safe.Count > 0)
			return safe[_random.Next(safe.Count)];

		// Everything gives something away: give as little as possible.
		return open.OrderBy(line => GiftSize(game, drawn, line)).ThenBy(_ => _random.Next()).First();
	}

	/// <summary>Boxes a line would close right now.</summary>
	private static int Closes(DotsGame game, UInt128 drawn, int line)
	{
		var after = drawn | DotsGame.Bit(line);
		return game.LineBoxes[line].Count(box => (after & game.BoxEdges[box]) == game.BoxEdges[box]);
	}

	/// <summary>True if drawing the line leaves a box with three sides for the opponent.</summary>
	private static bool GivesBox(DotsGame game, UInt128 drawn, int line) =>
		game.LineBoxes[line].Any(box => game.Sides(drawn, box) == 2);

	/// <summary>How many boxes the opponent could then take in a row (the chain this line opens).</summary>
	private static int GiftSize(DotsGame game, UInt128 drawn, int line)
	{
		var state = drawn | DotsGame.Bit(line);
		int taken = 0;
		while (true)
		{
			int next = -1;
			var open = ~state & game.AllLines;
			while (open != 0)
			{
				int candidate = (int)UInt128.TrailingZeroCount(open);
				open &= open - 1;
				if (Closes(game, state, candidate) > 0)
				{
					next = candidate;
					break;
				}
			}
			if (next < 0)
				return taken;
			taken += Closes(game, state, next);
			state |= DotsGame.Bit(next);
		}
	}

	private int BestExact(DotsGame game, UInt128 drawn, List<int> open)
	{
		_memo.Clear();
		int best = int.MinValue;
		var bestLines = new List<int>();
		foreach (int line in open)
		{
			int gained = Closes(game, drawn, line);
			var after = drawn | DotsGame.Bit(line);
			// Closing a box keeps the turn; otherwise the opponent's best result counts against us.
			int value = gained > 0 ? gained + Solve(game, after) : -Solve(game, after);
			if (value > best)
			{
				best = value;
				bestLines.Clear();
			}
			if (value == best)
				bestLines.Add(line);
		}
		return bestLines[_random.Next(bestLines.Count)];
	}

	/// <summary>
	/// The best box margin the side to move can still get from this position. It depends only on
	/// which lines are drawn, not on the score so far, so it's memoised per line mask.
	/// </summary>
	private int Solve(DotsGame game, UInt128 drawn)
	{
		if (drawn == game.AllLines)
			return 0;
		if (_memo.TryGetValue(drawn, out int known))
			return known;
		int best = int.MinValue;
		var open = ~drawn & game.AllLines;
		while (open != 0)
		{
			int line = (int)UInt128.TrailingZeroCount(open);
			open &= open - 1;
			int gained = Closes(game, drawn, line);
			var after = drawn | DotsGame.Bit(line);
			int value = gained > 0 ? gained + Solve(game, after) : -Solve(game, after);
			best = Math.Max(best, value);
		}
		_memo[drawn] = best;
		return best;
	}
}
