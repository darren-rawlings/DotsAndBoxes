using GameTemplate.Core;
using Xunit;

namespace GameTemplate.Tests;

public class ScoreboardTests
{
	[Fact]
	public void CountsWinsForEachSide()
	{
		var score = new Scoreboard();
		score.Win(first: true);
		score.Win(first: true);
		score.Win(first: false);
		Assert.Equal(2, score.First);
		Assert.Equal(1, score.Second);
	}

	[Fact]
	public void UnwinNeverGoesNegative()
	{
		var score = new Scoreboard();
		score.Unwin(first: true);
		Assert.Equal(0, score.First);
	}
}
