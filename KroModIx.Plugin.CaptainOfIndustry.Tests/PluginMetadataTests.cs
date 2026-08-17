using FluentAssertions;
using KroModIx.Plugin.Contracts;
using Xunit;

namespace KroModIx.Plugin.CaptainOfIndustry.Tests;

/// <summary>Sanity-Check dass Metadata + Target sinnvoll sind. Ohne
/// externen State — brueckt nur den Contract.</summary>
public class PluginMetadataTests
{
    [Fact]
    public void Metadata_HasCorrectId()
    {
        var plugin = new CaptainOfIndustryPlugin();
        plugin.Metadata.Id.Should().Be("kroste.captainofindustry");
        plugin.Metadata.Version.Should().Be("0.3.0");
    }

    [Fact]
    public void Targets_ExposeCorrectSteamAppId()
    {
        var plugin = new CaptainOfIndustryPlugin();
        plugin.Targets.Should().ContainSingle();
        var target = plugin.Targets[0];
        target.GameId.Should().Be("captain-of-industry");
        target.SteamAppId.Should().Be(1594320);
        target.Platforms.Should().Be(Platforms.Both);
        target.AlternativeExecutableNames.Should().Contain("CaptainOfIndustry.exe");
    }
}
