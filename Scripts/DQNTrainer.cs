using System;
using System.Threading.Tasks;
using Godot;

public partial class DQNTrainer : Node
{
    [Export] public DQNEnvironment Environment;
    [Export] public DQNNetwork Network;
    [Export] public int Episodes = 5000;
    [Export] public int MaxSteps = 1000;
    [Export] public string SavePath = "user://dqn_model.json";
    [Export] public bool AutoStart = true;

    private bool _training = false;

    public override void _Ready()
    {
        GD.Print("🟢 DQNTrainer загружен.");
        if (Environment == null || Network == null)
        {
            GD.PrintErr("❌ Назначьте Environment и Network в Inspector!");
            return;
        }
        if (AutoStart) StartTraining();
    }

    public async void StartTraining()
    {
        if (_training) { GD.PrintErr("⚠️ Уже запущено!"); return; }
        _training = true;
        GD.Print("🚀 Обучение запущено (окно не будет зависать)");

        try
        {
            for (int ep = 0; ep < Episodes; ep++)
            {
                float[] state = Environment.Reset();
                float epReward = 0; bool done = false; int step = 0;

                while (!done && step < MaxSteps)
                {
                    int action = Network.SelectAction(state);
                    var (nextState, reward, isDone) = Environment.Step(action);
                    Network.StoreTransition(state, action, reward, nextState, isDone);
                    
                    state = nextState; epReward += reward; done = isDone; step++;

                    Network.TrainStep();
                    if (step % Network.TargetUpdateFreq == 0) Network.UpdateTargetNetwork();

                    // ✅ ИСПРАВЛЕНО: корректный await сигнала в Godot 4 C#
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }

                if (ep % 50 == 0)
                    GD.Print($"📊 Эпизод {ep}/{Episodes} | Награда: {epReward:F1} | Epsilon: {Network.Epsilon:F3}");
            }

            Network.SaveModel(SavePath);
            GD.Print($"✅ Обучение завершено! Файл: {ProjectSettings.GlobalizePath(SavePath)}");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"❌ Ошибка обучения: {ex.Message}");
        }
        finally
        {
            _training = false;
        }
    }
}