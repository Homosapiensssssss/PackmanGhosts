using Godot;
using System;

public partial class NextTurn2 : Node2D
{
	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
	}

	public void _on_area_up_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if( (body is not Ghost12) &&(!ghost.IsUp))
		{
			ghost.IsUp=true;
			GD.Print("Up");
		}
	}
	public void _on_area_up_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if ((body is not Ghost12) && (ghost.IsUp))
		{
			ghost.IsUp = false;
			GD.Print("NOTUp");
		}
	}
	
	public void _on_area_down_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if( (body is not Ghost12) &&(!ghost.IsDown))
		{
			ghost.IsDown=true;
			GD.Print("Down");
		}
	}
	public void _on_area_down_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if ((body is not Ghost12) &&(ghost.IsDown))
		{
			ghost.IsDown= false;
			GD.Print("NOTDown");
		}
	}
	public void _on_area_right_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if( (body is not Ghost12) &&(!ghost.IsRight))
		{
			ghost.IsRight=true;
			GD.Print("Right");
		}
	}
	public void _on_area_right_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if ((body is not Ghost12) && (ghost.IsRight))
		{
			ghost.IsRight = false;
			GD.Print("NOTRight");
		}
	}
	public void _on_area_left_body_entered(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if( (body is not Ghost12) &&(!ghost.IsLeft))
		{
			ghost.IsLeft=true;
			GD.Print("Left");
		}
	}
	public void _on_area_left_body_exited(Node2D body)
	{
		var ghost = GetParent<Ghost12>();
		if ((body is not Ghost12) &&(ghost.IsLeft))
		{
			ghost.IsLeft = false;
			GD.Print("NOTLeft");
		}
	}
}
