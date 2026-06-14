using System;
using System.Collections.Generic;
using System.Linq;
using ECode.Core.IPC.V2;
using ECode.Core.Models;

namespace ECode.Core.Services;

public sealed class BrowserScriptingService
{
    private const string SurfaceRefPrefix = "surface:";
    private readonly Func<IEnumerable<BrowserScriptingSurfaceDescriptor>> _surfaceProvider;
    private readonly Func<string, BrowserScriptingSnapshot?> _snapshotProvider;
    private readonly Dictionary<string, BrowserScriptingRef> _surfaceRefs = new(StringComparer.Ordinal);

    public BrowserScriptingService(
        Func<IEnumerable<BrowserScriptingSurfaceDescriptor>> surfaceProvider,
        Func<string, BrowserScriptingSnapshot?>? snapshotProvider = null)
    {
        _surfaceProvider = surfaceProvider ?? throw new ArgumentNullException(nameof(surfaceProvider));
        _snapshotProvider = snapshotProvider ?? (_ => null);
    }

    public string TrackSurface(BrowserScriptingSurfaceDescriptor surface)
    {
        var surfaceRef = CreateSurfaceRef(surface.SurfaceId);
        _surfaceRefs[surfaceRef] = new BrowserScriptingRef(
            surfaceRef,
            surface.WorkspaceId,
            surface.SurfaceId,
            DateTimeOffset.UtcNow);
        return surfaceRef;
    }

    public BrowserScriptingDiagnostics GetDiagnostics()
    {
        var surfaces = GetCurrentSurfaces();
        return CreateDiagnostics(surfaces, null, null);
    }

    public BrowserScriptingResolveResult ResolveSurfaceRef(string? surfaceRef)
    {
        var surfaces = GetCurrentSurfaces();
        if (!TryParseSurfaceRef(surfaceRef, out var surfaceId))
        {
            return Error(
                V2ErrorCodes.InvalidRef,
                "surfaceRef must use the format surface:<surfaceId>.",
                surfaces,
                surfaceRef,
                null);
        }

        var normalizedRef = CreateSurfaceRef(surfaceId);
        var wasTracked = _surfaceRefs.ContainsKey(normalizedRef);
        var surface = surfaces.FirstOrDefault(item => string.Equals(item.SurfaceId, surfaceId, StringComparison.Ordinal));
        if (surface == null)
        {
            return Error(
                wasTracked ? V2ErrorCodes.StaleRef : V2ErrorCodes.NotFound,
                wasTracked ? $"Surface reference is stale: {normalizedRef}" : $"Browser surface not found: {surfaceId}",
                surfaces,
                normalizedRef,
                surfaceId);
        }

        if (surface.Kind != SurfaceKind.Browser)
        {
            return Error(
                V2ErrorCodes.NotSupported,
                $"Surface is not a browser surface: {surfaceId}",
                surfaces,
                normalizedRef,
                surfaceId);
        }

        TrackSurface(surface);
        return new BrowserScriptingResolveResult(
            Success: true,
            Surface: surface,
            Error: null,
            Diagnostics: CreateDiagnostics(surfaces, normalizedRef, surfaceId));
    }

    public BrowserScriptingSnapshotResult GetSnapshot(string? surfaceRef)
    {
        var resolved = ResolveSurfaceRef(surfaceRef);
        if (!resolved.Success)
        {
            return new BrowserScriptingSnapshotResult(
                Success: false,
                Snapshot: null,
                Error: resolved.Error,
                Diagnostics: resolved.Diagnostics);
        }

        var snapshot = _snapshotProvider(resolved.Surface!.SurfaceId);
        if (snapshot == null)
        {
            return new BrowserScriptingSnapshotResult(
                Success: false,
                Snapshot: null,
                Error: new V2Error(V2ErrorCodes.NotFound, $"Browser snapshot not available: {resolved.Surface.SurfaceId}"),
                Diagnostics: resolved.Diagnostics);
        }

        return new BrowserScriptingSnapshotResult(
            Success: true,
            Snapshot: snapshot,
            Error: null,
            Diagnostics: resolved.Diagnostics);
    }

    public BrowserScriptingLocatorResult FindByRole(string? surfaceRef, string role, string? name = null)
    {
        return Find(surfaceRef, BrowserScriptingLocator.Role(role, name));
    }

    public BrowserScriptingLocatorResult FindByText(string? surfaceRef, string text)
    {
        return Find(surfaceRef, BrowserScriptingLocator.Text(text));
    }

    public BrowserScriptingLocatorResult FindByTestId(string? surfaceRef, string testId)
    {
        return Find(surfaceRef, BrowserScriptingLocator.TestId(testId));
    }

    public BrowserScriptingLocatorResult FindFirst(string? surfaceRef, BrowserScriptingLocator locator)
    {
        return Find(surfaceRef, BrowserScriptingLocator.First(locator));
    }

    public BrowserScriptingLocatorResult FindLast(string? surfaceRef, BrowserScriptingLocator locator)
    {
        return Find(surfaceRef, BrowserScriptingLocator.Last(locator));
    }

    public BrowserScriptingLocatorResult FindNth(string? surfaceRef, BrowserScriptingLocator locator, int index)
    {
        return Find(surfaceRef, BrowserScriptingLocator.Nth(locator, index));
    }

    public BrowserScriptingLocatorResult Find(string? surfaceRef, BrowserScriptingLocator locator)
    {
        var snapshotResult = GetSnapshot(surfaceRef);
        if (!snapshotResult.Success)
        {
            return new BrowserScriptingLocatorResult(
                Success: false,
                Nodes: [],
                Error: snapshotResult.Error,
                Diagnostics: snapshotResult.Diagnostics);
        }

        var nodes = EvaluateLocator(snapshotResult.Snapshot!, locator);
        if (locator.Kind is BrowserScriptingLocatorKind.First or BrowserScriptingLocatorKind.Last or BrowserScriptingLocatorKind.Nth &&
            nodes.Count == 0)
        {
            return new BrowserScriptingLocatorResult(
                Success: false,
                Nodes: [],
                Error: new V2Error(V2ErrorCodes.NotFound, "Locator did not match any node."),
                Diagnostics: snapshotResult.Diagnostics);
        }

        return new BrowserScriptingLocatorResult(
            Success: true,
            Nodes: nodes,
            Error: null,
            Diagnostics: snapshotResult.Diagnostics);
    }

    public static string CreateSurfaceRef(string surfaceId)
    {
        return SurfaceRefPrefix + surfaceId;
    }

    private static bool TryParseSurfaceRef(string? surfaceRef, out string surfaceId)
    {
        surfaceId = "";
        if (string.IsNullOrWhiteSpace(surfaceRef) ||
            !surfaceRef.StartsWith(SurfaceRefPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        surfaceId = surfaceRef[SurfaceRefPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(surfaceId);
    }

    private IReadOnlyList<BrowserScriptingSurfaceDescriptor> GetCurrentSurfaces()
    {
        return _surfaceProvider()
            .Where(surface => !string.IsNullOrWhiteSpace(surface.SurfaceId))
            .ToList();
    }

    private BrowserScriptingResolveResult Error(
        string code,
        string message,
        IReadOnlyList<BrowserScriptingSurfaceDescriptor> surfaces,
        string? surfaceRef,
        string? surfaceId)
    {
        return new BrowserScriptingResolveResult(
            Success: false,
            Surface: null,
            Error: new V2Error(code, message),
            Diagnostics: CreateDiagnostics(surfaces, surfaceRef, surfaceId));
    }

    private BrowserScriptingDiagnostics CreateDiagnostics(
        IReadOnlyList<BrowserScriptingSurfaceDescriptor> surfaces,
        string? surfaceRef,
        string? surfaceId)
    {
        return new BrowserScriptingDiagnostics(
            LiveSurfaceCount: surfaces.Count,
            LiveBrowserSurfaceCount: surfaces.Count(surface => surface.Kind == SurfaceKind.Browser),
            RegisteredRefCount: _surfaceRefs.Count,
            SurfaceRef: surfaceRef,
            SurfaceId: surfaceId);
    }

    private static IReadOnlyList<BrowserScriptingNode> EvaluateLocator(
        BrowserScriptingSnapshot snapshot,
        BrowserScriptingLocator locator)
    {
        var nodes = Flatten(snapshot.Root)
            .Where(node => node.Visible)
            .ToList();

        return locator.Kind switch
        {
            BrowserScriptingLocatorKind.Role => nodes
                .Where(node => EqualsIgnoreCase(node.Role, locator.Value))
                .Where(node => string.IsNullOrWhiteSpace(locator.Name) || EqualsIgnoreCase(node.Name, locator.Name))
                .ToList(),
            BrowserScriptingLocatorKind.Text => nodes
                .Where(node => ContainsIgnoreCase(node.Text, locator.Value) || ContainsIgnoreCase(node.Name, locator.Value))
                .ToList(),
            BrowserScriptingLocatorKind.TestId => nodes
                .Where(node => string.Equals(node.TestId, locator.Value, StringComparison.Ordinal))
                .ToList(),
            BrowserScriptingLocatorKind.First => EvaluateNested(snapshot, locator).Take(1).ToList(),
            BrowserScriptingLocatorKind.Last => EvaluateNested(snapshot, locator).TakeLast(1).ToList(),
            BrowserScriptingLocatorKind.Nth => EvaluateNested(snapshot, locator).Skip(Math.Max(0, locator.Index)).Take(1).ToList(),
            _ => [],
        };
    }

    private static IReadOnlyList<BrowserScriptingNode> EvaluateNested(
        BrowserScriptingSnapshot snapshot,
        BrowserScriptingLocator locator)
    {
        return locator.Inner == null ? [] : EvaluateLocator(snapshot, locator.Inner);
    }

    private static IEnumerable<BrowserScriptingNode> Flatten(BrowserScriptingNode node)
    {
        yield return node;

        foreach (var child in node.Children)
        {
            foreach (var nested in Flatten(child))
                yield return nested;
        }
    }

    private static bool EqualsIgnoreCase(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsIgnoreCase(string? haystack, string? needle)
    {
        return !string.IsNullOrWhiteSpace(haystack) &&
               !string.IsNullOrWhiteSpace(needle) &&
               haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BrowserScriptingSurfaceDescriptor(
    string WorkspaceId,
    string WorkspaceName,
    string SurfaceId,
    string SurfaceName,
    SurfaceKind Kind,
    string? Url,
    string? Title);

public sealed record BrowserScriptingRef(
    string Value,
    string WorkspaceId,
    string SurfaceId,
    DateTimeOffset CreatedAtUtc);

public sealed record BrowserScriptingDiagnostics(
    int LiveSurfaceCount,
    int LiveBrowserSurfaceCount,
    int RegisteredRefCount,
    string? SurfaceRef,
    string? SurfaceId);

public sealed record BrowserScriptingResolveResult(
    bool Success,
    BrowserScriptingSurfaceDescriptor? Surface,
    V2Error? Error,
    BrowserScriptingDiagnostics Diagnostics);

public sealed record BrowserScriptingSnapshot(BrowserScriptingNode Root);

public sealed record BrowserScriptingNode
{
    public string NodeId { get; init; } = Guid.NewGuid().ToString();
    public string Role { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public string? TestId { get; init; }
    public bool Visible { get; init; } = true;
    public IReadOnlyList<BrowserScriptingNode> Children { get; init; } = [];
}

public enum BrowserScriptingLocatorKind
{
    Role,
    Text,
    TestId,
    First,
    Last,
    Nth,
}

public sealed record BrowserScriptingLocator(
    BrowserScriptingLocatorKind Kind,
    string? Value = null,
    string? Name = null,
    BrowserScriptingLocator? Inner = null,
    int Index = 0)
{
    public static BrowserScriptingLocator Role(string role, string? name = null) =>
        new(BrowserScriptingLocatorKind.Role, role, name);

    public static BrowserScriptingLocator Text(string text) =>
        new(BrowserScriptingLocatorKind.Text, text);

    public static BrowserScriptingLocator TestId(string testId) =>
        new(BrowserScriptingLocatorKind.TestId, testId);

    public static BrowserScriptingLocator First(BrowserScriptingLocator inner) =>
        new(BrowserScriptingLocatorKind.First, Inner: inner);

    public static BrowserScriptingLocator Last(BrowserScriptingLocator inner) =>
        new(BrowserScriptingLocatorKind.Last, Inner: inner);

    public static BrowserScriptingLocator Nth(BrowserScriptingLocator inner, int index) =>
        new(BrowserScriptingLocatorKind.Nth, Inner: inner, Index: index);
}

public sealed record BrowserScriptingSnapshotResult(
    bool Success,
    BrowserScriptingSnapshot? Snapshot,
    V2Error? Error,
    BrowserScriptingDiagnostics Diagnostics);

public sealed record BrowserScriptingLocatorResult(
    bool Success,
    IReadOnlyList<BrowserScriptingNode> Nodes,
    V2Error? Error,
    BrowserScriptingDiagnostics Diagnostics);
