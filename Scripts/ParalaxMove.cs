using Godot;

public partial class ParalaxMove : Parallax2D
{
	[Export] public Node2D Target { get; set; }
	[Export] public Vector2 ParallaxFactor { get; set; } = new Vector2(0.10f, 0.10f);

	public override void _Process(double delta)
	{
		if (Target != null)
		{
			ScrollOffset = Target.GlobalPosition * ParallaxFactor;
		}
	}
}
