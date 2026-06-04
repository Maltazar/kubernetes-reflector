using ES.Kubernetes.Reflector.Mirroring.Core;

namespace ES.Kubernetes.Reflector.Tests.Unit;

public class MetadataFilterTests
{
    // --- Filter ---

    [Fact]
    public void Filter_NullSource_ReturnsEmpty()
    {
        var result = MetadataFilter.Filter(null, ".*");
        Assert.Empty(result);
    }

    [Fact]
    public void Filter_EmptyPattern_ReturnsEmpty()
    {
        var source = new Dictionary<string, string> { ["app"] = "web" };
        var result = MetadataFilter.Filter(source, "");
        Assert.Empty(result);
    }

    [Fact]
    public void Filter_WhitespacePattern_ReturnsEmpty()
    {
        var source = new Dictionary<string, string> { ["app"] = "web" };
        var result = MetadataFilter.Filter(source, "   ");
        Assert.Empty(result);
    }

    [Fact]
    public void Filter_WildcardPattern_ReturnsAll()
    {
        var source = new Dictionary<string, string>
        {
            ["app"] = "web",
            ["tier"] = "frontend"
        };
        var result = MetadataFilter.Filter(source, ".*");
        Assert.Equal(2, result.Count);
        Assert.Equal("web", result["app"]);
        Assert.Equal("frontend", result["tier"]);
    }

    [Fact]
    public void Filter_SpecificPattern_MatchesOnlyMatchingKeys()
    {
        var source = new Dictionary<string, string>
        {
            ["my-app-name"] = "api",
            ["my-app-tier"] = "backend",
            ["other-label"] = "value"
        };
        var result = MetadataFilter.Filter(source, "my-app-.*");
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("my-app-name"));
        Assert.True(result.ContainsKey("my-app-tier"));
        Assert.False(result.ContainsKey("other-label"));
    }

    [Fact]
    public void Filter_UsesFullMatchSemantics()
    {
        var source = new Dictionary<string, string>
        {
            ["my-app"] = "v1",
            ["x-my-app"] = "v2",
            ["my-app-extra"] = "v3"
        };
        // "my-app" should only match "my-app" exactly (full match)
        var result = MetadataFilter.Filter(source, "my-app");
        Assert.Single(result);
        Assert.Equal("v1", result["my-app"]);
    }

    [Fact]
    public void Filter_CommaSeparatedPatterns_MatchesMultiple()
    {
        var source = new Dictionary<string, string>
        {
            ["app"] = "web",
            ["tier"] = "frontend",
            ["version"] = "v1",
            ["unrelated"] = "x"
        };
        var result = MetadataFilter.Filter(source, "app,tier");
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("app"));
        Assert.True(result.ContainsKey("tier"));
    }

    [Fact]
    public void Filter_CommaSeparatedWithRegex_Works()
    {
        var source = new Dictionary<string, string>
        {
            ["app.kubernetes.io/name"] = "api",
            ["app.kubernetes.io/version"] = "v1",
            ["my-label"] = "val",
            ["other"] = "x"
        };
        var result = MetadataFilter.Filter(source, @"app\.kubernetes\.io/.*,my-label");
        Assert.Equal(3, result.Count);
        Assert.False(result.ContainsKey("other"));
    }

    [Fact]
    public void Filter_CommaSeparatedWithWhitespace_TrimsPatterns()
    {
        var source = new Dictionary<string, string>
        {
            ["app"] = "web",
            ["tier"] = "frontend"
        };
        var result = MetadataFilter.Filter(source, " app , tier ");
        Assert.Equal(2, result.Count);
    }

    // --- Exclusion prefixes ---

    [Fact]
    public void Filter_ExcludesMatchingPrefixes()
    {
        var source = new Dictionary<string, string>
        {
            ["reflector.v1.k8s.emberstack.com/reflects"] = "ns/name",
            ["kubectl.kubernetes.io/last-applied-configuration"] = "{}",
            ["my-annotation"] = "value"
        };
        var result = MetadataFilter.Filter(source, ".*", Annotations.ExcludedAnnotationPrefixes);
        Assert.Single(result);
        Assert.Equal("value", result["my-annotation"]);
    }

    [Fact]
    public void Filter_ExcludesAllReflectorPrefixedAnnotations()
    {
        var source = new Dictionary<string, string>
        {
            ["reflector.v1.k8s.emberstack.com/reflection-allowed"] = "true",
            ["reflector.v1.k8s.emberstack.com/reflected-version"] = "123",
            ["reflector.v1.k8s.emberstack.com/auto-reflects"] = "true",
            ["safe-annotation"] = "keep"
        };
        var result = MetadataFilter.Filter(source, ".*", Annotations.ExcludedAnnotationPrefixes);
        Assert.Single(result);
        Assert.Equal("keep", result["safe-annotation"]);
    }

    [Fact]
    public void Filter_ExcludesArgocdAnnotations()
    {
        var source = new Dictionary<string, string>
        {
            ["argocd.argoproj.io/tracking-id"] = "abc",
            ["argocd.argoproj.io/managed-by"] = "argocd",
            ["my-annotation"] = "value"
        };
        var result = MetadataFilter.Filter(source, ".*", Annotations.ExcludedAnnotationPrefixes);
        Assert.Single(result);
        Assert.Equal("value", result["my-annotation"]);
    }

    [Fact]
    public void Filter_ExcludesDeploymentAnnotations()
    {
        var source = new Dictionary<string, string>
        {
            ["deployment.kubernetes.io/revision"] = "3",
            ["my-annotation"] = "value"
        };
        var result = MetadataFilter.Filter(source, ".*", Annotations.ExcludedAnnotationPrefixes);
        Assert.Single(result);
        Assert.Equal("value", result["my-annotation"]);
    }

    [Fact]
    public void Filter_NoExclusionPrefixes_ReturnsAllMatching()
    {
        var source = new Dictionary<string, string>
        {
            ["reflector.v1.k8s.emberstack.com/reflects"] = "ns/name",
            ["my-annotation"] = "value"
        };
        var result = MetadataFilter.Filter(source, ".*");
        Assert.Equal(2, result.Count);
    }

    // --- Invalid regex ---

    [Fact]
    public void Filter_InvalidRegex_SkipsPatternSilently()
    {
        var source = new Dictionary<string, string>
        {
            ["app"] = "web",
            ["tier"] = "frontend"
        };
        // "[invalid" is broken regex, "tier" is valid
        var result = MetadataFilter.Filter(source, "[invalid,tier");
        Assert.Single(result);
        Assert.Equal("frontend", result["tier"]);
    }

    // --- MergeFiltered ---

    [Fact]
    public void MergeFiltered_NullExisting_CreatesNewDict()
    {
        var source = new Dictionary<string, string> { ["app"] = "web" };
        var result = MetadataFilter.MergeFiltered(null, source, "app");
        Assert.Single(result);
        Assert.Equal("web", result["app"]);
    }

    [Fact]
    public void MergeFiltered_PreservesExistingEntries()
    {
        var existing = new Dictionary<string, string> { ["existing-label"] = "keep" };
        var source = new Dictionary<string, string> { ["app"] = "web" };
        var result = MetadataFilter.MergeFiltered(existing, source, "app");
        Assert.Equal(2, result.Count);
        Assert.Equal("keep", result["existing-label"]);
        Assert.Equal("web", result["app"]);
    }

    [Fact]
    public void MergeFiltered_SourceTakesPrecedence()
    {
        var existing = new Dictionary<string, string> { ["app"] = "old" };
        var source = new Dictionary<string, string> { ["app"] = "new" };
        var result = MetadataFilter.MergeFiltered(existing, source, "app");
        Assert.Single(result);
        Assert.Equal("new", result["app"]);
    }

    [Fact]
    public void MergeFiltered_EmptyFilter_AddsNothing()
    {
        var existing = new Dictionary<string, string> { ["existing"] = "keep" };
        var source = new Dictionary<string, string> { ["app"] = "web" };
        var result = MetadataFilter.MergeFiltered(existing, source, "");
        Assert.Single(result);
        Assert.Equal("keep", result["existing"]);
    }

    [Fact]
    public void MergeFiltered_NullSource_ReturnsExistingCopy()
    {
        var existing = new Dictionary<string, string> { ["existing"] = "keep" };
        var result = MetadataFilter.MergeFiltered(existing, null, ".*");
        Assert.Single(result);
        Assert.Equal("keep", result["existing"]);
    }

    // --- Labels vs Annotations parity ---

    [Fact]
    public void Filter_WorksForLabels_NoExclusions()
    {
        var labels = new Dictionary<string, string>
        {
            ["app.kubernetes.io/name"] = "myapp",
            ["app.kubernetes.io/instance"] = "prod",
            ["helm.sh/chart"] = "myapp-1.0"
        };
        var result = MetadataFilter.Filter(labels, @"app\.kubernetes\.io/.*");
        Assert.Equal(2, result.Count);
        Assert.False(result.ContainsKey("helm.sh/chart"));
    }

    [Fact]
    public void Filter_WorksForAnnotations_WithExclusions()
    {
        var annotations = new Dictionary<string, string>
        {
            ["my-org.io/team"] = "platform",
            ["my-org.io/cost-center"] = "eng",
            ["kubectl.kubernetes.io/last-applied-configuration"] = "{...}",
            ["reflector.v1.k8s.emberstack.com/reflects"] = "ns/name"
        };
        var result = MetadataFilter.Filter(annotations, ".*", Annotations.ExcludedAnnotationPrefixes);
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("my-org.io/team"));
        Assert.True(result.ContainsKey("my-org.io/cost-center"));
    }
}
