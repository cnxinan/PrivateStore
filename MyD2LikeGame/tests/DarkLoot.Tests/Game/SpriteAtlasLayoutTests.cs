using System.Numerics;
using DarkLoot.Engine.Animation;
using DarkLoot.Game;
using Microsoft.Xna.Framework;

namespace DarkLoot.Tests.Game;

public sealed class SpriteAtlasLayoutTests
{
    private const string Metadata = """
        {"version":1,"frameWidth":48,"frameHeight":48,"columns":6,
        "directions":["E","SE","S","SW","W","NW","N","NE"],
        "actions":["Idle","Move","Attack","Hurt","Dead"],
        "originX":24,"originY":24,"scale":2}
        """;

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(0, 1, 2)]
    [InlineData(-1, 1, 3)]
    [InlineData(-1, 0, 4)]
    [InlineData(-1, -1, 5)]
    [InlineData(0, -1, 6)]
    [InlineData(1, -1, 7)]
    public void Frame_MapsEightFacingsAndActionRows(float x, float y, int direction)
    {
        var atlas = SpriteAtlasLayout.Parse(Metadata);
        Assert.Equal(new Rectangle(96, (16 + direction) * 48, 48, 48),
            atlas.Frame(new ActorFrame(ActorAction.Attack, .4f), new System.Numerics.Vector2(x, y)));
    }

    [Fact]
    public void Frame_ClampsFinishedDeathToLastCellWithoutLooping()
    {
        var atlas = SpriteAtlasLayout.Parse(Metadata);
        Assert.Equal(new Rectangle(240, 1872, 48, 48),
            atlas.Frame(new ActorFrame(ActorAction.Dead, 1), new System.Numerics.Vector2(1, -1)));
        atlas.ValidateTextureSize(288, 1920, "test-actor");
    }

    [Theory]
    [InlineData("\"frameWidth\":48", "\"frameWidth\":0", "frameWidth")]
    [InlineData("\"version\":1", "\"version\":2", "version")]
    [InlineData("\"scale\":2", "\"scale\":0", "scale")]
    [InlineData("\"originY\":24", "\"originY\":60", "origin")]
    [InlineData("\"Idle\",\"Move\"", "\"Move\",\"Idle\"", "actions")]
    [InlineData("\"E\",\"SE\"", "\"SE\",\"E\"", "directions")]
    public void Parse_RejectsInvalidOrMismatchedMetadata(string original, string replacement, string field)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            SpriteAtlasLayout.Parse(Metadata.Replace(original, replacement)));
        Assert.Contains(field, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("\"frameWidth\":48", "\"frameWidth\":96", "frameWidth")]
    [InlineData("\"frameHeight\":48", "\"frameHeight\":32", "frameHeight")]
    [InlineData("\"columns\":6", "\"columns\":3", "columns")]
    [InlineData("\"originX\":24", "\"originX\":12", "origin")]
    [InlineData("\"originY\":24", "\"originY\":12", "origin")]
    [InlineData("\"scale\":2", "\"scale\":3", "scale")]
    [InlineData("\"frameWidth\":48,\"frameHeight\":48,\"columns\":6",
        "\"frameWidth\":96,\"frameHeight\":48,\"columns\":3", "frameWidth")]
    public void Parse_RejectsIncompatibleVersionOneLayout(string original, string replacement, string field)
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            SpriteAtlasLayout.Parse(Metadata.Replace(original, replacement)));
        Assert.Contains(field, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTextureSize_ReportsTruncatedAtlasWithAssetName()
    {
        var atlas = SpriteAtlasLayout.Parse(Metadata);
        var error = Assert.Throws<InvalidDataException>(() =>
            atlas.ValidateTextureSize(288, 1919, "Pixel/Actors/player"));
        Assert.Contains("Pixel/Actors/player", error.Message);
        Assert.Contains("1920", error.Message);
    }

    [Fact]
    public void Frame_RejectsInvalidProgressAndFacingInsteadOfSelectingWrongPixels()
    {
        var atlas = SpriteAtlasLayout.Parse(Metadata);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            atlas.Frame(new ActorFrame(ActorAction.Idle, float.NaN), System.Numerics.Vector2.UnitX));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            atlas.Frame(new ActorFrame(ActorAction.Idle, -.1f), System.Numerics.Vector2.UnitX));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            atlas.Frame(new ActorFrame(ActorAction.Idle, 0), System.Numerics.Vector2.Zero));
    }
}
