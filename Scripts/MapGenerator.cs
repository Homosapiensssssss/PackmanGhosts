using Godot;
using System;
using System.Collections.Generic;

public partial class MapGenerator : Node2D
{
	[Export] public int CellSize = 32;
	[Export] public PackedScene WallScene;
	[Export] public float WallRemovalRate = 0.35f;

	public int GridWidth;
	public int GridHeight;
	public int[,] Grid;
	public Vector2 PacmanSpawn;
	public Vector2 Ghost1Spawn;
	public Vector2 Ghost2Spawn;

	private Random _rng = new Random();

	public override void _Ready()
	{
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;

		GridWidth  = (int)(screenSize.X / CellSize);
		GridHeight = (int)(screenSize.Y / CellSize);

		if (GridWidth  % 2 == 0) GridWidth--;
		if (GridHeight % 2 == 0) GridHeight--;

		if (WallScene == null)
		{
			GD.PrintErr("WallScene не назначена!");
			return;
		}

		GenerateMap();
	}

	public void GenerateMap()
	{
		Grid = new int[GridWidth, GridHeight];

		for (int x = 0; x < GridWidth; x++)
			for (int y = 0; y < GridHeight; y++)
				Grid[x, y] = 1;

		Grid[1, 1] = 0;
		CarveMaze(1, 1);
		EnforceBorder();
		RemoveExtraWalls();
		SetSpawnPoints();
		SpawnWalls();
	}


	private void CarveMaze(int x, int y)
	{
		var dirs = ShuffledDirections();
		foreach (var (dx, dy) in dirs)
		{
			int nx = x + dx * 2;
			int ny = y + dy * 2;
			if (InBounds(nx, ny) && Grid[nx, ny] == 1)
			{
				Grid[x + dx, y + dy] = 0;
				Grid[nx, ny] = 0;
				CarveMaze(nx, ny);
			}
		}
	}


	private void RemoveExtraWalls()
	{
		var innerWalls = new List<(int x, int y)>();

		for (int x = 1; x < GridWidth - 1; x++)
			for (int y = 1; y < GridHeight - 1; y++)
				if (Grid[x, y] == 1 && CountPassableNeighbors(x, y) >= 2)
					innerWalls.Add((x, y));

		Shuffle(innerWalls);
		int toRemove = (int)(innerWalls.Count * WallRemovalRate);

		int removed = 0;
		foreach (var (x, y) in innerWalls)
		{
			if (removed >= toRemove) break;
			Grid[x, y] = 0;
			removed++;
		}
	}

	private int CountPassableNeighbors(int x, int y)
	{
		int count = 0;
		if (x > 0 && Grid[x - 1, y] == 0) count++;
		if (x < GridWidth  - 1 && Grid[x + 1, y] == 0) count++;
		if (y > 0 && Grid[x, y - 1] == 0) count++;
		if (y < GridHeight - 1 && Grid[x, y + 1] == 0) count++;
		return count;
	}


	private void SetSpawnPoints()
	{
		Vector2I pacmanCell = FindNearestPassable(GridWidth / 2, GridHeight / 2);
		Vector2I ghost1Cell = FindNearestPassable(1, 1);
		Vector2I ghost2Cell = FindNearestPassable(GridWidth - 2, 1);

		Grid[pacmanCell.X, pacmanCell.Y] = 0;
		Grid[ghost1Cell.X, ghost1Cell.Y] = 0;
		Grid[ghost2Cell.X, ghost2Cell.Y] = 0;

		ClearAreaAround(pacmanCell.X, pacmanCell.Y, 1);

		PacmanSpawn = GridToWorld(pacmanCell.X, pacmanCell.Y);
		Ghost1Spawn = GridToWorld(ghost1Cell.X, ghost1Cell.Y);
		Ghost2Spawn = GridToWorld(ghost2Cell.X, ghost2Cell.Y);
	}

	public Vector2 GetRandomSpawn(List<Vector2I> excludeCells = null)
	{
		var freeCells = new List<Vector2I>();

		for (int x = 1; x < GridWidth - 1; x++)
			for (int y = 1; y < GridHeight - 1; y++)
				if (Grid[x, y] == 0)
					freeCells.Add(new Vector2I(x, y));

		if (excludeCells != null)
			foreach (var exc in excludeCells)
				freeCells.Remove(exc);

		if (freeCells.Count == 0)
			return GridToWorld(1, 1);

		var chosen = freeCells[_rng.Next(freeCells.Count)];
		return GridToWorld(chosen.X, chosen.Y);
	}

	private Vector2I FindNearestPassable(int startX, int startY)
	{
		var queue   = new Queue<Vector2I>();
		var visited = new HashSet<Vector2I>();

		var start = new Vector2I(startX, startY);
		queue.Enqueue(start);
		visited.Add(start);

		while (queue.Count > 0)
		{
			var cell = queue.Dequeue();

			if (Grid[cell.X, cell.Y] == 0)
				return cell;

			int[] dx = { 0, 0, -1, 1 };
			int[] dy = { -1, 1,  0, 0 };

			for (int i = 0; i < 4; i++)
			{
				var next = new Vector2I(cell.X + dx[i], cell.Y + dy[i]);
				if (next.X > 0 && next.X < GridWidth  - 1 &&
					next.Y > 0 && next.Y < GridHeight - 1 &&
					!visited.Contains(next))
				{
					visited.Add(next);
					queue.Enqueue(next);
				}
			}
		}

		Grid[startX, startY] = 0;
		return new Vector2I(startX, startY);
	}

	private void ClearAreaAround(int cx, int cy, int radius)
	{
		for (int x = cx - radius; x <= cx + radius; x++)
			for (int y = cy - radius; y <= cy + radius; y++)
				if (x > 0 && x < GridWidth - 1 && y > 0 && y < GridHeight - 1)
					Grid[x, y] = 0;
	}


	private void SpawnWalls()
	{
		for (int x = 0; x < GridWidth; x++)
			for (int y = 0; y < GridHeight; y++)
			{
				if (Grid[x, y] != 1) continue;
				var wall = WallScene.Instantiate<Node2D>();
				wall.GlobalPosition = GridToWorld(x, y);
				AddChild(wall);
			}
	}


	public Vector2 GridToWorld(int x, int y)
		=> new Vector2(x * CellSize + CellSize / 2f, y * CellSize + CellSize / 2f);

	public Vector2I WorldToGrid(Vector2 worldPos)
	{
		int x = Mathf.FloorToInt(worldPos.X / CellSize);
		int y = Mathf.FloorToInt(worldPos.Y / CellSize);
		x = Mathf.Clamp(x, 0, GridWidth - 1);
		y = Mathf.Clamp(y, 0, GridHeight - 1);
		return new Vector2I(x, y);
	}

	private List<(int, int)> ShuffledDirections()
	{
		var dirs = new List<(int, int)> { (0, -1), (0, 1), (-1, 0), (1, 0) };
		for (int i = dirs.Count - 1; i > 0; i--)
		{
			int j = _rng.Next(i + 1);
			(dirs[i], dirs[j]) = (dirs[j], dirs[i]);
		}
		return dirs;
	}

	private void Shuffle<T>(List<T> list)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = _rng.Next(i + 1);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}

	private bool InBounds(int x, int y)
		=> x > 0 && y > 0 && x < GridWidth - 1 && y < GridHeight - 1;

	private void EnforceBorder()
	{
		for (int x = 0; x < GridWidth; x++)
		{
			Grid[x, 0] = 1;
			Grid[x, GridHeight - 1] = 1;
		}
		for (int y = 0; y < GridHeight; y++)
		{
			Grid[0, y] = 1;
			Grid[GridWidth - 1, y] = 1;
		}
	}
}
