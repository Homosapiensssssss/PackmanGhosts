using Godot;
using System;
using System.Collections.Generic;

public partial class Pacman : CharacterBody2D
{
	[Export] public float Speed = 150.0f;
	[Export] public float DetectionRadius = 300.0f;
	[Export] public float RandomWeight = 0.3f;

	private Vector2 _moveDir = Vector2.Zero;
	private Vector2I _fromCell;
	private Vector2I _toCell;
	private bool _needsDecision = true;

	private MapGenerator _map;
	private Random _rng = new Random();

	private static readonly Vector2[] CardinalDirs = {
		Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right
	};

	public override void _Ready()
	{
		_map = FindMapGenerator();
		if (_map == null)
		{
			GD.PrintErr("Pacman: MapGenerator не найден!");
			return;
		}

		_fromCell = SafeWorldToGrid(GlobalPosition);
		_toCell = _fromCell;
		GlobalPosition = CellCenter(_fromCell);
		_needsDecision = true;

		GD.Print($"Pacman start cell: {_fromCell}, passable: {IsCellPassable(_fromCell.X, _fromCell.Y)}");
		GD.Print($"Grid size: {_map.GridWidth}x{_map.GridHeight}, CellSize: {_map.CellSize}");
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

		Vector2 target = CellCenter(_toCell);
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

	private float RemainingDistance(Vector2 pos, Vector2 target)
	{
		if (_moveDir.X != 0)
			return Mathf.Abs(target.X - pos.X);
		if (_moveDir.Y != 0)
			return Mathf.Abs(target.Y - pos.Y);
		return pos.DistanceTo(target);
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


	private Vector2 CellCenter(Vector2I cell)
	{
		return _map.GridToWorld(cell.X, cell.Y);
	}

	private Vector2I SafeWorldToGrid(Vector2 worldPos)
	{
		int x = Mathf.FloorToInt(worldPos.X / _map.CellSize);
		int y = Mathf.FloorToInt(worldPos.Y / _map.CellSize);

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


	private Vector2 ComputeFleeDirection(Vector2I cell, List<Vector2> freeDirs)
	{
		Vector2 fleeVector = Vector2.Zero;
		bool hasThreat = false;

		var threats = new List<Node2D>();

		foreach (var node in GetTree().GetNodesInGroup("threats"))
		{
			if (node is Node2D n && n != this)
				threats.Add(n);
		}

		if (threats.Count == 0)
		{
			foreach (var node in GetTree().GetNodesInGroup("ghosts"))
			{
				if (node is Node2D n && n != this)
					threats.Add(n);
			}
		}

		// Способ 3: ищем по типу на уровне родителя
		if (threats.Count == 0)
		{
			var parent = GetParent();
			if (parent != null)
			{
				foreach (var child in parent.GetChildren())
				{
					if (child is CharacterBody2D body && body != this && child is not Pacman)
					{
						threats.Add(body);
					}
				}
			}
		}

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

		return PickWeightedDirection(fleeVector.Normalized(), cell, freeDirs);
	}

	private Vector2 PickWeightedDirection(Vector2 desired, Vector2I cell, List<Vector2> freeDirs)
	{
		var options = new List<Vector2>(freeDirs);

		if (options.Count > 1 && _moveDir != Vector2.Zero)
			options.RemoveAll(d => d == -_moveDir);
		if (options.Count == 0)
			options = new List<Vector2>(freeDirs);

		float minRaw = float.MaxValue;
		var rawScores = new List<(Vector2 dir, float raw)>();

		foreach (var dir in options)
		{
			float dot = desired.Dot(dir);
			int corridor = CountFreeCellsAhead(cell, dir, 6);
			float raw = dot * 2f + corridor * 0.3f;
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

		if (canContinue && isIntersection && _rng.NextDouble() > 0.4)
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

	private void ClampPerpendicular()
	{
		Vector2 lineStart = CellCenter(_fromCell);

		if (_moveDir.X != 0)
			GlobalPosition = new Vector2(GlobalPosition.X, lineStart.Y);
		else if (_moveDir.Y != 0)
			GlobalPosition = new Vector2(lineStart.X, GlobalPosition.Y);
	}

	private void CheckGhostCollision()
	{
		float collisionDist = _map.CellSize * 0.6f;

		var parent = GetParent();
		if (parent == null) return;

		foreach (var child in parent.GetChildren())
		{
			if (child is CharacterBody2D body && body != this && child is not Pacman)
			{
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
