using Godot;
using System;
using System.Collections.Generic;

public partial class Pacman : CharacterBody2D
{
	public const float Speed = 180.0f;
	public Vector2 direction;
	public bool IsUp = false;
	public bool IsDown = false;
	public bool IsRight = false;
	public bool IsLeft = false;

	// радиус угроз для пакмана
	[Export] public float DetectionRadius = 300.0f;
	
	// минимальное расстояние для реакции
	[Export] public float ReactDistance = 150.0f;

	private static readonly Vector2[] Directions = new[]
	{
		Vector2.Up,
		Vector2.Down,
		Vector2.Left,
		Vector2.Right
	};

	public override void _Ready()
	{
		// AddToGroup("threats");
	}

	Vector2 Bufer = Vector2.Zero;

	public override void _PhysicsProcess(double delta)
	{
		Vector2 velocity = Velocity;

		// лучшее направлоение для побега
		Vector2 fleeDir = ComputeFleeDirection();

		if (fleeDir != Vector2.Zero)
		{
			Bufer = fleeDir;
		}

		if (IsFree(Bufer)) { direction = Bufer; }

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

	// Находит оптимальное направление побега от всех угроз
	// Возвращает Vector2.Zero если угроз нет рядом
	private Vector2 ComputeFleeDirection()
	{
		Vector2 fleeVector = Vector2.Zero;
		bool hasThreat = false;

		var threats = GetTree().GetNodesInGroup("threats");

		foreach (var node in threats)
		{
			if (node is not Node2D threat || node == this)
				continue;

			Vector2 toThreat = threat.GlobalPosition - GlobalPosition;
			float dist = toThreat.Length();

			// реагрует только на угрозы в радиусе обнаружения
			if (dist < DetectionRadius && dist > 0.01f)
			{
				// чем ближе угроза - тем сильнее отталкивание
				float weight = 1.0f / dist;
				fleeVector -= toThreat.Normalized() * weight;
				hasThreat = true;
			}
		}

		if (!hasThreat)
			return Vector2.Zero;

		// из 4 направлений то, кот наиболее совпадает с вектором побега
		return BestAvailableDirection(fleeVector.Normalized());
	}

	/// Из доступных направлений выбирает наиболее близкое к желаемому вектору
	private Vector2 BestAvailableDirection(Vector2 desired)
	{
		Vector2 best = Vector2.Zero;
		float bestDot = float.NegativeInfinity;

		foreach (var dir in Directions)
		{
			if (!IsFree(dir)) continue; // направление заблокировано стеной

			float dot = desired.Dot(dir);
			if (dot > bestDot)
			{
				bestDot = dot;
				best = dir;
			}
		}

		return best;
	}

	public bool IsFree(Vector2 input)
	{
		if (input == Vector2.Up)    return !IsUp;
		if (input == Vector2.Down)  return !IsDown;
		if (input == Vector2.Right) return !IsRight;
		if (input == Vector2.Left)  return !IsLeft;
		return false;
	}

	private void _on_area_2d_body_entered(Node2D body)
	{
		if ((body is CharacterBody2D) && (body is not Pacman))
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
