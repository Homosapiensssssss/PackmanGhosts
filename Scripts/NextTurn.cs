using Godot;
using System;

public partial class NextTurn : Area2D
{

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	public override void _Process(double delta)
	{
	}

	public void OnBodyEntered(Node2D body)
	{
		
	}
}
