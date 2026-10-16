using System.Collections.Generic;
using Renegade.Ui;

namespace DotsAndBoxes.Game;

/// <summary>The guided tour. Clips are recorded from the game with --clip (see Main.PlayClip).</summary>
public static class Tutorial
{
	public static readonly IReadOnlyList<TutorialPage> Pages = new[]
	{
		new TutorialPage("Draw a line",
			"Take turns drawing one line between two neighbouring pegs: just tap between them.",
			"res://tutorial/draw.ogv"),
		new TutorialPage("Close a box",
			"Draw the fourth side of a box and it's yours: a tile in your colour springs up, and you draw again.",
			"res://tutorial/box.ogv"),
		new TutorialPage("Chains",
			"Closing one box often opens the next, so keep going and take the whole chain. Be careful with third sides: drawing one hands that box to your opponent.",
			"res://tutorial/box.ogv"),
		new TutorialPage("Winning",
			"When every line is drawn, the most boxes wins, and the winner's boxes catch fire.",
			"res://tutorial/end.ogv"),
		new TutorialPage("Your board",
			"Pick a board from 3×3 to 7×7 boxes in Settings, where you can also freeze the board so it doesn't spin. Against the computer, Undo (ad) takes back your last turn.",
			"res://tutorial/draw.ogv"),
	};
}
