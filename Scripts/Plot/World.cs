using Godot;
using System;

public partial class World : Node2D
{
	public int PlayersCount;
	public string NameOfFirstOne;
	public string NameOfSecondOne;

	public override void _Ready()
	{
		var mapGen = GetNode<MapGenerator>("MapGenerator");
		var pacman = GetNode<Pacman>("Pacman");
		var ghost1 = GetNode<Ghost1>("Ghost1");
		var ghost2 = GetNode<Ghost2>("Ghost2");

		pacman.GlobalPosition = mapGen.PacmanSpawn;
		ghost1.GlobalPosition = mapGen.Ghost1Spawn;
		ghost2.GlobalPosition = mapGen.Ghost2Spawn;

		GD.Print(PlayersCount);
		if (PlayersCount == 1)
		{
			ghost2.Visible = false;
		}
	}

	public override void _Process(double delta)
	{
	}
}
