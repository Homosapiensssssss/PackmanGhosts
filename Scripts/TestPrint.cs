using Godot;
public partial class TestPrint : Node
{
    public override void _Ready()
    {
        GD.Print("=== GODOT C# РАБОТАЕТ ===");
        GD.PrintErr("=== ТЕСТ ОШИБКИ ===");
    }
}