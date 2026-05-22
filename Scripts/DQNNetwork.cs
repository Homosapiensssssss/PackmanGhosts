using System;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class DQNNetwork : Node
{
    [Export] public int InputSize = 1125;  // 25 * 45
    [Export] public int HiddenSize = 256;
    [Export] public int ActionSize = 4;
    [Export] public float Gamma = 0.99f;
    [Export] public float LearningRate = 0.0005f;
    [Export] public float Epsilon = 1.0f;
    [Export] public float EpsilonMin = 0.05f;
    [Export] public float EpsilonDecay = 0.999f;
    [Export] public int BatchSize = 64;
    [Export] public int ReplayCapacity = 50000;
    [Export] public int TargetUpdateFreq = 200;

    private float[] W1, b1, W2, b2;
    private float[] W1_target, b1_target, W2_target, b2_target;
    private readonly Random _rng = new();

    private class Transition { public float[] State; public int Action; public float Reward; public float[] NextState; public bool Done; }
    private Transition[] _buffer;
    private int _writePos = 0;
    private int _count = 0;

    public override void _Ready() => InitNetwork();

    private void InitNetwork()
    {
        W1 = InitWeights(InputSize, HiddenSize); b1 = new float[HiddenSize];
        W2 = InitWeights(HiddenSize, ActionSize); b2 = new float[ActionSize];
        CloneToTarget();
        _buffer = new Transition[ReplayCapacity];
    }

    private float[] InitWeights(int rows, int cols)
    {
        float scale = MathF.Sqrt(2.0f / rows);
        float[] w = new float[rows * cols];
        for (int i = 0; i < w.Length; i++) w[i] = (float)(_rng.NextDouble() * 2.0 - 1.0) * scale;
        return w;
    }

    private void CloneToTarget()
    {
        W1_target = (float[])W1.Clone(); b1_target = (float[])b1.Clone();
        W2_target = (float[])W2.Clone(); b2_target = (float[])b2.Clone();
    }

    public int SelectAction(float[] state)
    {
        if (_rng.NextDouble() < Epsilon) return _rng.Next(ActionSize);
        float[] q = Forward(state, W1, b1, W2, b2);
        int best = 0; for (int i = 1; i < ActionSize; i++) if (q[i] > q[best]) best = i;
        return best;
    }

    public void StoreTransition(float[] state, int action, float reward, float[] nextState, bool done)
    {
        _buffer[_writePos] = new Transition { State = state, Action = action, Reward = reward, NextState = nextState, Done = done };
        _writePos = (_writePos + 1) % ReplayCapacity;
        if (_count < ReplayCapacity) _count++;
    }

    public void TrainStep()
    {
        if (_count < BatchSize) return;

        float[] dW1 = new float[W1.Length], db1 = new float[b1.Length];
        float[] dW2 = new float[W2.Length], db2 = new float[b2.Length];
        float avg = 1.0f / BatchSize;

        for (int b = 0; b < BatchSize; b++)
        {
            var t = _buffer[_rng.Next(_count)];
            float[] target = ComputeTarget(t);
            ForwardWithCache(t.State, out var z1, out var a1, out var z2);

            float[] dq = new float[ActionSize];
            for (int i = 0; i < ActionSize; i++) dq[i] = z2[i] - target[i];

            for (int i = 0; i < ActionSize; i++)
            {
                db2[i] += dq[i];
                for (int j = 0; j < HiddenSize; j++) dW2[i * HiddenSize + j] += dq[i] * a1[j];
            }

            float[] da1 = new float[HiddenSize];
            for (int j = 0; j < HiddenSize; j++)
                for (int i = 0; i < ActionSize; i++) da1[j] += W2[i * HiddenSize + j] * dq[i];

            for (int j = 0; j < HiddenSize; j++)
            {
                float dz1 = da1[j] * (z1[j] > 0 ? 1f : 0f);
                db1[j] += dz1;
                for (int k = 0; k < InputSize; k++) dW1[j * InputSize + k] += dz1 * t.State[k];
            }
        }

        for (int i = 0; i < W2.Length; i++) W2[i] -= LearningRate * dW2[i] * avg;
        for (int i = 0; i < b2.Length; i++) b2[i] -= LearningRate * db2[i] * avg;
        for (int i = 0; i < W1.Length; i++) W1[i] -= LearningRate * dW1[i] * avg;
        for (int i = 0; i < b1.Length; i++) b1[i] -= LearningRate * db1[i] * avg;

        Epsilon = Math.Max(EpsilonMin, Epsilon * EpsilonDecay);
    }

    private float[] ComputeTarget(Transition t)
    {
        float[] q = Forward(t.State, W1, b1, W2, b2);
        q[t.Action] = t.Done ? t.Reward : t.Reward + Gamma * Max(Forward(t.NextState, W1_target, b1_target, W2_target, b2_target));
        return q;
    }

    private float[] Forward(float[] x, float[] w1, float[] b1, float[] w2, float[] b2)
    {
        float[] a1 = new float[HiddenSize];
        for (int j = 0; j < HiddenSize; j++) { float s = b1[j]; for (int i = 0; i < InputSize; i++) s += w1[j * InputSize + i] * x[i]; a1[j] = MathF.Max(0f, s); }
        float[] z2 = new float[ActionSize];
        for (int k = 0; k < ActionSize; k++) { float s = b2[k]; for (int j = 0; j < HiddenSize; j++) s += w2[k * HiddenSize + j] * a1[j]; z2[k] = s; }
        return z2;
    }

    // ✅ ИСПРАВЛЕНО: Используем W1/W2 (поля класса), а не несуществующие w1/w2
    private void ForwardWithCache(float[] x, out float[] z1, out float[] a1, out float[] z2)
    {
        z1 = new float[HiddenSize]; a1 = new float[HiddenSize];
        for (int j = 0; j < HiddenSize; j++)
        {
            float s = b1[j];
            for (int i = 0; i < InputSize; i++) s += W1[j * InputSize + i] * x[i];
            z1[j] = s;
            a1[j] = MathF.Max(0f, s);
        }
        z2 = new float[ActionSize];
        for (int k = 0; k < ActionSize; k++)
        {
            float s = b2[k];
            for (int j = 0; j < HiddenSize; j++) s += W2[k * HiddenSize + j] * a1[j];
            z2[k] = s;
        }
    }

    public void UpdateTargetNetwork() => CloneToTarget();
    private float Max(float[] arr) { float m = arr[0]; for (int i = 1; i < arr.Length; i++) if (arr[i] > m) m = arr[i]; return m; }

    public void SaveModel(string path)
    {
        int lastSlash = path.LastIndexOf('/');
        if (lastSlash > 0) { string dir = path.Substring(0, lastSlash); if (!DirAccess.DirExistsAbsolute(dir)) DirAccess.MakeDirRecursiveAbsolute(dir); }
        
        var model = new SerializableModel { W1 = W1, b1 = b1, W2 = W2, b2 = b2, W1_target = W1_target, b1_target = b1_target, W2_target = W2_target, b2_target = b2_target };
        string json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file != null) file.StoreString(json);
        else GD.PrintErr($"❌ Не удалось сохранить модель. Путь: {ProjectSettings.GlobalizePath(path)}");
    }

    public bool LoadModel(string path)
    {
        if (!FileAccess.FileExists(path)) return false;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var model = JsonSerializer.Deserialize<SerializableModel>(file.GetAsText());
        if (model == null) return false;
        W1 = model.W1; b1 = model.b1; W2 = model.W2; b2 = model.b2;
        CloneToTarget();
        return true;
    }
}

public class SerializableModel
{
    public float[] W1, b1, W2, b2, W1_target, b1_target, W2_target, b2_target;
}