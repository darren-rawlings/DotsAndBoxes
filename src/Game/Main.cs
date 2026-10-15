using GameTemplate.Core;
using GameTemplate.Game.Ui;
using Godot;
using Renegade.Ads;
using Renegade.Audio;
using Renegade.Core;
using Renegade.Core.Synth;
using Renegade.Dev;

namespace GameTemplate.Game;

/// <summary>
/// Starting point for a new game: a lit 3D table, a HUD, sound, ads and developer flags, all
/// wired up. Tap the table to hear a synthesised knock; "New Game" counts a finished game and
/// shows the between-games ad every fifth time. Replace the table and the tap with the real game.
/// </summary>
public partial class Main : Node3D
{
	public const string Title = "Game Template";

	/// <summary>
	/// Google's test ad units until the game's real ones exist in AdMob (see the release playbook
	/// in addons/renegade_core/docs). ForChildren defaults to true: Renegade games are for everyone.
	/// </summary>
	private static readonly AdMobConfig AdUnits = AdMobConfig.Test;

	/// <summary>A wooden knock; swap in the game's own materials. See ImpactRecipe for what each number does.</summary>
	private static readonly ImpactRecipe Knock = new(750, new[] { 1.0, 2.57, 4.2, 6.1 }, new[] { 1.0, 0.6, 0.3, 0.15 }, 0.045, 0.7, 0.006, 0.35, 0.3);

	private readonly ImpactBank<int> _impacts = new(_ => Knock);
	private readonly Scoreboard _score = new();
	private Settings _settings = null!;
	private SoundPool _sound = null!;
	private Hud _hud = null!;
	private IAdService _ads = null!;
	private AdSchedule _adSchedule = null!;
	private Camera3D _camera = null!;

	private bool _demo;
	private bool _quitAfterDemo;
	private float _demoTimer;
	private int _demoGames;

	public override void _Ready()
	{
		DevLog.Tag = "gt";
		_settings = Settings.Load();
		ReadDeveloperArgs();
		AudioServer.SetBusMute(0, _settings.Muted);
		BuildWorld();

		_sound = new SoundPool();
		AddChild(_sound);

		_adSchedule ??= new AdSchedule(_settings.GamesSinceAd);
		_ads = AdServices.Create(this, AdUnits, _demo, new PlaceholderAds { TextColor = Palette.Text });

		_hud = new Hud();
		AddChild(_hud);
		_hud.Build(Title, _settings.Muted);
		_hud.NewGamePressed += FinishGame;
		_hud.MuteToggled += muted =>
		{
			_settings.Muted = muted;
			AudioServer.SetBusMute(0, muted);
			_settings.Save();
		};
		RefreshUi();
	}

	private void BuildWorld()
	{
		_camera = new Camera3D { Position = new Vector3(0, 6, 9), Fov = 50 };
		AddChild(_camera);
		_camera.LookAt(Vector3.Zero);

		AddChild(new DirectionalLight3D { ShadowEnabled = true, RotationDegrees = new Vector3(-55, -30, 0) });
		AddChild(new WorldEnvironment
		{
			Environment = new Godot.Environment
			{
				BackgroundMode = Godot.Environment.BGMode.Color,
				BackgroundColor = new Color("120c0a"),
				AmbientLightSource = Godot.Environment.AmbientSource.Color,
				AmbientLightColor = new Color("806050"),
			},
		});

		var table = new MeshInstance3D
		{
			Mesh = new BoxMesh { Size = new Vector3(8, 0.3f, 8) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("6b3f22"), Roughness = 0.7f },
			Position = new Vector3(0, -0.15f, 0),
		};
		AddChild(table);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
			Knocked(click.Position);
	}

	/// <summary>Plays a knock where the tap hits the table, panned to that side.</summary>
	private void Knocked(Vector2 screen)
	{
		var from = _camera.ProjectRayOrigin(screen);
		var direction = _camera.ProjectRayNormal(screen);
		if (Mathf.Abs(direction.Y) < 0.001f)
			return;
		var hit = from + direction * (-from.Y / direction.Y);
		_sound.PlayImpact(_impacts.Random(0), hit, strength: 1f);
	}

	private void FinishGame()
	{
		_score.Win(first: GD.Randf() < 0.5f);
		_hud.SetStatus("Game over!");
		RefreshUi();
		bool adDue = _adSchedule.GameFinished();
		_settings.GamesSinceAd = _adSchedule.GamesSinceAd;
		_settings.Save();
		if (adDue)
		{
			DevLog.Write("ad: between-games interstitial");
			_ads.ShowInterstitial(() => _hud.SetStatus("New game"));
		}
	}

	private void RefreshUi() => _hud.SetScore($"Red  {_score.First}   —   {_score.Second}  Blue");

	public override void _Process(double delta)
	{
		if (!_demo)
			return;
		// Demo: finish a game every second, so five games show the stand-in ad, then optionally quit.
		_demoTimer += (float)delta;
		if (_demoTimer < 1f)
			return;
		_demoTimer = 0f;
		if (_demoGames++ < 5)
			FinishGame();
		else if (_quitAfterDemo)
			GetTree().Quit();
	}

	/// <summary>
	/// Developer flags after `--` (or in user://dev_args.txt on a phone): `--log`, `--demo`,
	/// `--quit-after-demo`, `--games-since-ad=4`. Add the game's own flags here.
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
			}
		}
	}
}
