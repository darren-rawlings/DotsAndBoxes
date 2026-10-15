using Godot;

namespace GameTemplate.Game;

/// <summary>Player preferences, saved on the device only (the privacy policy promises nothing leaves it).</summary>
public sealed class Settings
{
	private const string Path = "user://settings.cfg";
	private const string Section = "settings";

	public bool Muted { get; set; }

	/// <summary>Finished games since the last between-games ad; see <see cref="Renegade.Core.AdSchedule"/>.</summary>
	public int GamesSinceAd { get; set; }

	public static Settings Load()
	{
		var settings = new Settings();
		var file = new ConfigFile();
		if (file.Load(Path) != Error.Ok)
			return settings;
		settings.Muted = file.GetValue(Section, "muted", false).AsBool();
		settings.GamesSinceAd = file.GetValue(Section, "games_since_ad", 0).AsInt32();
		return settings;
	}

	public void Save()
	{
		var file = new ConfigFile();
		file.SetValue(Section, "muted", Muted);
		file.SetValue(Section, "games_since_ad", GamesSinceAd);
		file.Save(Path);
	}
}
