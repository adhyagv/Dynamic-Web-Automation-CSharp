namespace DynamicWebAutomation.Core;

/// <summary>
/// Applies the same ancestor-aware visibility rule to browser snapshots and
/// plain-data unit tests, excluding hidden duplicate widgets before scoring.
/// </summary>
public static class PageVisibilityChecker
{
    public static bool IsVisible(IEnumerable<VisibilityNode> elementAndAncestors)
    {
        VisibilityNode[] nodes = elementAndAncestors.ToArray();
        return nodes.Length > 0 && nodes.All(node =>
            !string.Equals(node.Display, "none", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(node.Visibility, "hidden", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(node.Visibility, "collapse", StringComparison.OrdinalIgnoreCase) &&
            node.Opacity > 0 &&
            node.Width > 0 &&
            node.Height > 0);
    }
}

/// <summary>
/// Records the rendered state of one element in the candidate's ancestor chain.
/// </summary>
public sealed record VisibilityNode(
    string Display,
    string Visibility,
    double Opacity,
    double Width,
    double Height);
