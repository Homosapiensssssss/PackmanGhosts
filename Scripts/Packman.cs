using Godot;
using System;

public partial class Packman : CharacterBody2D
{
	public const float Speed = 300.0f;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;
		
		Velocity = velocity;
		MoveAndSlide();
		Console.WriteLine("[eq]");
	}

}
