using Godot;

/// <summary>Parametri de contact estimativi, separați de gravitație și zbor.</summary>
public static class PieceContactModel
{
    // Godot adună Bounce la contact: două contribuții de .14 dau .28.
    public const float BallPairRestitution=.28f;
    public const float BounceContribution=BallPairRestitution/2;
    public const float SlidingFriction=.35f;
    // Rezistența la rostogolire pe o suprafață: de calibrat prin filmare.
    public const float RollingDeceleration=.35f; // m/s², în regim fără alunecare
    public const float SpinDeceleration=4f; // rad/s² la contact, de calibrat
    public const float InertiaFactor=2f/3; // aproximarea unei sfere cu perete subțire
    public static PhysicsMaterial BallMaterial() => new() { Bounce=BounceContribution, Friction=SlidingFriction };
    // Păstrăm ricoșeul minge-podea la .28, separat de minge-minge.
    public static PhysicsMaterial FloorMaterial() => new() { Bounce=BounceContribution, Friction=1 };
}
