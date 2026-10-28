using System.Reflection;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Game;

namespace DarkLoot.Tests.Game;

public sealed class FinalAcceptanceTests
{
    [Fact]
    public void ChineseDeathPresentation_ExplainsCountdownAndDisabledControls()
    {
        var strings = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Contains(strings, text => text.Contains("死亡") && text.Contains("复活") && text.Contains("{0"));
        Assert.Contains(strings, text => text.Contains("无法") && text.Contains("移动") &&
            text.Contains("攻击") && text.Contains("拾取"));
    }

    [Fact]
    public void ProductionWindowActivityGate_SkipsInactiveAndFirstReactivatedUpdate()
    {
        var gate = new WindowActivityGate();

        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(true));
        Assert.True(gate.CanAdvance(true));
        Assert.False(gate.CanAdvance(false));
        Assert.False(gate.CanAdvance(true));
        Assert.True(gate.CanAdvance(true));
    }
}
