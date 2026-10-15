namespace GameTemplate.Core;

/// <summary>
/// Wins for two sides across a session. Rules code lives in Core: plain C# with no Godot types,
/// so it is unit tested without the engine (see tests/GameTemplate.Tests).
/// </summary>
public sealed class Scoreboard
{
	public int First { get; private set; }
	public int Second { get; private set; }

	public void Win(bool first)
	{
		if (first)
			First++;
		else
			Second++;
	}

	/// <summary>Takes a win back, for Undo after a game-ending move. Never goes below zero.</summary>
	public void Unwin(bool first)
	{
		if (first && First > 0)
			First--;
		else if (!first && Second > 0)
			Second--;
	}

	public void Reset() => First = Second = 0;
}
