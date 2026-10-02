using RoomBooking.Api.Infrastructure;
using Shouldly;

namespace RoomBooking.Tests.Api;

public sealed class WeakETagTests
{
    [Fact]
    public void Format_WrapsTokenAsWeakQuotedETag()
    {
        // Arrange

        // Act
        var result = WeakETag.Format("rooms.v1.g3.abc");

        // Assert
        result.ShouldBe("W/\"rooms.v1.g3.abc\"");
    }

    [Fact]
    public void Matches_SingleStrongTag_MatchesToken()
    {
        // Arrange

        // Act
        var matched = WeakETag.Matches("\"rooms.v1.g3.abc\"", "rooms.v1.g3.abc");

        // Assert
        matched.ShouldBeTrue();
    }

    [Fact]
    public void Matches_MultipleTags_MatchesWhenAnyTagMatches()
    {
        // Arrange

        // Act
        var matched = WeakETag.Matches("\"other\", W/\"rooms.v1.g3.abc\", \"another\"", "rooms.v1.g3.abc");

        // Assert
        matched.ShouldBeTrue();
    }

    [Fact]
    public void Matches_WeakPrefix_IsCaseInsensitive()
    {
        // Arrange

        // Act
        var matched = WeakETag.Matches("w/\"rooms.v1.g3.abc\"", "rooms.v1.g3.abc");

        // Assert
        matched.ShouldBeTrue();
    }

    [Fact]
    public void Matches_Wildcard_MatchesAnyToken()
    {
        // Arrange

        // Act
        var matched = WeakETag.Matches("*", "rooms.v1.g3.abc");

        // Assert
        matched.ShouldBeTrue();
    }

    [Theory]
    [InlineData("rooms.v1.g3.abc")]
    [InlineData("W/rooms.v1.g3.abc")]
    [InlineData("W/\"rooms.v1.g3.abc")]
    [InlineData("W/\"rooms\"tail\"")]
    [InlineData("\"\"")]
    public void Matches_MalformedItems_DoNotMatch(string header)
    {
        // Arrange

        // Act
        var matched = WeakETag.Matches(header, "rooms.v1.g3.abc");

        // Assert
        matched.ShouldBeFalse();
    }

    [Fact]
    public void Matches_EmptyOrNullHeader_DoesNotMatch()
    {
        // Arrange

        // Act
        var nullResult = WeakETag.Matches(null, "rooms.v1.g3.abc");
        var emptyResult = WeakETag.Matches(string.Empty, "rooms.v1.g3.abc");
        var whitespaceResult = WeakETag.Matches("   ", "rooms.v1.g3.abc");

        // Assert
        nullResult.ShouldBeFalse();
        emptyResult.ShouldBeFalse();
        whitespaceResult.ShouldBeFalse();
    }
}
