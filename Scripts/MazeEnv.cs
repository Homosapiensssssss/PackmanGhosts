using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MazeEnv : Node2D
{
    [Export] public TileMap MazeTileMap;
    [Export] public CharacterBody2D Agent;
    [Export] public Node2D ThreatsParent;
    [Export] public float TileSize = 32f;
    [Export] public int MaxSteps = 1000;
    [Export] public int WallTileId = 1; // ID плитки-стены в вашем TileSet (обычно 0=пол, 1=стена)

    private List<CharacterBody2D> _threats = new();
    private Dictionary<CharacterBody2D, List<Vector2I>> _threatPaths = new();
    private Dictionary<CharacterBody2D, int> _threatPathIndices = new();
    private float _moveTimer = 0f;
    private int _stepCount;
    private bool _isDone;

    public float[] State { get; private set; }
    public float Reward { get; private set; }

    public override void _Ready()
    {
        _threats = ThreatsParent.GetChildren().Cast<CharacterBody2D>().ToList();
        foreach (var t in _threats)
        {
            _threatPaths[t] = new List<Vector2I>();
            _threatPathIndices[t] = 0;
        }
        Reset();
    }

    public void Reset()
    {
        // Начальные позиции (настройте под свою карту)
        Agent.GlobalPosition = MazeTileMap.MapToLocal(new Vector2I(2, 2));
        _stepCount = 0;
        _isDone = false;
        Reward = 0;

        for (int i = 0; i < _threats.Count; i++)
        {
            _threats[i].GlobalPosition = MazeTileMap.MapToLocal(new Vector2I(10 + i * 3, 10));
            _threatPaths[_threats[i]].Clear();
            _threatPathIndices[_threats[i]] = 0;
        }

        UpdateThreatsPaths();
        ComputeState();
    }

    // Вызывается из Python/обучения для совершения шага агентом
    public void Step(int action)
    {
        if (_isDone) return;

        var dir = action switch { 0 => Vector2.Up, 1 => Vector2.Right, 2 => Vector2.Down, 3 => Vector2.Left };
        Agent.Velocity = dir * 120f;
        Agent.MoveAndSlide();

        _stepCount++;
        Reward = -1f; // Штраф за каждый шаг

        // Проверка поимки
        foreach (var threat in _threats)
        {
            if (Agent.GlobalPosition.DistanceTo(threat.GlobalPosition) < TileSize * 0.75f)
            {
                Reward = -100f;
                _isDone = true;
                return;
            }
        }

        // Бонус за долговечность
        if (_stepCount % 100 == 0) Reward += 50f;
        if (_stepCount >= MaxSteps) { Reward = 100f; _isDone = true; }

        ComputeState();
    }

    public bool IsDone() => _isDone;

    // Физика угроз (вызывается автоматически каждый кадр)
    public override void _PhysicsProcess(double delta)
    {
        _moveTimer += (float)delta;
        if (_moveTimer >= 1.0f)
        {
            _moveTimer -= 1.0f;
            UpdateThreatsPaths();
        }
        MoveThreatsAlongPaths((float)delta);
    }

    private void ComputeState()
    {
        // Нормализуем клеточные координаты в [-1, 1]
        var agentCell = MazeTileMap.LocalToMap(Agent.GlobalPosition);
        State = new float[2 + _threats.Count * 2];
        State[0] = agentCell.X / 20f;
        State[1] = agentCell.Y / 20f;

        for (int i = 0; i < _threats.Count; i++)
        {
            var tCell = MazeTileMap.LocalToMap(_threats[i].GlobalPosition);
            State[2 + i * 2]     = tCell.X / 20f;
            State[2 + i * 2 + 1] = tCell.Y / 20f;
        }
    }

    private void UpdateThreatsPaths()
    {
        var agentCell = MazeTileMap.LocalToMap(Agent.GlobalPosition);
        foreach (var threat in _threats)
        {
            var threatCell = MazeTileMap.LocalToMap(threat.GlobalPosition);
            _threatPaths[threat] = AStarPathfinder.FindPath(MazeTileMap, threatCell, agentCell, WallTileId);
            _threatPathIndices[threat] = 0;
        }
    }

    private void MoveThreatsAlongPaths(float delta)
    {
        foreach (var threat in _threats)
        {
            var path = _threatPaths[threat];
            if (path.Count <= 1) continue;

            int idx = _threatPathIndices[threat];
            if (idx < path.Count - 1)
            {
                Vector2 target = MazeTileMap.MapToLocal(path[idx + 1]);
                Vector2 direction = (target - threat.GlobalPosition).Normalized();
                threat.Velocity = direction * 100f;
                threat.MoveAndSlide();

                // Переход к следующей клетке, когда угроза почти достигла цели
                if (threat.GlobalPosition.DistanceTo(target) < 4f)
                    _threatPathIndices[threat]++;
            }
        }
    }
}

// 🧠 Встроенный A* для сетки TileMap
public static class AStarPathfinder
{
    private static readonly Vector2I[] Directions = { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left };

    public static List<Vector2I> FindPath(TileMap tileMap, Vector2I start, Vector2I end, int wallTileId)
    {
        if (start == end) return new List<Vector2I> { start };

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0 };
        var fScore = new Dictionary<Vector2I, float> { [start] = Heuristic(start, end) };

        open.Enqueue(start, fScore[start]);
        var closed = new HashSet<Vector2I>();

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == end) return ReconstructPath(cameFrom, start, end);
            if (closed.Contains(current)) continue; // Пропускаем устаревшие записи в очереди
            closed.Add(current);

            foreach (var dir in Directions)
            {
                var neighbor = current + dir;
                if (closed.Contains(neighbor) || !IsWalkable(tileMap, neighbor, wallTileId)) continue;

                float tentativeG = gScore[current] + 1;
                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, end);
                    open.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }
        return new List<Vector2I>(); // Путь не найден
    }

    private static float Heuristic(Vector2I a, Vector2I b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static bool IsWalkable(TileMap tm, Vector2I pos, int wallId)
    {
        // Возвращает true, если клетка не является стеной.
        // GetCellSourceId возвращает -1 для пустых клеток.
        int tileId = tm.GetCellSourceId(0, pos);
        return tileId == -1 || tileId != wallId;
    }

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cf, Vector2I start, Vector2I end)
    {
        var path = new List<Vector2I> { end };
        while (cf.ContainsKey(end)) { end = cf[end]; path.Add(end); }
        path.Reverse();
        return path;
    }
}