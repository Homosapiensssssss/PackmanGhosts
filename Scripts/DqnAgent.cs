using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DqnAgent : Node
{
    [Export] public RlEnvironment Env { get; set; }
    [Export] public int HiddenSize { get; set; } = 64;
    [Export] public float LearningRate { get; set; } = 0.001f;
    [Export] public float Gamma { get; set; } = 0.95f;
    [Export] public float EpsilonStart { get; set; } = 1.0f;
    [Export] public float EpsilonEnd { get; set; } = 0.05f;
    [Export] public float EpsilonDecay { get; set; } = 0.995f;
    [Export] public int BatchSize { get; set; } = 32;
    [Export] public int BufferCapacity { get; set; } = 5000;
    [Export] public int TargetUpdateFreq { get; set; } = 100;

    private int _stateSize, _actionSize = 4;
    private float _epsilon;
    private int _stepCount = 0;
    private readonly Random _rng = new Random(42);

    private class Exp { public float[] S; public int A; public float R; public float[] S2; public bool Done; }
    private readonly Queue<Exp> _buffer = new();

    private float[,] _W1, _W2;
    private float[] _b1, _b2;
    private float[,] _tW1, _tW2;
    private float[] _tb1, _tb2;

    private float[] _lastState;
    private int _lastAction = -1;

    public override void _Ready()
    {
        if (Env == null) { GD.PrintErr("DqnAgent: Env не назначен!"); return; }
        Env.StepCompleted += OnStepCompleted;
        _stateSize = Env.GetState().Length;
        _epsilon = EpsilonStart;
        InitNetworks();
        Env.Reset();
        int firstAction = SelectAction(Env.GetState());
        Env.SetAction(firstAction);
        GD.Print($"[DQN] Первое действие: {firstAction}");
    }

    private void OnStepCompleted(float reward, bool done, float[] nextState)
    {
        if (_lastState != null && _lastAction >= 0)
        {
            if (_buffer.Count >= BufferCapacity) _buffer.Dequeue();
            _buffer.Enqueue(new Exp { S = _lastState, A = _lastAction, R = reward, S2 = nextState, Done = done });
        }

        if (_buffer.Count >= BatchSize) TrainBatch();
        if (++_stepCount % TargetUpdateFreq == 0) CopyToTarget();
        if (done) _epsilon = Math.Max(EpsilonEnd, _epsilon * EpsilonDecay);

        if (done)
        {
            Env.Reset();
            _lastState = null;
            _lastAction = -1;
        }
        else
        {
            _lastState = nextState;
            _lastAction = SelectAction(nextState);
            Env.SetAction(_lastAction);
        }
    }

    private int SelectAction(float[] state)
    {
        if (_rng.NextDouble() < _epsilon) return _rng.Next(_actionSize);
        var q = Forward(_W1, _b1, _W2, _b2, Normalize(state));
        int best = 0;
        for (int i = 1; i < _actionSize; i++) if (q[i] > q[best]) best = i;
        return best;
    }

    private void TrainBatch()
    {
        var sample = _buffer.OrderBy(_ => _rng.Next()).Take(BatchSize).ToList();
        
        foreach (var exp in sample)
        {
            var s = Normalize(exp.S);
            var s2 = Normalize(exp.S2);
            
            var qPred = Forward(_W1, _b1, _W2, _b2, s);
            var qTarget = Forward(_tW1, _tb1, _tW2, _tb2, s2);

            float maxNextQ = exp.Done ? 0f : qTarget.Max();
            float target = exp.R + Gamma * maxNextQ;
            float error = qPred[exp.A] - target; 

            float[] dz2 = new float[_actionSize];
            dz2[exp.A] = error;

            // Hidden layer gradients
            float[] a1 = AddArrays(MatVecMul(s, _W1), _b1); 
            float[] z1 = ReLU(a1);
            
            float[] da1 = MatVecMul(dz2, Transpose(_W2));
            float[] dz1 = new float[HiddenSize];
            for (int i = 0; i < HiddenSize; i++) 
                dz1[i] = da1[i] * (a1[i] > 0 ? 1 : 0); 

            for (int j = 0; j < _actionSize; j++)
            {
                _b2[j] -= LearningRate * dz2[j];
                for (int i = 0; i < HiddenSize; i++)
                    _W2[i, j] -= LearningRate * z1[i] * dz2[j];
            }

            for (int j = 0; j < HiddenSize; j++)
            {
                _b1[j] -= LearningRate * dz1[j];
                for (int i = 0; i < _stateSize; i++)
                    _W1[i, j] -= LearningRate * s[i] * dz1[j];
            }
        }
    }

    private float[] Forward(float[,] W1, float[] b1, float[,] W2, float[] b2, float[] x)
    {
        float[] z1 = AddArrays(MatVecMul(x, W1), b1);
        float[] a1 = ReLU(z1);
        return AddArrays(MatVecMul(a1, W2), b2);
    }

    private float[] Normalize(float[] s)
    {
        var n = new float[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            n[i] = (i % 2 == 0) ? s[i] / Env.GridW : s[i] / Env.GridH;
        }
        return n;
    }

    private float[] ReLU(float[] v)
    {
        var r = new float[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = MathF.Max(0, v[i]);
        return r;
    }

    private float[] AddArrays(float[] a, float[] b)
    {
        var r = new float[a.Length];
        for (int i = 0; i < a.Length; i++) r[i] = a[i] + b[i];
        return r;
    }

    private float[] MatVecMul(float[] x, float[,] W)
    {
        int rows = W.GetLength(0), cols = W.GetLength(1);
        var outV = new float[cols];
        for (int j = 0; j < cols; j++)
            for (int i = 0; i < rows; i++) 
                outV[j] += x[i] * W[i, j];
        return outV;
    }

    private float[,] Transpose(float[,] m)
    {
        int r = m.GetLength(0), c = m.GetLength(1);
        var t = new float[c, r];
        for (int i = 0; i < r; i++) 
            for (int j = 0; j < c; j++) 
                t[j, i] = m[i, j];
        return t;
    }

    private void InitNetworks()
    {
        float s1 = MathF.Sqrt(2f / (_stateSize + HiddenSize));
        float s2 = MathF.Sqrt(2f / (HiddenSize + _actionSize));
        _W1 = InitMatrix(_stateSize, HiddenSize, s1); 
        _b1 = new float[HiddenSize];
        _W2 = InitMatrix(HiddenSize, _actionSize, s2); 
        _b2 = new float[_actionSize];
        CopyToTarget();
    }

    private float[,] InitMatrix(int r, int c, float scale)
    {
        var m = new float[r, c];
        for (int i = 0; i < r; i++) 
            for (int j = 0; j < c; j++) 
                m[i, j] = (float)(_rng.NextDouble() * 2 - 1) * scale;
        return m;
    }

    private void CopyToTarget()
    {
        _tW1 = (float[,])_W1.Clone(); _tb1 = (float[])_b1.Clone();
        _tW2 = (float[,])_W2.Clone(); _tb2 = (float[])_b2.Clone();
    }
}