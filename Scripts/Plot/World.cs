using Godot;
using System;

public partial class World : Node2D
{
	public int PlayersCount;
	public string NameOfFirstOne;
	public string NameOfSecondOne;

	public override void _Ready()
	{
		var ghost2 = GetChild<Ghost2>(2);
		ghost2.Name = "Ghost2";
		GD.Print(PlayersCount);
		if (PlayersCount == 1) {ghost2.Visible = false;}
	}
		public override void _Process(double delta)
	{
	}
}
