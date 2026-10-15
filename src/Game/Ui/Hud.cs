using System;
using Godot;

namespace GameTemplate.Game.Ui;

/// <summary>
/// The on-screen layer: title, score, status, and the buttons. Sized for a 720×1280 portrait
/// phone; anchors keep it in place on taller and wider screens.
/// </summary>
public partial class Hud : CanvasLayer
{
	public event Action? NewGamePressed;
	public event Action<bool>? MuteToggled;

	private Label _score = null!;
	private Label _status = null!;
	private Button _mute = null!;

	public void Build(string title, bool muted)
	{
		var heading = MakeLabel(title.ToUpperInvariant(), 60, Palette.Text);
		Anchor(heading, Control.LayoutPreset.TopWide, top: 60, bottom: 140);
		AddChild(heading);

		_score = MakeLabel("", 30, Palette.Muted);
		Anchor(_score, Control.LayoutPreset.TopWide, top: 140, bottom: 190);
		AddChild(_score);

		_status = MakeLabel("", 42, Palette.Text);
		Anchor(_status, Control.LayoutPreset.BottomWide, top: -335, bottom: -275);
		AddChild(_status);

		var newGame = MakeButton("New Game", 34, Palette.Accent);
		Anchor(newGame, Control.LayoutPreset.CenterBottom, left: -190, right: 190, top: -255, bottom: -175);
		newGame.Pressed += () => NewGamePressed?.Invoke();
		AddChild(newGame);

		_mute = MakeButton("", 24, Palette.Button);
		Anchor(_mute, Control.LayoutPreset.BottomRight, left: -170, right: -20, top: -118, bottom: -58);
		_mute.Pressed += () =>
		{
			muted = !muted;
			SetMuteText(muted);
			MuteToggled?.Invoke(muted);
		};
		SetMuteText(muted);
		AddChild(_mute);
	}

	public void SetScore(string text) => _score.Text = text;

	public void SetStatus(string text) => _status.Text = text;

	private void SetMuteText(bool muted) => _mute.Text = muted ? "Sound: Off" : "Sound: On";

	private static Label MakeLabel(string text, int fontSize, Color color)
	{
		var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		// A soft shadow and outline keep text readable over any 3D scene.
		label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
		label.AddThemeConstantOverride("shadow_offset_y", 3);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.55f));
		label.AddThemeConstantOverride("outline_size", 8);
		return label;
	}

	private static Button MakeButton(string text, int fontSize, Color color)
	{
		var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
		button.AddThemeFontSizeOverride("font_size", fontSize);
		foreach (var (state, shade) in new[] { ("normal", color), ("hover", color.Lightened(0.12f)), ("pressed", color.Darkened(0.2f)), ("hover_pressed", color.Darkened(0.1f)) })
		{
			var box = new StyleBoxFlat { BgColor = shade, AntiAliasing = true };
			box.SetCornerRadiusAll(32);
			box.SetContentMarginAll(12);
			button.AddThemeStyleboxOverride(state, box);
		}
		return button;
	}

	private static void Anchor(Control control, Control.LayoutPreset preset, float left = 0, float right = 0, float top = 0, float bottom = 0)
	{
		control.SetAnchorsPreset(preset);
		control.OffsetLeft = left;
		control.OffsetRight = right;
		control.OffsetTop = top;
		control.OffsetBottom = bottom;
	}
}
