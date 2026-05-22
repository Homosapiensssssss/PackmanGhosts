using Godot;
using System;
using System.Collections.Generic;

public partial class Pacman : CharacterBody2D
{
	[Export] public float Speed = 200.0f;
	[Export] public float DetectionRadius = 500.0f;
	[Export] public float RandomWeight = 0.05f;
	[Export] public float FleeWeight = 5.0f;
	[Export] public float CorridorWeight = 0.3f;

	private Vector2 _moveDir = Vector2.Zero;
	private Vector2I _fromCell;
	private Vector2I _toCell;
	private bool _needsDecision = true;

	private MapGenerator _map;
	private int _cellSize;
	private Random _rng = new Random();

	private static readonly Vector2[] CardinalDirs = {
		Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right
	};

	// Замените _Ready() в Pacman.cs временно на это:
	public override void _Ready()
	{
		_map = FindMapGenerator();
		if (_map == null)
		{
			GD.PrintErr("Pacman: MapGenerator не найден!");
			return;
		}

		Scale = Vector2.One;
		_cellSize = _map.CellSize;
		AddToGroup("pacman");

		GD.Print($"=== DIAGNOSTICS v2 ===");
		GD.Print($"Pacman GlobalPosition BEFORE snap: {GlobalPosition}");
		
		// Проверяем реальные позиции стен
		int wallCount = 0;
		foreach (var child in _map.GetChildren())
		{
			if (child is Node2D wall && wallCount < 5)
			{
				GD.Print($"Wall[{wallCount}] GlobalPos: {wall.GlobalPosition}, LocalPos: {wall.Position}");
				wallCount++;
			}
		}
		
		// Проверяем что GridToWorld выдаёт vs реальная позиция стены
		// Находим стену в клетке (0,0) — это левый верхний угол, точно стена
		Vector2 expectedPos = _map.GridToWorld(0, 0);
		GD.Print($"GridToWorld(0,0) = {expectedPos}");
		GD.Print($"GridToWorld(1,0) = {_map.GridToWorld(1, 0)}");
		GD.Print($"GridToWorld(2,0) = {_map.GridToWorld(2, 0)}");
		
		// Проверяем все трансформации вверх по дереву
		Node current = this;
		while (current != null)
		{
			if (current is Node2D n2d)
				GD.Print($"Node '{n2d.Name}': Pos={n2d.Position}, Scale={n2d.Scale}, GlobalPos={n2d.GlobalPosition}");
			current = current.GetParent();
		}
		
		// Проверяем трансформации MapGenerator
		Node mapCurrent = _map;
		while (mapCurrent != null)
		{
			if (mapCurrent is Node2D mn2d)
				GD.Print($"Map chain '{mn2d.Name}': Pos={mn2d.Position}, Scale={mn2d.Scale}");
			mapCurrent = mapCurrent.GetParent();
		}
		
		GD.Print($"=== END DIAGNOSTICS v2 ===");

		_fromCell = WorldToGrid(GlobalPosition);
		_toCell = _fromCell;
		GlobalPosition = GridToWorld(_fromCell);
		_needsDecision = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_map == null) return;

		if (_needsDecision)
		{
			ChooseNextCell();
			if (_moveDir == Vector2.Zero)
				return;
		}

		Vector2 target = GridToWorld(_toCell);
		Vector2 pos = GlobalPosition;
		float step = Speed * (float)delta;

		float remaining = RemainingDistance(pos, target);

		if (remaining <= step)
		{
			GlobalPosition = target;
			_fromCell = _toCell;
			_needsDecision = true;
		}
		else
		{
			GlobalPosition = pos + _moveDir * step;
			ClampPerpendicular();
		}

		Velocity = _moveDir * Speed;

		CheckGhostCollision();
	}

	private Vector2 GridToWorld(Vector2I cell)
	{
		return new Vector2(cell.X * _cellSize + _cellSize * 0.5f,
						   cell.Y * _cellSize + _cellSize * 0.5f);
	}

	private Vector2I WorldToGrid(Vector2 worldPos)
	{
		int x = Mathf.FloorToInt(worldPos.X / _cellSize);
		int y = Mathf.FloorToInt(worldPos.Y / _cellSize);
		x = Mathf.Clamp(x, 0, _map.GridWidth - 1);
		y = Mathf.Clamp(y, 0, _map.GridHeight - 1);
		return new Vector2I(x, y);
	}


	private bool IsCellPassable(int x, int y)
	{
		if (x < 0 || x >= _map.GridWidth || y < 0 || y >= _map.GridHeight)
			return false;
		return _map.Grid[x, y] == 0;
	}

	private bool IsCellFree(Vector2I cell, Vector2 dir)
	{
		int tx = cell.X + (int)dir.X;
		int ty = cell.Y + (int)dir.Y;
		return IsCellPassable(tx, ty);
	}

	private List<Vector2> GetFreeDirections(Vector2I cell)
	{
		var free = new List<Vector2>();
		foreach (var dir in CardinalDirs)
		{
			if (IsCellFree(cell, dir))
				free.Add(dir);
		}
		return free;
	}


	private float RemainingDistance(Vector2 pos, Vector2 target)
	{
		if (_moveDir.X != 0)
			return Mathf.Abs(target.X - pos.X);
		if (_moveDir.Y != 0)
			return Mathf.Abs(target.Y - pos.Y);
		return pos.DistanceTo(target);
	}

	private void ClampPerpendicular()
	{
		Vector2 lineStart = GridToWorld(_fromCell);

		if (_moveDir.X != 0)
			GlobalPosition = new Vector2(GlobalPosition.X, lineStart.Y);
		else if (_moveDir.Y != 0)
			GlobalPosition = new Vector2(lineStart.X, GlobalPosition.Y);
	}


	private void ChooseNextCell()
	{
		_needsDecision = false;

		var freeDirs = GetFreeDirections(_fromCell);

		if (freeDirs.Count == 0)
		{
			GD.Print($"Pacman STUCK at {_fromCell}! No free dirs.");
			_moveDir = Vector2.Zero;
			_needsDecision = true;
			return;
		}

		Vector2 chosenDir;

		Vector2 fleeDir = ComputeFleeDirection(_fromCell, freeDirs);
		if (fleeDir != Vector2.Zero)
		{
			chosenDir = fleeDir;
		}
		else
		{
			chosenDir = PickWanderDirection(_fromCell, freeDirs);
		}

		if (chosenDir == Vector2.Zero)
		{
			_moveDir = Vector2.Zero;
			_needsDecision = true;
			return;
		}

		_moveDir = chosenDir;
		_toCell = _fromCell + new Vector2I((int)chosenDir.X, (int)chosenDir.Y);

		if (!IsCellPassable(_toCell.X, _toCell.Y))
		{
			GD.PrintErr($"BUG: Chose wall cell {_toCell}! Dir={chosenDir}, From={_fromCell}");
			_moveDir = Vector2.Zero;
			_needsDecision = true;
		}
	}


	private List<Node2D> GatherThreats()
	{
		var threats = new List<Node2D>();
		var found = new HashSet<ulong>();

		foreach (var node in GetTree().GetNodesInGroup("threats"))
		{
			if (node is Node2D n && n != this && n is not Pacman && n.Visible)
			{
				if (found.Add(n.GetInstanceId()))
					threats.Add(n);
			}
		}

		foreach (var node in GetTree().GetNodesInGroup("ghosts"))
		{
			if (node is Node2D n && n != this && n is not Pacman && n.Visible)
			{
				if (found.Add(n.GetInstanceId()))
					threats.Add(n);
			}
		}

		var parent = GetParent();
		if (parent != null)
		{
			foreach (var child in parent.GetChildren())
			{
				if (child is CharacterBody2D body && body != this && child is not Pacman && body.Visible)
				{
					if (found.Add(body.GetInstanceId()))
						threats.Add(body);
				}
			}
		}

		return threats;
	}


	private Vector2 ComputeFleeDirection(Vector2I cell, List<Vector2> freeDirs)
	{
		Vector2 fleeVector = Vector2.Zero;
		bool hasThreat = false;

		var threats = GatherThreats();

		if (threats.Count == 0)
			return Vector2.Zero;

		foreach (var threat in threats)
		{
			Vector2 toThreat = threat.GlobalPosition - GlobalPosition;
			float dist = toThreat.Length();

			if (dist < DetectionRadius && dist > 1f)
			{
				float weight = 1.0f / (dist * dist);
				fleeVector -= toThreat.Normalized() * weight;
				hasThreat = true;
			}
		}

		if (!hasThreat)
			return Vector2.Zero;

		return PickFleeDirection(fleeVector.Normalized(), cell, freeDirs);
	}

	private Vector2 PickFleeDirection(Vector2 desired, Vector2I cell, List<Vector2> freeDirs)
	{
		var options = new List<Vector2>(freeDirs);

		if (options.Count > 1 && _moveDir != Vector2.Zero)
			options.RemoveAll(d => d == -_moveDir);
		if (options.Count == 0)
			options = new List<Vector2>(freeDirs);

		var threatCells = new List<Vector2I>();
		var threats = GatherThreats();
		foreach (var t in threats)
		{
			float dist = GlobalPosition.DistanceTo(t.GlobalPosition);
			if (dist < DetectionRadius)
				threatCells.Add(WorldToGrid(t.GlobalPosition));
		}

		float minRaw = float.MaxValue;
		var rawScores = new List<(Vector2 dir, float raw)>();

		foreach (var dir in options)
		{
			float dot = desired.Dot(dir);
			int corridor = CountFreeCellsAhead(cell, dir, 8);

			Vector2I targetCell = cell + new Vector2I((int)dir.X, (int)dir.Y);
			float bfsScore = 0f;
			if (threatCells.Count > 0)
			{
				int minBfsDist = int.MaxValue;
				foreach (var tc in threatCells)
				{
					int d = BfsDistance(targetCell, tc, 20);
					if (d < minBfsDist) minBfsDist = d;
				}
				bfsScore = minBfsDist == int.MaxValue ? 20f : minBfsDist;
			}

			float raw = dot * FleeWeight + corridor * CorridorWeight + bfsScore * 0.5f;
			rawScores.Add((dir, raw));
			if (raw < minRaw) minRaw = raw;
		}

		var scored = new List<(Vector2 dir, float w)>();
		float totalW = 0f;

		foreach (var (dir, raw) in rawScores)
		{
			float w = raw - minRaw + 1f;
			w *= w;
			w += (float)_rng.NextDouble() * RandomWeight;
			scored.Add((dir, w));
			totalW += w;
		}

		return Roulette(scored, totalW);
	}

	private int BfsDistance(Vector2I from, Vector2I to, int maxDepth)
	{
		if (from == to) return 0;

		var queue = new Queue<(Vector2I cell, int dist)>();
		var visited = new HashSet<Vector2I>();

		queue.Enqueue((from, 0));
		visited.Add(from);

		int[] dx = { 0, 0, -1, 1 };
		int[] dy = { -1, 1, 0, 0 };

		while (queue.Count > 0)
		{
			var (cell, dist) = queue.Dequeue();

			if (dist >= maxDepth)
				continue;

			for (int i = 0; i < 4; i++)
			{
				var next = new Vector2I(cell.X + dx[i], cell.Y + dy[i]);

				if (next == to)
					return dist + 1;

				if (!IsCellPassable(next.X, next.Y))
					continue;

				if (visited.Contains(next))
					continue;

				visited.Add(next);
				queue.Enqueue((next, dist + 1));
			}
		}

		return int.MaxValue;
	}


	private Vector2 PickWanderDirection(Vector2I cell, List<Vector2> freeDirs)
	{
		var options = new List<Vector2>(freeDirs);

		if (options.Count > 1 && _moveDir != Vector2.Zero)
			options.RemoveAll(d => d == -_moveDir);
		if (options.Count == 0)
			options = new List<Vector2>(freeDirs);

		bool canContinue = _moveDir != Vector2.Zero && options.Contains(_moveDir);
		bool isIntersection = freeDirs.Count >= 3;

		if (canContinue && !isIntersection)
			return _moveDir;

		if (canContinue && isIntersection && _rng.NextDouble() > 0.7)
			return _moveDir;

		var scored = new List<(Vector2 dir, float w)>();
		float totalW = 0f;

		foreach (var dir in options)
		{
			int corridor = CountFreeCellsAhead(cell, dir, 5);
			float w = 1f + corridor * 0.5f + (float)_rng.NextDouble() * 0.8f;
			scored.Add((dir, w));
			totalW += w;
		}

		return Roulette(scored, totalW);
	}

	private int CountFreeCellsAhead(Vector2I start, Vector2 dir, int maxDepth)
	{
		int dx = (int)dir.X, dy = (int)dir.Y;
		int cx = start.X, cy = start.Y;
		int count = 0;

		for (int i = 0; i < maxDepth; i++)
		{
			cx += dx;
			cy += dy;
			if (!IsCellPassable(cx, cy)) break;
			count++;
		}
		return count;
	}


	private void CheckGhostCollision()
	{
		float collisionDist = _cellSize * 0.6f;

		var parent = GetParent();
		if (parent == null) return;

		foreach (var child in parent.GetChildren())
		{
			if (child is CharacterBody2D body && body != this && child is not Pacman)
			{
				if (!body.Visible) continue;

				float dist = GlobalPosition.DistanceTo(body.GlobalPosition);
				if (dist < collisionDist)
				{
					OnCaughtBy(body);
					return;
				}
			}
		}
	}

	private void OnCaughtBy(Node2D ghost)
	{
		var scene = GD.Load<PackedScene>("res://TSCN/End.tscn").Instantiate<End>();
		scene.WinnerName = ghost.Name;
		GetTree().Root.AddChild(scene);
		var oldScene = GetTree().CurrentScene;
		GetTree().CurrentScene = scene;
		oldScene.QueueFree();
	}


	private Vector2 Roulette(List<(Vector2 dir, float w)> items, float totalW)
	{
		float roll = (float)_rng.NextDouble() * totalW;
		float cum = 0f;
		foreach (var (dir, w) in items)
		{
			cum += w;
			if (roll <= cum) return dir;
		}
		return items[^1].dir;
	}

	private MapGenerator FindMapGenerator()
	{
		var parent = GetParent();
		if (parent != null)
		{
			var mg = parent.GetNodeOrNull<MapGenerator>("MapGenerator");
			if (mg != null) return mg;
		}
		return GetTree().Root.FindChild("MapGenerator", true, false) as MapGenerator;
	}

	private void _on_area_2d_body_entered(Node2D body)
	{
		if (body is CharacterBody2D && body is not Pacman)
		{
			OnCaughtBy(body);
		}
	}
}
