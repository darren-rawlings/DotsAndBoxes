using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotsAndBoxes.Core;
using Godot;
using Renegade.Ads;
using Renegade.Audio;
using Renegade.Core;
using Renegade.Dev;
using Renegade.Looks;
using Renegade.Play;
using Renegade.Ui;

namespace DotsAndBoxes.Game;

/// <summary>
/// Dots and Boxes. Tap between two pegs and a stick drops into place in your colour. Close a box and
/// a glowing tile in your colour springs up into it with sparks (the signature "box pop"); a chain
/// of boxes pops one after another, each note a little higher, and you go again. Drag sideways to
/// spin the board.
/// </summary>
public partial class Main : Node3D
{
	public const string Title = "Dots and Boxes";

	/// <summary>Google's test ad units until the real ones exist in AdMob (see the release playbook).</summary>
	private static readonly AdMobConfig AdUnits = AdMobConfig.Test;

	private const Player Human = Player.First;
	private const float Cell = 1.5f;
	private const float GridHalf = DotsGame.Columns * Cell / 2f;
	private const float Half = GridHalf + 0.6f;
	private const float Thickness = 0.4f;

	private readonly DotsGame _game = new();
	private readonly Scoreboard _wins = new();
	private readonly MeshInstance3D?[] _sticks = new MeshInstance3D?[DotsGame.Lines];
	private readonly MeshInstance3D?[] _tiles = new MeshInstance3D?[DotsGame.Boxes];
	private readonly ImpactBank<Style> _impacts = new(Styles.Impact);
	private readonly ComputerTurn _computer = new();

	private GameSettings _settings = null!;
	private Node3D _rig = null!;
	private Camera3D _camera = null!;
	private SoundPool _sound = null!;
	private GameHud _hud = null!;
	private IAdService _ads = null!;
	private AdSchedule? _adSchedule;
	private SpinControl _spin = null!;
	private MeshInstance3D _slab = null!;
	private bool _busy;
	private bool _roundCounted;
	private int _chain;

	private bool _demo;
	private bool _quitAfterDemo;
	private float _initialSpin;

	private bool VsComputer => _settings.Mode == GameMode.PlayerVsComputer || _demo;

	private bool IsComputerTurn => !_game.IsOver && (_demo || (VsComputer && _game.Turn != Human));

	private PieceSet Colours => Styles.Pieces(_settings.Pieces);

	public override void _Ready()
	{
		DevLog.Tag = "dab";
		AudioWatch.Attach(this, Title);
		_settings = GameSettings.Load(board: Style.Wood, pieces: Style.Glass);
		ReadDeveloperArgs();
		AudioServer.SetBusMute(0, _settings.Muted);
		BuildWorld();

		_sound = new SoundPool();
		AddChild(_sound);
		_adSchedule ??= new AdSchedule(_settings.GamesSinceAd);
		var theme = HudTheme.Warm;
		_ads = AdServices.Create(this, AdUnits, _demo, new PlaceholderAds
		{
			TextColor = theme.Text,
			RewardTitle = "Watch to undo your move",
			ClaimText = "Claim undo",
			SkipText = "Skip (no undo)",
		});

		_hud = new GameHud(theme);
		AddChild(_hud);
		_hud.Build(new HudOptions(Title, "Tap between two pegs  ·  Drag sideways to spin", PiecesLabel: "Colours",
			PieceName: s => $"{Styles.Pieces(s).NameOne} & {Styles.Pieces(s).NameTwo}"),
			_settings.Muted, _settings.Mode, _settings.Difficulty, _settings.Board, _settings.Pieces);
		_hud.NewGamePressed += OnNewGamePressed;
		_hud.UndoPressed += OnUndoPressed;
		_hud.MuteToggled += muted =>
		{
			_settings.Muted = muted;
			AudioServer.SetBusMute(0, muted);
			_settings.Save();
		};
		_hud.ModeChosen += (mode, difficulty) =>
		{
			_settings.Mode = mode;
			_settings.Difficulty = difficulty;
			_settings.Save();
			_wins.Reset();
			_ = StartGame();
		};
		_hud.BoardChosen += style =>
		{
			_settings.Board = style;
			_settings.Save();
			_slab.MaterialOverride = Materials.For(Styles.BoardLook(style));
		};
		_hud.PiecesChosen += style =>
		{
			_settings.Pieces = style;
			_settings.Save();
			Rebuild(animate: false);
		};

		_spin = new SpinControl(_rig, () => !_busy);
		_spin.Tapped += OnTapped;
		_ = StartGame();
	}

	// ---------------------------------------------------------------- World

	private static Vector3 Dot(int row, int column) => new(column * Cell - GridHalf, 0, row * Cell - GridHalf);

	private static Vector3 LineCenter(int line)
	{
		if (DotsGame.IsHorizontal(line))
		{
			int row = line / DotsGame.Columns, column = line % DotsGame.Columns;
			return (Dot(row, column) + Dot(row, column + 1)) / 2;
		}
		int index = line - DotsGame.Horizontal;
		int r = index / (DotsGame.Columns + 1), c = index % (DotsGame.Columns + 1);
		return (Dot(r, c) + Dot(r + 1, c)) / 2;
	}

	private static Vector3 BoxCenter(int box) => (Dot(box / DotsGame.Columns, box % DotsGame.Columns) + Dot(box / DotsGame.Columns + 1, box % DotsGame.Columns + 1)) / 2;

	private void BuildWorld()
	{
		_camera = new Camera3D { Position = new Vector3(0, 10.4f, 6.4f), Fov = 54, KeepAspect = Camera3D.KeepAspectEnum.Width };
		AddChild(_camera);
		_camera.LookAt(new Vector3(0, -0.4f, 0.3f));
		Stage.Build(this, -Thickness, new Color("1a0f09"));

		_rig = new Node3D();
		AddChild(_rig);
		_rig.RotateY(_initialSpin);
		_slab = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(Half * 2, Thickness, Half * 2) },
			Position = new Vector3(0, -Thickness / 2, 0),
			MaterialOverride = Materials.For(Styles.BoardLook(_settings.Board)),
		};
		_rig.AddChild(_slab);

		// Brass pegs at every dot.
		var peg = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.11f, Height = 0.3f, RadialSegments = 16 };
		var brass = Materials.For(Styles.Pieces(Style.Metal).One);
		for (int row = 0; row <= DotsGame.Rows; row++)
		{
			for (int column = 0; column <= DotsGame.Columns; column++)
				_rig.AddChild(new MeshInstance3D { Mesh = peg, MaterialOverride = brass, Position = Dot(row, column) + new Vector3(0, 0.15f, 0) });
		}
		Stage.WarmUp(this, -Thickness, Sparkle.Halo);
	}

	private MeshInstance3D MakeStick(int line, Player drawer)
	{
		bool across = DotsGame.IsHorizontal(line);
		var size = across ? new Vector3(Cell - 0.22f, 0.09f, 0.1f) : new Vector3(0.1f, 0.09f, Cell - 0.22f);
		var stick = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = Materials.For(Colours.LookOf(drawer == Player.First)),
			Position = LineCenter(line) + new Vector3(0, 0.12f, 0),
		};
		_rig.AddChild(stick);
		_sticks[line] = stick;
		return stick;
	}

	private MeshInstance3D MakeTile(int box, Player owner)
	{
		var tile = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(Cell - 0.34f, 0.06f, Cell - 0.34f) },
			MaterialOverride = Materials.For(Colours.LookOf(owner == Player.First)),
			Position = BoxCenter(box) + new Vector3(0, 0.03f, 0),
		};
		_rig.AddChild(tile);
		_tiles[box] = tile;
		return tile;
	}

	// ---------------------------------------------------------------- Input

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_hud.MenuOpen)
			_spin.HandleInput(@event);
	}

	private void OnTapped(Vector2 screen)
	{
		if (_busy || IsComputerTurn || _game.IsOver)
			return;
		var from = _camera.ProjectRayOrigin(screen);
		var direction = _camera.ProjectRayNormal(screen);
		if (direction.Y > -0.01f)
			return;
		var local = _rig.ToLocal(from + direction * ((0.1f - from.Y) / direction.Y));
		local.Y = 0;
		// The nearest open line, if the tap is close enough to it.
		int best = -1;
		float bestDistance = 0.55f;
		foreach (int line in _game.OpenLines())
		{
			float distance = (LineCenter(line) - local).Length();
			if (distance < bestDistance)
			{
				best = line;
				bestDistance = distance;
			}
		}
		if (best >= 0)
			_ = PlayLine(best);
	}

	// ---------------------------------------------------------------- Turns

	private async Task PlayLine(int line)
	{
		_busy = true;
		_spin.Stop();
		_hud.HideHint();
		var result = _game.Draw(line);
		DevLog.Write($"{result.Drawer} draws {line}, boxes {result.Boxes.Count}, again={result.GoesAgain}, over={result.GameOver}");

		var stick = MakeStick(line, result.Drawer);
		var rest = stick.Position;
		stick.Position = rest + new Vector3(0, 1.2f, 0);
		var drop = CreateTween();
		drop.TweenProperty(stick, "position", rest, 0.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		await ToSignal(drop, Tween.SignalName.Finished);
		Clack(stick.GlobalPosition, 0.7f);

		if (result.Boxes.Count == 0)
			_chain = 0;
		foreach (int box in result.Boxes)
			await PopTile(box, result.Drawer);

		if (_game.IsOver && !_roundCounted)
		{
			_roundCounted = true;
			if (_game.Winner is { } winner)
				_wins.Win(first: winner == Player.First);
		}
		_busy = false;
		RefreshUi(result.GoesAgain ? result.Drawer : null);
	}

	/// <summary>
	/// The signature: a tile in the closer's colour springs up into the finished box, flashing, with
	/// a spray of sparks. Each box in a chain rings a little higher than the one before.
	/// </summary>
	private async Task PopTile(int box, Player owner)
	{
		var tile = MakeTile(box, owner);
		tile.Scale = new Vector3(0.2f, 1f, 0.2f);
		tile.MaterialOverlay = Sparkle.Halo;
		var rest = tile.Position;
		tile.Position = rest + new Vector3(0, -0.2f, 0);
		var pop = CreateTween().SetParallel();
		pop.TweenProperty(tile, "scale", Vector3.One, 0.28f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		pop.TweenProperty(tile, "position", rest, 0.28f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		Sparkle.Burst(_rig, rest + new Vector3(0, 0.2f, 0));
		float pitch = 1f + Mathf.Min(_chain, 10) * 0.07f;
		_sound.Play(_impacts.Random(_settings.Pieces), tile.GlobalPosition, Styles.GainDb(_settings.Pieces) + 2f, pitch);
		_chain++;
		_hud.SetStatus(_chain > 1 ? $"Box! ×{_chain}" : "Box!", Colours.UiColorOf(owner == Player.First));
		await Wait(0.22f);
		GetTree().CreateTimer(0.3).Timeout += () => tile.MaterialOverlay = null;
	}

	private void Clack(Vector3 at, float strength)
	{
		_sound.PlayImpact(_impacts.Random(_settings.Pieces), at, strength, Styles.GainDb(_settings.Pieces));
		_sound.PlayImpact(_impacts.Random(_settings.Board), at, strength * 0.6f, Styles.GainDb(_settings.Board));
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		_spin.Process(dt);
		if (_busy || !IsComputerTurn)
			return;
		if (!_computer.Thinking)
		{
			var player = new ComputerPlayer(_demo ? Difficulty.Medium : _settings.Difficulty);
			var snapshot = _game.Clone();
			// Quicker between boxes in a chain, so a long chain flows.
			_computer.MinThinkTime = _demo ? 0.15f : _chain > 0 ? 0.3f : 0.7f;
			_computer.Start(() => player.ChooseLine(snapshot), $"computer ({player.Difficulty})");
			RefreshUi();
			return;
		}
		if (_computer.Poll(dt) is { } line && !_game.IsDrawn(line))
			_ = PlayLine(line);
	}

	// ---------------------------------------------------------------- Games, undo, ads

	private void OnNewGamePressed()
	{
		if (_busy)
			return;
		bool adDue = _adSchedule!.GameFinished();
		_settings.GamesSinceAd = _adSchedule.GamesSinceAd;
		_settings.Save();
		if (adDue)
		{
			DevLog.Write("ad: between-games interstitial");
			_ads.ShowInterstitial(() => _ = StartGame());
		}
		else
		{
			_ = StartGame();
		}
	}

	private async Task StartGame()
	{
		_computer.Cancel();
		_busy = true;
		_roundCounted = false;
		_chain = 0;
		_game.Reset();
		Rebuild(animate: true);
		await Wait(0.3f);
		_busy = false;
		RefreshUi();
	}

	/// <summary>Removes every stick and tile and puts back the ones the rules have (after undo or a colour change).</summary>
	private void Rebuild(bool animate)
	{
		foreach (var node in _sticks.Concat(_tiles))
		{
			if (node == null)
				continue;
			if (!animate)
			{
				node.QueueFree();
				continue;
			}
			var shrink = CreateTween();
			shrink.TweenProperty(node, "scale", Vector3.One * 0.01f, 0.2f);
			shrink.TweenCallback(Callable.From(node.QueueFree));
		}
		System.Array.Clear(_sticks);
		System.Array.Clear(_tiles);
		// Line owners aren't kept by the rules, so replayed sticks take the colour of the box they
		// closed, or stay neutral (the first colour) otherwise.
		foreach (int line in Enumerable.Range(0, DotsGame.Lines).Where(_game.IsDrawn))
			MakeStick(line, Player.First);
		for (int box = 0; box < DotsGame.Boxes; box++)
		{
			if (_game.Owner(box) is { } owner)
				MakeTile(box, owner);
		}
	}

	private bool CanUndo => VsComputer && !_demo && !_busy && _game.MovesMade > 0 && (_game.IsOver || _game.Turn == Human);

	private void OnUndoPressed()
	{
		if (!CanUndo)
			return;
		DevLog.Write("ad: rewarded (undo)");
		_hud.SetUndoVisible(false);
		_ads.ShowRewarded(rewarded: UndoLastTurn, closed: () => RefreshUi());
	}

	/// <summary>Takes back the computer's lines and then your whole last turn (extra lines included).</summary>
	private void UndoLastTurn()
	{
		_computer.Cancel();
		if (_game.IsOver && _roundCounted && _game.Winner is { } winner)
			_wins.Unwin(first: winner == Player.First);
		_roundCounted = false;
		while (_game.LastMover == DotsGame.Opponent(Human))
			_game.Undo();
		while (_game.LastMover == Human)
			_game.Undo();
		_chain = 0;
		Rebuild(animate: false);
		RefreshUi();
	}

	// ---------------------------------------------------------------- UI

	private string NameOf(Player player) => VsComputer && !_demo
		? (player == Human ? "You" : "Computer")
		: Colours.NameOf(player == Player.First);

	private Color ColorOf(Player player) => Colours.UiColorOf(player == Player.First);

	private void RefreshUi(Player? goesAgain = null)
	{
		_hud.SetScore($"{NameOf(Player.First)}  {_game.Score(Player.First)}   —   {_game.Score(Player.Second)}  {NameOf(Player.Second)}");
		_hud.SetNewGameVisible(_game.IsOver && !_busy);
		_hud.SetUndoVisible(CanUndo);
		if (_game.IsOver)
		{
			if (_busy)
				return;
			string wins = $"(wins {_wins.First}–{_wins.Second})";
			string text = _game.Winner switch
			{
				null => $"A draw! {wins}",
				{ } w when NameOf(w) == "You" => $"You win! {wins}",
				{ } w => $"{NameOf(w)} wins {wins}",
			};
			_hud.SetStatus(text, _game.Winner is { } side ? ColorOf(side) : Colors.White);
			if (_demo && _quitAfterDemo)
				GetTree().CreateTimer(1.5).Timeout += () => GetTree().Quit();
			return;
		}
		var turn = _game.Turn;
		string status = goesAgain != null ? (NameOf(turn) == "You" ? "Go again!" : $"{NameOf(turn)} goes again")
			: IsComputerTurn && !_demo ? "Computer is thinking…"
			: NameOf(turn) == "You" ? "Your turn" : $"{NameOf(turn)}'s turn";
		_hud.SetStatus(status, ColorOf(turn));
	}

	private SignalAwaiter Wait(float seconds) => ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	// ---------------------------------------------------------------- Developer flags

	/// <summary>
	/// Developer flags after `--` (or in user://dev_args.txt on a phone): `--log`, `--demo` (the
	/// computer plays both sides), `--quit-after-demo`, `--games-since-ad=4`, `--board=Wood`,
	/// `--pieces=Glass`, `--spin=30` (degrees). Style flags aren't saved.
	/// </summary>
	private void ReadDeveloperArgs()
	{
		foreach (var (name, value) in DevArgs.Read())
		{
			switch (name)
			{
				case "--log":
					DevLog.Enabled = true;
					break;
				case "--demo":
					_demo = true;
					_settings.Muted = false;
					break;
				case "--quit-after-demo":
					_quitAfterDemo = true;
					break;
				case "--games-since-ad":
					_adSchedule = new AdSchedule(int.Parse(value));
					break;
				case "--board" when System.Enum.TryParse(value, out Style board):
					_settings.Board = board;
					break;
				case "--pieces" when System.Enum.TryParse(value, out Style pieces):
					_settings.Pieces = pieces;
					break;
				case "--spin":
					_initialSpin = Mathf.DegToRad(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
					break;
			}
		}
	}
}
