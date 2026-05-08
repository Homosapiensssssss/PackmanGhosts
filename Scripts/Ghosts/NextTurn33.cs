using Godot;
using System;

public partial class NextTurn33 : Node2D
{	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
	}

	public void _on_area_up_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if( (body is not Ghost3) &&(!ghost.IsUp))
		{
			ghost.IsUp=true;
		}
	}
	public void _on_area_up_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if ((body is not Ghost3) && (ghost.IsUp))
		{
			ghost.IsUp = false;
		}
	}
	
	public void _on_area_down_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if( (body is not Ghost3) &&(!ghost.IsDown))
		{
			ghost.IsDown=true;
		}
	}
	public void _on_area_down_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if ((body is not Ghost3) &&(ghost.IsDown))
		{
			ghost.IsDown= false;
		}
	}
	public void _on_area_right_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if( (body is not Ghost3) &&(!ghost.IsRight))
		{
			ghost.IsRight=true;
		}
	}
	public void _on_area_right_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if ((body is not Ghost3) &&(ghost.IsRight))
		{
			ghost.IsRight = false;
		}
	}
	public void _on_area_left_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if( (body is not Ghost3) &&(!ghost.IsLeft))
		{
			ghost.IsLeft=true;
		}
	}
	public void _on_area_left_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost3>();
		if ((body is not Ghost3) &&(ghost.IsLeft))
		{
			ghost.IsLeft = false;
		}
	}
}
