using Godot;
using System;

public partial class World : Node2D
{
	public int PlayersCount;
	public string NameOfFirstOne;
	public string NameOfSecondOne;

	public override void _Ready()
	{
		var ghost1 = GetChild<Ghost12>(1);
		var ghost2 = GetChild<Ghost2>(2);
		ghost1.Name = NameOfFirstOne;
		ghost2.Name = NameOfSecondOne;
		GD.Print(PlayersCount);
		if (PlayersCount == 1) {ghost2.Visible = false;}
	}
		public override void _Process(double delta)
	{
	}
}
