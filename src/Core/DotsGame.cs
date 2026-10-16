using System;
using System.Collections.Generic;
using System.Numerics;

namespace DotsAndBoxes.Core;

/// <summary>Who's playing: First moves first; against the computer, that's you.</summary>
public enum Player
{
	First,
	Second,
}

/// <summary>What a line did: the boxes it closed (if any) and whether the drawer goes again.</summary>
public sealed record LineResult(Player Drawer, int Line, IReadOnlyList<int> Boxes, bool GoesAgain, bool GameOver);

/// <summary>
/// Dots and Boxes on a grid of <see cref="Rows"/> × <see cref="Columns"/> boxes. Lines are bits in
/// a 64-bit mask: first the horizontal lines, row by row ((rows + 1) × columns of them), then the
/// vertical ones ((columns + 1) per row). Closing a box scores it and earns another turn; when every
/// line is drawn, the most boxes wins.
/// </summary>
public sealed class DotsGame
{
	public const int Rows = 5;
	public const int Columns = 5;
	public const int Horizontal = (Rows + 1) * Columns;
	public const int Lines = Horizontal + Rows * (Columns + 1);
	public const int Boxes = Rows * Columns;

	/// <summary>The four lines round each box, precomputed.</summary>
	public static readonly ulong[] BoxEdges = BuildEdges();

	/// <summary>The one or two boxes beside each line.</summary>
	public static readonly int[][] LineBoxes = BuildLineBoxes();

	private readonly Player?[] _owners = new Player?[Boxes];
	private readonly Stack<(ulong Drawn, Player Turn, int[] Scores, int[] Claimed)> _history = new();
	private readonly int[] _scores = new int[2];

	public ulong Drawn { get; private set; }
	public Player Turn { get; private set; }

	public DotsGame() => Reset();

	private DotsGame(DotsGame other)
	{
		Drawn = other.Drawn;
		Turn = other.Turn;
		Array.Copy(other._owners, _owners, Boxes);
		Array.Copy(other._scores, _scores, 2);
	}

	public DotsGame Clone() => new(this);

	public void Reset()
	{
		Drawn = 0;
		Turn = Player.First;
		Array.Clear(_owners);
		Array.Clear(_scores);
		_history.Clear();
	}

	public static ulong AllLines => (1UL << Lines) - 1;

	public bool IsOver => Drawn == AllLines;

	public int MovesMade => _history.Count;

	public Player? LastMover => _history.Count == 0 ? null : _history.Peek().Turn;

	public int Score(Player player) => _scores[(int)player];

	public Player? Owner(int box) => _owners[box];

	public bool IsDrawn(int line) => (Drawn & (1UL << line)) != 0;

	public static Player Opponent(Player player) => player == Player.First ? Player.Second : Player.First;

	public Player? Winner => !IsOver || _scores[0] == _scores[1] ? null : _scores[0] > _scores[1] ? Player.First : Player.Second;

	public IEnumerable<int> OpenLines()
	{
		ulong open = ~Drawn & AllLines;
		while (open != 0)
		{
			yield return BitOperations.TrailingZeroCount(open);
			open &= open - 1;
		}
	}

	public LineResult Draw(int line)
	{
		if (line < 0 || line >= Lines || IsDrawn(line))
			throw new InvalidOperationException($"Line {line} can't be drawn.");
		var claimed = new List<int>();
		_history.Push((Drawn, Turn, (int[])_scores.Clone(), Array.Empty<int>()));
		var drawer = Turn;
		Drawn |= 1UL << line;
		foreach (int box in LineBoxes[line])
		{
			if ((Drawn & BoxEdges[box]) == BoxEdges[box])
			{
				_owners[box] = drawer;
				_scores[(int)drawer]++;
				claimed.Add(box);
			}
		}
		// Remember which boxes this line claimed, so undo can give them back.
		var top = _history.Pop();
		_history.Push((top.Drawn, top.Turn, top.Scores, claimed.ToArray()));
		bool again = claimed.Count > 0 && !IsOver;
		if (claimed.Count == 0)
			Turn = Opponent(drawer);
		return new LineResult(drawer, line, claimed, again, IsOver);
	}

	public bool Undo()
	{
		if (_history.Count == 0)
			return false;
		var (drawn, turn, scores, claimed) = _history.Pop();
		Drawn = drawn;
		Turn = turn;
		Array.Copy(scores, _scores, 2);
		foreach (int box in claimed)
			_owners[box] = null;
		return true;
	}

	/// <summary>How many of a box's four sides are drawn in a line mask.</summary>
	public static int Sides(ulong drawn, int box) => BitOperations.PopCount(drawn & BoxEdges[box]);

	// ---------------------------------------------------------------- Geometry

	/// <summary>The horizontal line above box row <paramref name="row"/> (0..Rows), column <paramref name="column"/>.</summary>
	public static int HorizontalLine(int row, int column) => row * Columns + column;

	/// <summary>The vertical line left of box column <paramref name="column"/> (0..Columns), in row <paramref name="row"/>.</summary>
	public static int VerticalLine(int row, int column) => Horizontal + row * (Columns + 1) + column;

	public static bool IsHorizontal(int line) => line < Horizontal;

	private static ulong[] BuildEdges()
	{
		var edges = new ulong[Boxes];
		for (int row = 0; row < Rows; row++)
		{
			for (int column = 0; column < Columns; column++)
			{
				edges[row * Columns + column] =
					(1UL << HorizontalLine(row, column)) | (1UL << HorizontalLine(row + 1, column)) |
					(1UL << VerticalLine(row, column)) | (1UL << VerticalLine(row, column + 1));
			}
		}
		return edges;
	}

	private static int[][] BuildLineBoxes()
	{
		var boxes = new List<int>[Lines];
		for (int line = 0; line < Lines; line++)
			boxes[line] = new List<int>();
		var edges = BuildEdges();
		for (int box = 0; box < Boxes; box++)
		{
			for (int line = 0; line < Lines; line++)
			{
				if ((edges[box] & (1UL << line)) != 0)
					boxes[line].Add(box);
			}
		}
		return Array.ConvertAll(boxes, list => list.ToArray());
	}
}
