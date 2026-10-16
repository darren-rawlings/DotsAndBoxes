using System;
using System.Collections.Generic;

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
/// Dots and Boxes on a square grid of <see cref="Size"/> × <see cref="Size"/> boxes (3 to 7).
/// Lines are bits in a 128-bit mask: first the horizontal lines, row by row ((size + 1) × size of
/// them), then the vertical ones ((size + 1) per row). Closing a box scores it and earns another
/// turn; when every line is drawn, the most boxes wins.
/// </summary>
public sealed class DotsGame
{
	public const int MinSize = 3;
	public const int MaxSize = 7;

	private readonly Player?[] _owners;
	private readonly Stack<(UInt128 Drawn, Player Turn, int[] Scores, int[] Claimed, int Line)> _history = new();
	private readonly Player?[] _drawers;
	private readonly int[] _scores = new int[2];

	public DotsGame(int size = 5)
	{
		if (size < MinSize || size > MaxSize)
			throw new ArgumentOutOfRangeException(nameof(size));
		Size = size;
		Horizontal = (size + 1) * size;
		Lines = Horizontal * 2;
		Boxes = size * size;
		AllLines = (UInt128.One << Lines) - 1;
		BoxEdges = new UInt128[Boxes];
		for (int row = 0; row < size; row++)
		{
			for (int column = 0; column < size; column++)
			{
				BoxEdges[row * size + column] =
					Bit(HorizontalLine(row, column)) | Bit(HorizontalLine(row + 1, column)) |
					Bit(VerticalLine(row, column)) | Bit(VerticalLine(row, column + 1));
			}
		}
		var lineBoxes = new List<int>[Lines];
		for (int line = 0; line < Lines; line++)
			lineBoxes[line] = new List<int>();
		for (int box = 0; box < Boxes; box++)
		{
			for (int line = 0; line < Lines; line++)
			{
				if ((BoxEdges[box] & Bit(line)) != 0)
					lineBoxes[line].Add(box);
			}
		}
		LineBoxes = Array.ConvertAll(lineBoxes, list => list.ToArray());
		_owners = new Player?[Boxes];
		_drawers = new Player?[Lines];
		Reset();
	}

	private DotsGame(DotsGame other)
	{
		Size = other.Size;
		Horizontal = other.Horizontal;
		Lines = other.Lines;
		Boxes = other.Boxes;
		AllLines = other.AllLines;
		BoxEdges = other.BoxEdges;
		LineBoxes = other.LineBoxes;
		_owners = (Player?[])other._owners.Clone();
		_drawers = (Player?[])other._drawers.Clone();
		Drawn = other.Drawn;
		Turn = other.Turn;
		Array.Copy(other._scores, _scores, 2);
	}

	public DotsGame Clone() => new(this);

	public int Size { get; }
	public int Horizontal { get; }
	public int Lines { get; }
	public int Boxes { get; }
	public UInt128 AllLines { get; }

	/// <summary>The four lines round each box.</summary>
	public UInt128[] BoxEdges { get; }

	/// <summary>The one or two boxes beside each line.</summary>
	public int[][] LineBoxes { get; }

	public UInt128 Drawn { get; private set; }
	public Player Turn { get; private set; }

	public static UInt128 Bit(int line) => UInt128.One << line;

	public void Reset()
	{
		Drawn = 0;
		Turn = Player.First;
		Array.Clear(_owners);
		Array.Clear(_drawers);
		Array.Clear(_scores);
		_history.Clear();
	}

	public bool IsOver => Drawn == AllLines;

	public int MovesMade => _history.Count;

	public Player? LastMover => _history.Count == 0 ? null : _history.Peek().Turn;

	public int Score(Player player) => _scores[(int)player];

	public Player? Owner(int box) => _owners[box];

	/// <summary>Who drew a line, if it's drawn.</summary>
	public Player? DrewLine(int line) => _drawers[line];

	public bool IsDrawn(int line) => (Drawn & Bit(line)) != 0;

	public static Player Opponent(Player player) => player == Player.First ? Player.Second : Player.First;

	public Player? Winner => !IsOver || _scores[0] == _scores[1] ? null : _scores[0] > _scores[1] ? Player.First : Player.Second;

	public IEnumerable<int> OpenLines()
	{
		var open = ~Drawn & AllLines;
		while (open != 0)
		{
			yield return (int)UInt128.TrailingZeroCount(open);
			open &= open - 1;
		}
	}

	public LineResult Draw(int line)
	{
		if (line < 0 || line >= Lines || IsDrawn(line))
			throw new InvalidOperationException($"Line {line} can't be drawn.");
		var claimed = new List<int>();
		var before = (Drawn, Turn, (int[])_scores.Clone());
		var drawer = Turn;
		Drawn |= Bit(line);
		_drawers[line] = drawer;
		foreach (int box in LineBoxes[line])
		{
			if ((Drawn & BoxEdges[box]) == BoxEdges[box])
			{
				_owners[box] = drawer;
				_scores[(int)drawer]++;
				claimed.Add(box);
			}
		}
		_history.Push((before.Drawn, before.Turn, before.Item3, claimed.ToArray(), line));
		bool again = claimed.Count > 0 && !IsOver;
		if (claimed.Count == 0)
			Turn = Opponent(drawer);
		return new LineResult(drawer, line, claimed, again, IsOver);
	}

	public bool Undo()
	{
		if (_history.Count == 0)
			return false;
		var (drawn, turn, scores, claimed, line) = _history.Pop();
		_drawers[line] = null;
		Drawn = drawn;
		Turn = turn;
		Array.Copy(scores, _scores, 2);
		foreach (int box in claimed)
			_owners[box] = null;
		return true;
	}

	/// <summary>How many of a box's four sides are drawn in a line mask.</summary>
	public int Sides(UInt128 drawn, int box) => (int)UInt128.PopCount(drawn & BoxEdges[box]);

	/// <summary>The horizontal line above box row <paramref name="row"/> (0..Size), column <paramref name="column"/>.</summary>
	public int HorizontalLine(int row, int column) => row * Size + column;

	/// <summary>The vertical line left of box column <paramref name="column"/> (0..Size), in row <paramref name="row"/>.</summary>
	public int VerticalLine(int row, int column) => Horizontal + row * (Size + 1) + column;

	public bool IsHorizontal(int line) => line < Horizontal;
}
