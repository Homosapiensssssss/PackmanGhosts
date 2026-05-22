using Godot;
using System;
using System.Linq;

public partial class AgentAI : CharacterBody2D
{
    [Export] public TileMapLayer MazeLayer;
    [Export] public CharacterBody2D[] Threats;
    [Export] public string ModelPath = "user://dqn_model.json";
    [Export] public float CellSize = 50f;
    [Export] public float ActionInterval = 0.2f;

    private DQNNetwork _network;
    private float _timer = 0f;
    private bool[,] _walkableCache;
    private Vector2I _cacheOffset;
    private int _mapWidth, _mapHeight;
    private float[] _obsBuffer;

    public override void _Ready()
    {
        MotionMode = MotionModeEnum.Floating;
        if (!BuildCache()) { GD.PrintErr("❌ Ошибка кэша карты!"); SetPhysicsProcess(false); return; }
        
        _network = new DQNNetwork();
        if (!_network.LoadModel(ModelPath)) { GD.PrintErr($"❌ Модель не найдена: {ProjectSettings.GlobalizePath(ModelPath)}"); SetPhysicsProcess(false); return; }
        
        _network.Epsilon = 0f;
        GD.Print("✅ AI Агент загружен");
    }

    public override void _PhysicsProcess(double delta)
    {
        _timer += (float)delta;
        if (_timer < ActionInterval) return;
        _timer = 0f;

        float[] obs = GetObservation();
        int action = _network.SelectAction(obs);
        ApplyAction(action);
    }

    private void ApplyAction(int action)
    {
        Vector2 dir = action switch { 0 => Vector2.Up, 1 => Vector2.Down, 2 => Vector2.Left, 3 => Vector2.Right, _ => Vector2.Zero };
        Vector2 target = GlobalPosition + dir * CellSize;
        Vector2I cell = ToTilePos(target);
        if (IsWalkable(cell)) GlobalPosition = ToGlobal(cell);
    }

    private float[] GetObservation()
    {
        Array.Clear(_obsBuffer, 0, _obsBuffer.Length);
        var agentCell = ToTilePos(GlobalPosition);
        foreach (var t in Threats)
        {
            if (t == null) continue;
            var tc = ToTilePos(t.GlobalPosition);
            int tx = tc.X - _cacheOffset.X, ty = tc.Y - _cacheOffset.Y;
            if (tx >= 0 && ty >= 0 && tx < _mapWidth && ty < _mapHeight) _obsBuffer[ty * _mapWidth + tx] = -1.0f;
        }
        int ax = agentCell.X - _cacheOffset.X, ay = agentCell.Y - _cacheOffset.Y;
        if (ax >= 0 && ay >= 0 && ax < _mapWidth && ay < _mapHeight) _obsBuffer[ay * _mapWidth + ax] = 1.0f;
        
        for (int y = 0; y < _mapHeight; y++)
            for (int x = 0; x < _mapWidth; x++)
            {
                int idx = y * _mapWidth + x;
                if (_obsBuffer[idx] == 0f) _obsBuffer[idx] = _walkableCache[x, y] ? 0.0f : -0.5f;
            }
        return _obsBuffer;
    }

    private bool BuildCache()
    {
        if (MazeLayer == null) return false;
        var rect = MazeLayer.GetUsedRect();
        _cacheOffset = rect.Position;
        _mapWidth = rect.Size.X; _mapHeight = rect.Size.Y;
        _walkableCache = new bool[_mapWidth, _mapHeight];
        _obsBuffer = new float[_mapWidth * _mapHeight];
        
        // Синхронизация с DQNNetwork
        _network = new DQNNetwork { InputSize = _mapWidth * _mapHeight };
        
        for (int y = 0; y < _mapHeight; y++)
            for (int x = 0; x < _mapWidth; x++)
                _walkableCache[x, y] = MazeLayer.GetCellSourceId(new Vector2I(_cacheOffset.X + x, _cacheOffset.Y + y)) == -1;
        return true;
    }

    private bool IsWalkable(Vector2I cell)
    {
        int x = cell.X - _cacheOffset.X, y = cell.Y - _cacheOffset.Y;
        return x >= 0 && y >= 0 && x < _mapWidth && y < _mapHeight && _walkableCache[x, y];
    }

    private Vector2 ToGlobal(Vector2I tile) => new Vector2(tile.X * CellSize + CellSize * 0.5f, tile.Y * CellSize + CellSize * 0.5f);
    private Vector2I ToTilePos(Vector2 global) => (Vector2I)((global - new Vector2(CellSize * 0.5f, CellSize * 0.5f)) / CellSize);
}