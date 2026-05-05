using Godot;
using System;

public partial class Start : Node2D
{
	int Players;
	string Name1;
	string Name2;
	public override void _Ready()
	{
	}
	public override void _Process(double delta)
	{
	}
	public void _on_option_button_item_selected(int index)
	{
		if (index == 0) Players = 2;
		if (index == 1) Players = 1;
	}
	public void _on_line_edit_text_changed(string text)
	{
		Name1 = text;
	}
	public void _on_line_edit_2_text_changed(string text)
	{
		Name2 = text;
	}
	 public void _on_button_pressed()
	{
		var Scene = GD.Load<PackedScene>("res://TSCN/world.tscn").Instantiate<World>();
		Scene.PlayersCount = Players;
		Scene.NameOfFirstOne = Name1;
		Scene.NameOfSecondOne = Name2;
		GetTree().Root.AddChild(Scene);
		var OldScene = GetTree().CurrentScene;
		GetTree().CurrentScene = Scene;
		OldScene.QueueFree();
	}
}
