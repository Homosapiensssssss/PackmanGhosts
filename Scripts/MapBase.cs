using Godot;
using System;

public partial class MapBase : StaticBody2D
{

	public override void _Ready()
	{
		AddToGroup("walls");
	}

	public override void _Process(double delta)
	{
	}
}
