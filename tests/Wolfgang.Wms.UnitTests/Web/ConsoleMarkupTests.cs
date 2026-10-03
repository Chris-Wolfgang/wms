// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Wolfgang.Wms.Web.Shared;
using Wolfgang.Wms.Web.Shared.Components;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// The markup check promised by the localization convention (E1.14, E82.4): UI text comes from
/// <c>IStringLocalizer</c>, never a literal in a component. Every console component is rendered with a localizer
/// that marks each string it returns as <c>⟦key⟧</c>; any letters left in a text node or a user-facing attribute
/// outside a marker are a literal in the component, and the test names the component and the text.
/// </summary>
public sealed class ConsoleMarkupTests : IDisposable
{
    private static readonly string[] UserFacingAttributes = ["aria-label", "placeholder", "title", "alt"];

    private static readonly Regex Marker = new("⟦[^⟦⟧]*⟧", RegexOptions.CultureInvariant);

    private readonly BunitContext _context = new();



    public ConsoleMarkupTests()
    {
        _context.Services.AddSingleton<IStringLocalizerFactory, MarkingLocalizerFactory>();
        _context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
    }



    public static TheoryData<string> Pages => new()
    {
        "configure",
        "supervise",
        "resolve",
        "report",
        "insights",
        "home",
        "error",
        "not-found",
        "main-layout",
        "reconnect-modal",
        "scan-listener",
        "workspace-nav",
    };



    [Theory]
    [MemberData(nameof(Pages))]
    public void Console_component_text_comes_from_the_localizer(string page)
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));

        var nodes = Render(page);

        Assert.Empty(Literals(nodes));
        Assert.Contains("⟦", string.Concat(nodes.Select(n => n.TextContent)) + string.Concat(Attributes(nodes)), StringComparison.Ordinal);
    }



    [Theory]
    [InlineData(WorkspaceAccessResult.Allowed)]
    [InlineData(WorkspaceAccessResult.NotLicensed)]
    [InlineData(WorkspaceAccessResult.NotPermitted)]
    public void Workspace_layout_text_comes_from_the_localizer_in_every_state(WorkspaceAccessResult access)
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(access));

        var layout = _context.Render<MarkupTestLayout>(p => p.Add(x => x.Body, (RenderFragment)(_ => { })));

        Assert.Empty(Literals(layout.Nodes));
    }



    [Fact]
    public void Workspace_layout_text_comes_from_the_localizer_while_access_is_pending()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new PendingAccess());

        var layout = _context.Render<MarkupTestLayout>(p => p.Add(x => x.Body, (RenderFragment)(_ => { })));

        Assert.Equal("⟦layout.loading⟧", layout.Find(".workspace-loading").TextContent);
        Assert.Empty(Literals(layout.Nodes));
    }



    [Fact]
    public void The_check_catches_a_literal()
    {
        var nodes = _context.Render<LiteralSample>().Nodes;

        Assert.Equal(["text: Not localized", "aria-label: Close"], Literals(nodes));
    }



    [Fact]
    public void The_marking_localizer_marks_lookups_by_name_and_lists_no_strings()
    {
        var localizer = new MarkingLocalizerFactory().Create("ConsoleText", "Wolfgang.Wms.Web.Shared");

        Assert.Equal("⟦scan.label⟧", localizer["scan.label"].Value);
        Assert.Empty(localizer.GetAllStrings(includeParentCultures: true));
    }



    public void Dispose()
    {
        _context.Dispose();
    }



    private static List<string> Literals(INodeList nodes)
    {
        var texts = AllNodes(nodes)
            .OfType<IText>()
            .Select(t => t.Data)
            .Where(HasUnmarkedLetters)
            .Select(t => "text: " + t.Trim());
        var attributes = AllNodes(nodes)
            .OfType<IElement>()
            .SelectMany(e => e.Attributes.Where(a => UserFacingAttributes.Contains(a.Name, StringComparer.Ordinal)))
            .Where(a => HasUnmarkedLetters(a.Value))
            .Select(a => a.Name + ": " + a.Value);
        return texts.Concat(attributes).ToList();
    }



    private static IEnumerable<INode> AllNodes(INodeList nodes)
    {
        return nodes.SelectMany(n => new[] { n }.Concat(n.GetDescendants()));
    }



    private static IEnumerable<string> Attributes(INodeList nodes)
    {
        return AllNodes(nodes)
            .OfType<IElement>()
            .SelectMany(e => e.Attributes)
            .Select(a => a.Value);
    }



    private static bool HasUnmarkedLetters(string text)
    {
        var rest = text;
        string previous;
        do
        {
            previous = rest;
            rest = Marker.Replace(rest, string.Empty);
        }
        while (!string.Equals(previous, rest, StringComparison.Ordinal));

        return rest.Any(char.IsLetter);
    }



    private INodeList Render(string page)
    {
        return page switch
        {
            "configure" => _context.Render<global::Wolfgang.Wms.Web.Configure.Pages.ConfigureHome>().Nodes,
            "supervise" => _context.Render<global::Wolfgang.Wms.Web.Supervise.Pages.SuperviseHome>().Nodes,
            "resolve" => _context.Render<global::Wolfgang.Wms.Web.Resolve.Pages.ResolveHome>().Nodes,
            "report" => _context.Render<global::Wolfgang.Wms.Web.Report.Pages.ReportHome>().Nodes,
            "insights" => _context.Render<global::Wolfgang.Wms.Web.Insights.Pages.InsightsHome>().Nodes,
            "home" => _context.Render<global::Wolfgang.Wms.Web.Components.Pages.Home>().Nodes,
            "error" => _context.Render<global::Wolfgang.Wms.Web.Components.Pages.Error>().Nodes,
            "not-found" => _context.Render<global::Wolfgang.Wms.Web.Components.Pages.NotFound>().Nodes,
            "main-layout" => _context.Render<global::Wolfgang.Wms.Web.Components.Layout.MainLayout>(p => p.Add(x => x.Body, (RenderFragment)(_ => { }))).Nodes,
            "reconnect-modal" => _context.Render<global::Wolfgang.Wms.Web.Components.Layout.ReconnectModal>().Nodes,
            "scan-listener" => _context.Render<ScanListener>().Nodes,
            _ => _context.Render<WorkspaceNav>().Nodes,
        };
    }



    private sealed class MarkupTestLayout : WorkspaceLayout
    {
        protected override Workspace Workspace => Workspaces.Configure;
    }



    private sealed class FixedAccess(WorkspaceAccessResult result) : IWorkspaceAccess
    {
        public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            return Task.FromResult(result);
        }
    }



    private sealed class PendingAccess : IWorkspaceAccess
    {
        public Task<WorkspaceAccessResult> CheckAsync(Workspace workspace, CancellationToken cancellationToken)
        {
            return new TaskCompletionSource<WorkspaceAccessResult>().Task;
        }
    }



    private sealed class LiteralSample : ComponentBase
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddContent(1, "Not localized");
            builder.CloseElement();
            builder.OpenElement(2, "button");
            builder.AddAttribute(3, "aria-label", "Close");
            builder.AddContent(4, "⟦close⟧");
            builder.CloseElement();
        }
    }



    /// <summary>
    /// Returns every string as <c>⟦key⟧</c>, or <c>⟦key: args⟧</c> when formatted, so the check can tell
    /// localized text from literals.
    /// </summary>
    private sealed class MarkingLocalizerFactory : IStringLocalizerFactory
    {
        public IStringLocalizer Create(Type resourceSource)
        {
            return new MarkingLocalizer();
        }



        public IStringLocalizer Create(string baseName, string location)
        {
            return new MarkingLocalizer();
        }
    }



    private sealed class MarkingLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, "⟦" + name + "⟧");

        public LocalizedString this[string name, params object[] arguments] => new(name, "⟦" + name + ": " + string.Join(", ", arguments) + "⟧");

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }
}
