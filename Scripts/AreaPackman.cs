using Godot;
using System;
using System.IO;

public partial class AreaPackman : Area2D
{

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
	}
	private void OnBodyEntered(Node2D body)
	{
		if  ( (body is CharacterBody2D) && (body is not Packman ))
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