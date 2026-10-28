namespace DarkLoot.Engine.Diagnostics;

public sealed class WindowActivityGate
{
    private bool _wasActive;

    public bool CanAdvance(bool isActive)
    {
        var canAdvance = isActive && _wasActive;
        _wasActive = isActive;
        return canAdvance;
    }
}
