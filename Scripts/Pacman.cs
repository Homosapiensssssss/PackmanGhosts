using Godot;
using System;
using System.Runtime.CompilerServices;

public partial class Pacman : CharacterBody2D
{

	public const float Speed = 180.0f;
	public Vector2 direction;
	public bool IsUp = false;
	public bool IsDown = false;
	public bool IsRight = false;
	public bool IsLeft = false;

	public override void _Ready()
	{
		AddToGroup("threats");
	}
	
	Vector2 Bufer = Vector2.Zero ;
	public override void _PhysicsProcess(double delta)
	{
		
		Vector2 velocity = Velocity;
		//var input = Input.GetVector("PacmanLeft", "PacmanRight", "PacmanUp", "PacmanDown");
		//if (input != Vector2.Zero) {Bufer = input;}
		if (IsFree(Bufer)){ direction = Bufer;}
	
		if (direction != Vector2.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Y = direction.Y * Speed;

		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Y = Mathf.MoveToward(Velocity.Y, 0, Speed);
			
		}

		Velocity = velocity;
		MoveAndSlide();
	}

	public bool IsFree(Vector2 input)
	{
		if (input == Vector2.Up){ return !IsUp; }
		if (input == Vector2.Down){ return !IsDown; }
		if (input == Vector2.Right){ return !IsRight; }
		if (input == Vector2.Left){ return !IsLeft; }
		return false;
	}

private void _on_area_2d_body_entered(Node2D body)
	{
		if  ( (body is CharacterBody2D) && (body is not Pacman ))
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