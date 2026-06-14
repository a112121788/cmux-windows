using System;
using System.Collections.Generic;
using System.Linq;
using ECode.Core.Models;
using ECode.Core.Services;
using ECode.ViewModels;
using CoreBrowserScriptingService = ECode.Core.Services.BrowserScriptingService;

namespace ECode.Services;

public sealed class BrowserScriptingService
{
    private readonly Func<IEnumerable<WorkspaceViewModel>> _workspaceProvider;
    private readonly CoreBrowserScriptingService _core;

    public BrowserScriptingService(
        Func<IEnumerable<WorkspaceViewModel>> workspaceProvider,
        Func<string, BrowserScriptingSnapshot?>? snapshotProvider = null)
    {
        _workspaceProvider = workspaceProvider ?? throw new ArgumentNullException(nameof(workspaceProvider));
        _core = new CoreBrowserScriptingService(GetSurfaceDescriptors, snapshotProvider);
    }

    public BrowserScriptingResolveResult ResolveSurfaceRef(string? surfaceRef)
    {
        return _core.ResolveSurfaceRef(surfaceRef);
    }

    public BrowserScriptingDiagnostics GetDiagnostics()
    {
        return _core.GetDiagnostics();
    }

    public string TrackSurface(WorkspaceViewModel workspace, SurfaceViewModel surface)
    {
        return _core.TrackSurface(ToDescriptor(workspace, surface));
    }

    public BrowserScriptingSnapshotResult GetSnapshot(string? surfaceRef)
    {
        return _core.GetSnapshot(surfaceRef);
    }

    public BrowserScriptingLocatorResult FindByRole(string? surfaceRef, string role, string? name = null)
    {
        return _core.FindByRole(surfaceRef, role, name);
    }

    public BrowserScriptingLocatorResult FindByText(string? surfaceRef, string text)
    {
        return _core.FindByText(surfaceRef, text);
    }

    public BrowserScriptingLocatorResult FindByTestId(string? surfaceRef, string testId)
    {
        return _core.FindByTestId(surfaceRef, testId);
    }

    public BrowserScriptingLocatorResult FindFirst(string? surfaceRef, BrowserScriptingLocator locator)
    {
        return _core.FindFirst(surfaceRef, locator);
    }

    public BrowserScriptingLocatorResult FindLast(string? surfaceRef, BrowserScriptingLocator locator)
    {
        return _core.FindLast(surfaceRef, locator);
    }

    public BrowserScriptingLocatorResult FindNth(string? surfaceRef, BrowserScriptingLocator locator, int index)
    {
        return _core.FindNth(surfaceRef, locator, index);
    }

    private IEnumerable<BrowserScriptingSurfaceDescriptor> GetSurfaceDescriptors()
    {
        return _workspaceProvider()
            .SelectMany(workspace => workspace.Surfaces.Select(surface => ToDescriptor(workspace, surface)));
    }

    private static BrowserScriptingSurfaceDescriptor ToDescriptor(WorkspaceViewModel workspace, SurfaceViewModel surface)
    {
        return new BrowserScriptingSurfaceDescriptor(
            WorkspaceId: workspace.Workspace.Id,
            WorkspaceName: workspace.Name,
            SurfaceId: surface.Surface.Id,
            SurfaceName: surface.Name,
            Kind: surface.Surface.Kind,
            Url: surface.Surface.BrowserUrl,
            Title: surface.Surface.BrowserTitle);
    }
}
