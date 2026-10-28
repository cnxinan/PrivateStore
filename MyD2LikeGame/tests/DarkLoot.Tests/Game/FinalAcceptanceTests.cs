using System.Reflection;
using System.Xml.Linq;
using DarkLoot.Data;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Game;
using DarkLoot.Gameplay.Crafting;

namespace DarkLoot.Tests.Game;

public sealed class FinalAcceptanceTests
{
    [Fact]
    public void SmithFeedback_CoversEveryResultAndRejectsUnknownStatus()
    {
        Assert.All(Enum.GetValues<BlacksmithStatus>(), status =>
            Assert.False(string.IsNullOrWhiteSpace(GameText.SmithStatus(status))));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameText.SmithStatus((BlacksmithStatus)999));
    }

    [Fact]
    public void FontAtlas_ContainsAllCentralizedTextAndResourceNames()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var font = XDocument.Load(Path.Combine(root, "src", "DarkLoot.Game", "Content", "Fonts", "UIFont.spritefont"),
            LoadOptions.PreserveWhitespace);
        var covered = font.Descendants("CharacterRegion").SelectMany(region =>
            Enumerable.Range(region.Element("Start")!.Value[0],
                region.Element("End")!.Value[0] - region.Element("Start")!.Value[0] + 1)).ToHashSet();
        var content = GameContentLoader.LoadDirectory(Path.Combine(root, "content", "definitions"));
        var texts = typeof(GameText).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Concat(content.Materials.Values.Select(material => material.Name))
            .Concat(content.Recipes.Values.Select(recipe => recipe.Name))
            .Concat(Enum.GetValues<BlacksmithStatus>().Select(GameText.SmithStatus));
        var missing = texts.SelectMany(text => text).Distinct().Where(character => !covered.Contains(character)).ToArray();
        Assert.True(missing.Length == 0, $"Missing glyphs: {new string(missing)}");
    }

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
