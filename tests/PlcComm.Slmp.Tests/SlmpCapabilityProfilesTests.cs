using System.Text.Json;
using PlcComm.Slmp;

namespace PlcComm.Slmp.Tests;

public sealed class SlmpCapabilityProfilesTests
{
    [Fact]
    public void BuiltInCapabilityProfiles_MatchCanonicalFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "slmp_ethernet_profiles.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var expectedProfiles = document.RootElement.GetProperty("profiles");
        var expectedIds = expectedProfiles.EnumerateObject().Select(static property => property.Name).Order().ToArray();
        var actualIds = SlmpCapabilityProfiles.All.Values.Select(static profile => profile.ProfileId).Order().ToArray();

        Assert.Equal(expectedIds, actualIds);

        foreach (var expectedProfileProperty in expectedProfiles.EnumerateObject())
        {
            var profile = SlmpPlcProfiles.ParseKnownProfileId(expectedProfileProperty.Name);
            Assert.True(SlmpCapabilityProfiles.TryGetProfile(profile, out var actualProfile));
            var expectedProfile = expectedProfileProperty.Value;

            Assert.Equal(
                expectedProfile.GetProperty("display_name").GetString(),
                SlmpPlcProfiles.GetDisplayName(profile));
            Assert.Equal(expectedProfile.GetProperty("frame").GetString(), actualProfile.Frame);
            Assert.Equal(expectedProfile.GetProperty("compat").GetString(), actualProfile.Compat);

            var expectedFeatures = expectedProfile.GetProperty("features");
            Assert.Equal(
                expectedFeatures.EnumerateObject().Select(static property => property.Name).Order().ToArray(),
                actualProfile.Features.Keys.Select(SlmpCapabilityProfiles.ToCanonicalFeatureKey).Order().ToArray());

            foreach (var expectedFeatureProperty in expectedFeatures.EnumerateObject())
            {
                var feature = actualProfile.Features.Single(entry =>
                    SlmpCapabilityProfiles.ToCanonicalFeatureKey(entry.Key) == expectedFeatureProperty.Name);
                Assert.Equal(
                    expectedFeatureProperty.Value.GetProperty("state").GetString(),
                    SlmpCapabilityProfiles.ToCanonicalState(feature.Value.State));
                Assert.Equal(expectedFeatureProperty.Value.GetProperty("source").GetString(), feature.Value.Source);
            }

            var expectedLimits = expectedProfile.GetProperty("limits");
            Assert.Equal(
                expectedLimits.EnumerateObject().Select(static property => property.Name).Order().ToArray(),
                actualProfile.Limits.Keys.Select(SlmpCapabilityProfiles.ToCanonicalLimitKey).Order().ToArray());

            foreach (var expectedLimitProperty in expectedLimits.EnumerateObject())
            {
                var limit = actualProfile.Limits.Single(entry =>
                    SlmpCapabilityProfiles.ToCanonicalLimitKey(entry.Key) == expectedLimitProperty.Name).Value;
                Assert.Equal(expectedLimitProperty.Value.GetProperty("max").GetInt32(), limit.Max);
                var expectedOverEndCode = OptionalString(expectedLimitProperty.Value, "over_end_code");
                Assert.True(
                    string.Equals(expectedOverEndCode, limit.OverEndCode, StringComparison.Ordinal),
                    $"{expectedProfileProperty.Name}/{expectedLimitProperty.Name}: expected over_end_code '{expectedOverEndCode}', actual '{limit.OverEndCode}'.");
                var expectedSource = expectedLimitProperty.Value.GetProperty("source").GetString();
                Assert.True(
                    string.Equals(expectedSource, limit.Source, StringComparison.Ordinal),
                    $"{expectedProfileProperty.Name}/{expectedLimitProperty.Name}: expected source '{expectedSource}', actual '{limit.Source}'.");
                if (expectedLimitProperty.Value.TryGetProperty("weighted_max", out var weightedMax))
                    Assert.Equal(weightedMax.GetInt32(), limit.WeightedMax);
                else
                    Assert.Null(limit.WeightedMax);
            }

            var expectedWritePolicy = expectedProfile.GetProperty("write_policy");
            Assert.Equal(
                expectedWritePolicy.EnumerateObject().Select(static property => property.Name).Order().ToArray(),
                actualProfile.WritePolicy.Keys.Order().ToArray());
            foreach (var expectedPolicyProperty in expectedWritePolicy.EnumerateObject())
                Assert.Equal(expectedPolicyProperty.Value.GetString(), actualProfile.WritePolicy[expectedPolicyProperty.Name]);
        }
    }

    [Fact]
    public void ProfileDescriptors_MatchCanonicalProfileMetadata()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "slmp_ethernet_profiles.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var expectedProfiles = document.RootElement.GetProperty("profiles");
        var descriptors = SlmpPlcProfiles.GetProfileDescriptors();
        var expectedIds = expectedProfiles.EnumerateObject().Select(static property => property.Name).Order().ToArray();
        var actualIds = descriptors.Select(static descriptor => descriptor.CanonicalName).Order().ToArray();

        Assert.Equal(expectedIds, actualIds);

        foreach (var descriptor in descriptors)
        {
            var expected = expectedProfiles.GetProperty(descriptor.CanonicalName);
            Assert.Equal(expected.GetProperty("display_name").GetString(), descriptor.DisplayName);
            Assert.Equal(
                !expected.TryGetProperty("role", out var role) || role.GetString() != "base",
                descriptor.Connectable);
            Assert.Equal(
                expected.TryGetProperty("base_profile", out var baseProfile) ? baseProfile.GetString() : null,
                descriptor.BaseProfile);
        }
    }

    [Fact]
    public void MxRRj71En71_ProfileIdAndClientDefaultsAreDirectlyUsable()
    {
        var profile = SlmpPlcProfiles.ParseKnownProfileId("melsec:mx-r:rj71en71");

        using var client = new SlmpClient(
            "127.0.0.1",
            profile,
            1025,
            SlmpTransportMode.Udp,
            SlmpTargetAddress.OwnStation);

        Assert.Equal(SlmpPlcProfile.MxRRj71En71, profile);
        Assert.Equal(SlmpPlcProfile.MxRRj71En71, client.PlcProfile);
        Assert.Equal(SlmpFrameType.Frame4E, client.FrameType);
        Assert.Equal(SlmpCompatibilityMode.Iqr, client.CompatibilityMode);
    }

    private static string? OptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
}
