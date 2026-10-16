using System.Linq;
using DotsAndBoxes.Core;
using Renegade.Core;
using Xunit;

namespace DotsAndBoxes.Tests;

public class DotsTests
{
	[Fact]
	public void TheGridHasSixtyLinesAndEachBoxFourSides()
	{
		Assert.Equal(60, DotsGame.Lines);
		Assert.All(DotsGame.BoxEdges, edges => Assert.Equal(4, System.Numerics.BitOperations.PopCount(edges)));
		Assert.All(Enumerable.Range(0, DotsGame.Lines), line => Assert.InRange(DotsGame.LineBoxes[line].Length, 1, 2));
	}

	[Fact]
	public void ClosingABoxScoresAndGoesAgain()
	{
		var game = new DotsGame();
		game.Draw(DotsGame.HorizontalLine(0, 0));
		game.Draw(DotsGame.HorizontalLine(1, 0));
		game.Draw(DotsGame.VerticalLine(0, 0));
		Assert.Equal(Player.Second, game.Turn);
		var result = game.Draw(DotsGame.VerticalLine(0, 1));
		Assert.Equal(new[] { 0 }, result.Boxes);
		Assert.True(result.GoesAgain);
		Assert.Equal(Player.Second, game.Turn);
		Assert.Equal(1, game.Score(Player.Second));
		Assert.Equal(Player.Second, game.Owner(0));
	}

	[Fact]
	public void OneLineCanCloseTwoBoxes()
	{
		var game = new DotsGame();
		foreach (int line in new[] { DotsGame.HorizontalLine(0, 0), DotsGame.HorizontalLine(1, 0), DotsGame.VerticalLine(0, 0),
			DotsGame.HorizontalLine(0, 1), DotsGame.HorizontalLine(1, 1), DotsGame.VerticalLine(0, 2) })
			game.Draw(line);
		var result = game.Draw(DotsGame.VerticalLine(0, 1));
		Assert.Equal(2, result.Boxes.Count);
	}

	[Fact]
	public void UndoGivesTheBoxBack()
	{
		var game = new DotsGame();
		foreach (int line in new[] { DotsGame.HorizontalLine(0, 0), DotsGame.HorizontalLine(1, 0), DotsGame.VerticalLine(0, 0), DotsGame.VerticalLine(0, 1) })
			game.Draw(line);
		Assert.True(game.Undo());
		Assert.Null(game.Owner(0));
		Assert.Equal(0, game.Score(Player.Second));
	}

	[Fact]
	public void AFullGameFillsEveryLineAndEveryBox()
	{
		var game = new DotsGame();
		var computer = new ComputerPlayer(Difficulty.Medium, seed: 1);
		while (!game.IsOver)
			game.Draw(computer.ChooseLine(game));
		Assert.Equal(DotsGame.Boxes, game.Score(Player.First) + game.Score(Player.Second));
	}

	[Fact]
	public void HardBeatsEasyAndThinksQuickly()
	{
		int hardWins = 0;
		long worst = 0;
		for (int i = 0; i < 6; i++)
		{
			var game = new DotsGame();
			var hard = new ComputerPlayer(Difficulty.Hard, seed: i);
			var easy = new ComputerPlayer(Difficulty.Easy, seed: 30 + i);
			var hardSide = i % 2 == 0 ? Player.First : Player.Second;
			while (!game.IsOver)
			{
				var watch = System.Diagnostics.Stopwatch.StartNew();
				int line = (game.Turn == hardSide ? hard : easy).ChooseLine(game);
				if (game.Turn == hardSide)
					worst = System.Math.Max(worst, watch.ElapsedMilliseconds);
				game.Draw(line);
			}
			if (game.Winner == hardSide)
				hardWins++;
		}
		Assert.True(hardWins >= 5, $"Hard won only {hardWins} of 6.");
		Assert.True(worst < 1500, $"Slowest move took {worst} ms.");
	}
}
