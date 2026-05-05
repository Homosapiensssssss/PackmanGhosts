using Godot;
using System;
using System.Dynamic;

public partial class End : Node2D
{
	public string WinnerName = "you" ;
	public override void _Ready()
	{
		GetNode<Label>("CongratsText").Text = "Game Over. "+ WinnerName+" has won. Congratulations.";
	}

	public override void _Process(double delta)
	{
	}

	
}
