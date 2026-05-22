using Godot;
using System;

public partial class BasicMap : Node2D
{
	public const float Speed = 180.0f;
	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
	}
	
	public void _on_tube_left_body_entered(Node2D body)
	{
		if (body is CharacterBody2D CharBody){
    	Vector2 newVelocity = new Vector2(1, 0) * Speed; 
    	CharBody.Velocity = newVelocity;
    	Transform2D rightTrans = body.Transform;
    	rightTrans.Origin = new Vector2(834,355); 
    	body.Transform = rightTrans;}
	}
	public void _on_tube_right_body_entered(Node2D body)
	{
		if (body is CharacterBody2D CharBody){
    	Vector2 newVelocity = new Vector2(-1, 0) * Speed; 
    	CharBody.Velocity = newVelocity;
    	Transform2D leftTrans = body.Transform;
    	leftTrans.Origin = new Vector2(312,355); 
    	body.Transform = leftTrans;
		}
	}
}
