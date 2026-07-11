using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using ManagerIV.Core;

namespace ManagerIV.Tests;

public class MusicTrackToIndexConverterTests
{
    [Fact]
    public void TestConvertValidTrackIndex()
    {
        var converter = new MusicTrackToIndexConverter();

        var track1 = new MusicTrack("1", "song1.mp3", "Title 1", "Artist 1", "Album 1", TimeSpan.FromMinutes(3), ".mp3", true);
        var track2 = new MusicTrack("2", "song2.mp3", "Title 2", "Artist 2", "Album 2", TimeSpan.FromMinutes(4), ".mp3", true);
        var track3 = new MusicTrack("3", "song3.mp3", "Title 3", "Artist 3", "Album 3", TimeSpan.FromMinutes(2), ".mp3", false);

        var list = new List<MusicTrack> { track1, track2, track3 };

        var result1 = converter.Convert(new object[] { track1, list }, typeof(string), new object(), CultureInfo.InvariantCulture);
        var result2 = converter.Convert(new object[] { track2, list }, typeof(string), new object(), CultureInfo.InvariantCulture);
        var result3 = converter.Convert(new object[] { track3, list }, typeof(string), new object(), CultureInfo.InvariantCulture);

        Assert.Equal("001", result1);
        Assert.Equal("002", result2);
        Assert.Equal("003", result3);
    }

    [Fact]
    public void TestConvertInvalidData()
    {
        var converter = new MusicTrackToIndexConverter();

        var result = converter.Convert(new object[] { new object(), new object() }, typeof(string), new object(), CultureInfo.InvariantCulture);

        Assert.Equal("000", result);
    }
}
