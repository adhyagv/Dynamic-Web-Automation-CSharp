using DynamicWebAutomation.Core;

namespace DynamicWebAutomation.Tests;

/// <summary>
/// Tests the reusable visibility rule with plain rendered-state snapshots.
/// </summary>
public sealed class PageVisibilityCheckerTests
{
    [Fact]
    public void VisibleCandidateAndAncestorsRemainEligible()
    {
        Assert.True(PageVisibilityChecker.IsVisible(
        [
            Node(width: 140, height: 24),
            Node(width: 500, height: 400),
            Node(width: 1200, height: 800)
        ]));
    }

    [Fact]
    public void HiddenCandidateIsExcluded()
    {
        Assert.False(PageVisibilityChecker.IsVisible(
        [
            Node(visibility: "hidden"),
            Node()
        ]));
    }

    [Fact]
    public void VisibleDuplicateInsideHiddenAncestorIsExcluded()
    {
        Assert.False(PageVisibilityChecker.IsVisible(
        [
            Node(),
            Node(display: "none")
        ]));
    }

    [Fact]
    public void CollapsedAncestorIsExcluded()
    {
        Assert.False(PageVisibilityChecker.IsVisible(
        [
            Node(),
            Node(visibility: "collapse")
        ]));
    }

    [Fact]
    public void ZeroSizedAncestorIsExcluded()
    {
        Assert.False(PageVisibilityChecker.IsVisible(
        [
            Node(),
            Node(width: 0, height: 20)
        ]));
    }

    [Fact]
    public void EmptySnapshotIsNotVisible()
    {
        Assert.False(PageVisibilityChecker.IsVisible([]));
    }

    private static VisibilityNode Node(
        string display = "block",
        string visibility = "visible",
        double opacity = 1,
        double width = 100,
        double height = 20) =>
        new(display, visibility, opacity, width, height);
}
