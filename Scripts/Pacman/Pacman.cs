using Godot;
using System;

public partial class Pacman : CharacterBody2D
{
	public override void _Process(double delta)
	{
		
	}
	private void OnBodyEntered(Node2D body)
	{
		if  ((body is not Pacman ) && (body is CharacterBody2D))
		{
			var Scene = GD.Load<PackedScene>("res://TSCN/End.tscn").Instantiate<End>();
			Scene.WinnerName = body.Name;
			GetTree().Root.AddChild(Scene);
			var OldScene = GetTree().CurrentScene;
			GetTree().CurrentScene = Scene;
			OldScene.QueueFree();
		}
	}
}
