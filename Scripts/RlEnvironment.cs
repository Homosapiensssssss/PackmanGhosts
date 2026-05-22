using Godot;
using System;
using System.Linq;

public partial class RlEnvironment : Node
{
    [Signal]
    public delegate void StepCompletedEventHandler(float reward, bool isDone, float[] nextState);

    [Export] public CharacterBody2D Agent { get; set; }
    [Export] public CharacterBody2D[] Threats { get; set; } = Array.Empty<CharacterBody2D>();
    [Export] public TileMapLayer Tilemap { get; set; } 
    [Export] public Vector2 AgentStartPos { get; set; }
    [Export] public Vector2[] ThreatStartPos { get; set; } = Array.Empty<Vector2>();

    public int GridW { get; private set; }
    public int GridH { get; private set; }
    private int _stepCount = 0;
    private const int TileSize = 32;
    private const float StepDuration = 0.2f;
    private bool[,] _collisionGrid;
    private float _stepAccumulator = 0f;
    private int? _pendingAction = null;
    private bool _isDone = false;
    private readonly Random _rng = new Random(42);

    private static readonly Vector2I[] _dirs = { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

    public override void _Ready()
    {
        BuildCollisionGrid();
        Reset();
        SetPhysicsProcess(true);
    }

    private void BuildCollisionGrid()
    {
        if (Tilemap == null) { GD.PrintErr("RlEnvironment: Tilemap не назначен!"); return; }
        
        var rect = Tilemap.GetUsedRect();
        GridW = rect.Size.X;
        GridH = rect.Size.Y;
        _collisionGrid = new bool[GridW, GridH];

        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                var coords = new Vector2I(x, y);
                _collisionGrid[x, y] = Tilemap.GetCellSourceId(coords) != -1;
            }
        }
    }

    public void Reset()
    {
        _isDone = false;
        _stepAccumulator = 0f;
        _pendingAction = null;
        if (Agent != null) Agent.GlobalPosition = AgentStartPos;
        if (Threats != null && ThreatStartPos != null)
        {
            for (int i = 0; i < Threats.Length && i < ThreatStartPos.Length; i++)
                Threats[i].GlobalPosition = ThreatStartPos[i];
        }
    }

    public void SetAction(int action)
    {
        if (!_isDone) _pendingAction = action;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isDone) return;
        _stepAccumulator += (float)delta;
        
    if (_stepCount % 60 == 0) GD.Print($"[Env] _PhysicsProcess работает. Acc: {_stepAccumulator:F2} | Pending: {_pendingAction}");
    
    if (_stepAccumulator >= StepDuration && _pendingAction.HasValue)
    {
        _stepAccumulator -= StepDuration;
        GD.Print($"[Env] Выполняю шаг! Action: {_pendingAction.Value} | Pos до: {Agent.GlobalPosition}");
        ExecuteStep(_pendingAction.Value);
        GD.Print($"[Env] Pos после: {Agent.GlobalPosition}");
        _pendingAction = null;
    }
        
    }

    private void ExecuteStep(int action)
    {
        if (action < 0 || action > 3) return;

        var agentGrid = WorldToGrid(Agent.GlobalPosition);
        var target = agentGrid + _dirs[action];
        float reward = 0f;
        _isDone = false;

        if (IsInBounds(target) && !_collisionGrid[target.X, target.Y])
            Agent.GlobalPosition = target * TileSize;
        else
            reward -= 0.1f;

        foreach (var t in Threats)
        {
            MoveThreatRandom(t);
            if (WorldToGrid(t.GlobalPosition) == WorldToGrid(Agent.GlobalPosition))
            {
                _isDone = true;
                reward -= 100f;
                break;
            }
        }

        if (!_isDone) reward += 1f;
        EmitSignal("StepCompleted", new Variant[] { reward, _isDone, GetState() });
    }

    private void MoveThreatRandom(CharacterBody2D threat)
    {
        var grid = WorldToGrid(threat.GlobalPosition);
        var neighbors = new[] { grid + Vector2I.Up, grid + Vector2I.Down, grid + Vector2I.Left, grid + Vector2I.Right };
        var valid = neighbors.Where(n => IsInBounds(n) && !_collisionGrid[n.X, n.Y]).ToList();
        if (valid.Count > 0)
            threat.GlobalPosition = valid[_rng.Next(valid.Count)] * TileSize;
    }

    private Vector2I WorldToGrid(Vector2 pos) => new Vector2I(
        (int)Mathf.Round(pos.X / TileSize), 
        (int)Mathf.Round(pos.Y / TileSize)
    );

    private bool IsInBounds(Vector2I v) => v.X >= 0 && v.X < GridW && v.Y >= 0 && v.Y < GridH;

    public float[] GetState()
    {
        var ap = WorldToGrid(Agent.GlobalPosition);
        var state = new float[2 + Threats.Length * 2];
        state[0] = ap.X; state[1] = ap.Y;
        for (int i = 0; i < Threats.Length; i++)
        {
            var tp = WorldToGrid(Threats[i].GlobalPosition);
            state[2 + i * 2] = tp.X;
            state[3 + i * 2] = tp.Y;
        }
        return state;
    }
}