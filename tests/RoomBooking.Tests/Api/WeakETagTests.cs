using RoomBooking.Api.Infrastructure;
using Shouldly;

namespace RoomBooking.Tests.Api;

public sealed class WeakETagTests
{
    [Fact]
    public void Format_WrapsTokenAsWeakQuotedETag()
    {
        WeakETag.Format("rooms.v1.g3.abc").ShouldBe("W/\"rooms.v1.g3.abc\"");
    }

    [Fact]
    public void Matches_SingleStrongTag_MatchesToken()
    {
        WeakETag.Matches("\"rooms.v1.g3.abc\"", "rooms.v1.g3.abc").ShouldBeTrue();
    }

    [Fact]
    public void Matches_MultipleTags_MatchesWhenAnyTagMatches()
    {
        WeakETag.Matches("\"other\", W/\"rooms.v1.g3.abc\", \"another\"", "rooms.v1.g3.abc").ShouldBeTrue();
    }

    [Fact]
    public void Matches_WeakPrefix_IsCaseInsensitive()
    {
        WeakETag.Matches("w/\"rooms.v1.g3.abc\"", "rooms.v1.g3.abc").ShouldBeTrue();
    }

    [Fact]
    public void Matches_Wildcard_MatchesAnyToken()
    {
        WeakETag.Matches("*", "rooms.v1.g3.abc").ShouldBeTrue();
    }

    [Theory]
    [InlineData("rooms.v1.g3.abc")]
    [InlineData("W/rooms.v1.g3.abc")]
    [InlineData("W/\"rooms.v1.g3.abc")]
    [InlineData("W/\"rooms\"tail\"")]
    [InlineData("\"\"")]
    public void Matches_MalformedItems_DoNotMatch(string header)
    {
        WeakETag.Matches(header, "rooms.v1.g3.abc").ShouldBeFalse();
    }

    [Fact]
    public void Matches_EmptyOrNullHeader_DoesNotMatch()
    {
        WeakETag.Matches(null, "rooms.v1.g3.abc").ShouldBeFalse();
        WeakETag.Matches(string.Empty, "rooms.v1.g3.abc").ShouldBeFalse();
        WeakETag.Matches("   ", "rooms.v1.g3.abc").ShouldBeFalse();
    }
}
