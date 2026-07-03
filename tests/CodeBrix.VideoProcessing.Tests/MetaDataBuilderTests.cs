using System.Text.RegularExpressions;
using CodeBrix.VideoProcessing.Builders.MetaData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SilverAssertions;
using CodeBrix.VideoProcessing.Tests.Utilities;

namespace CodeBrix.VideoProcessing.Tests;
public class MetaDataBuilderTests
{
    [Fact]
    public void TestMetaDataBuilderIntegrity()
    {
        var source = new
        {
            Album = "Kanon und Gigue",
            Artist = "Pachelbel",
            Title = "Kanon und Gigue in D-Dur",
            Copyright = "Copyright Lol",
            Composer = "Pachelbel",
            Genres = new[] { "Synthwave", "Classics" },
            Tracks = new[]
            {
                new { Duration = TimeSpan.FromSeconds(10), Title = "Chapter 01" }, new { Duration = TimeSpan.FromSeconds(10), Title = "Chapter 02" },
                new { Duration = TimeSpan.FromSeconds(10), Title = "Chapter 03" }, new { Duration = TimeSpan.FromSeconds(10), Title = "Chapter 04" }
            }
        };

        var builder = new MetaDataBuilder()
            .WithTitle(source.Title)
            .WithArtists(source.Artist)
            .WithComposers(source.Composer)
            .WithAlbumArtists(source.Artist)
            .WithGenres(source.Genres)
            .WithCopyright(source.Copyright)
            .AddChapters(source.Tracks, x => (x.Duration, x.Title));

        var metadata = builder.Build();
        var serialized = MetaDataSerializer.Instance.Serialize(metadata);

        (serialized.StartsWith(";FFMETADATA1", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        (serialized.Contains("genre=Synthwave; Classics", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        (serialized.Contains("title=Chapter 01", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        (serialized.Contains("album_artist=Pachelbel", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public void TestMapMetadata()
    {
        //-i "whaterver0" // index: 0
        //-f concat -safe 0
        //-i "\AppData\Local\Temp\concat_b511f2bf-c4af-4f71-b9bd-24d706bf4861.txt"   // index: 1
        //-i "\AppData\Local\Temp\metadata_210d3259-3d5c-43c8-9786-54b5c414fa70.txt" // index: 2
        //-map_metadata 2

        var text0 = FFMpegArguments.FromFileInput("whaterver0")
            .AddMetaData("WhatEver3")
            .Text;

        var text1 = FFMpegArguments.FromFileInput("whaterver0")
            .AddDemuxConcatInput(new[] { "whaterver", "whaterver1" })
            .AddMetaData("WhatEver3")
            .Text;

        (Regex.IsMatch(text0, "metadata_[0-9a-f-]+\\.txt\" -map_metadata 1")).Should().BeTrue("map_metadata index is calculated incorrectly.");
        (Regex.IsMatch(text1, "metadata_[0-9a-f-]+\\.txt\" -map_metadata 2")).Should().BeTrue("map_metadata index is calculated incorrectly.");
    }
}
