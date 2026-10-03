// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Reflection;
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

    private static readonly Regex Marker = new("⟦[^⟦⟧]*⟧", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly BunitContext _context = new();



    public ConsoleMarkupTests()
    {
        _context.Services.AddSingleton<IStringLocalizerFactory, MarkingLocalizerFactory>();
        _context.Services.AddSingleton(typeof(IStringLocalizer<>), typeof(StringLocalizer<>));
    }



    /// <summary>
    /// Every component the console ships, rendered by <see cref="Console_component_text_comes_from_the_localizer"/>.
    /// <see cref="Every_console_component_is_checked_or_excluded_with_a_reason"/> fails when one is added
    /// without being listed here or in <see cref="Excluded"/>.
    /// </summary>
    public static TheoryData<Type> Components => [.. ComponentTypes];



    private static Type[] ComponentTypes =>
    [
        typeof(global::Wolfgang.Wms.Web.Configure.Pages.ConfigureHome),
        typeof(global::Wolfgang.Wms.Web.Supervise.Pages.SuperviseHome),
        typeof(global::Wolfgang.Wms.Web.Resolve.Pages.ResolveHome),
        typeof(global::Wolfgang.Wms.Web.Report.Pages.ReportHome),
        typeof(global::Wolfgang.Wms.Web.Insights.Pages.InsightsHome),
        typeof(global::Wolfgang.Wms.Web.Configure.ConfigureLayout),
        typeof(global::Wolfgang.Wms.Web.Supervise.SuperviseLayout),
        typeof(global::Wolfgang.Wms.Web.Resolve.ResolveLayout),
        typeof(global::Wolfgang.Wms.Web.Report.ReportLayout),
        typeof(global::Wolfgang.Wms.Web.Insights.InsightsLayout),
        typeof(global::Wolfgang.Wms.Web.Components.Pages.Home),
        typeof(global::Wolfgang.Wms.Web.Components.Pages.Error),
        typeof(global::Wolfgang.Wms.Web.Components.Pages.NotFound),
        typeof(global::Wolfgang.Wms.Web.Components.Layout.MainLayout),
        typeof(global::Wolfgang.Wms.Web.Components.Layout.ReconnectModal),
        typeof(global::Wolfgang.Wms.Web.Components.Routes),
        typeof(ScanListener),
        typeof(WorkspaceNav),
    ];



    /// <summary>
    /// Console components the markup check does not render, each with the reason.
    /// </summary>
    private static Dictionary<Type, string> Excluded => new()
    {
        [typeof(global::Wolfgang.Wms.Web.Components.App)] =
            "The document shell (html, head, body, scripts) has no text of its own; it needs the host's endpoint and "
            + "static-asset services to render, and ConsoleLocalizationTests checks its one culture-dependent "
            + "attribute (<html lang>) against the running host.",
    };



    [Theory]
    [MemberData(nameof(Components))]
    public void Console_component_text_comes_from_the_localizer(Type component)
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(WorkspaceAccessResult.Allowed));

        var nodes = Render(component);

        Assert.Empty(Literals(nodes));
        Assert.Contains("⟦", string.Concat(nodes.Select(n => n.TextContent)) + string.Concat(Attributes(nodes)), StringComparison.Ordinal);
    }



    [Fact]
    public void Every_console_component_is_checked_or_excluded_with_a_reason()
    {
        Assembly[] console =
        [
            typeof(global::Wolfgang.Wms.Web.Components.App).Assembly,
            typeof(WorkspaceLayout).Assembly,
            .. global::Wolfgang.Wms.Web.WorkspaceAssemblies.All,
        ];
        var shipped = console
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested && !string.Equals(t.Name, "_Imports", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .Order(StringComparer.Ordinal);
        var accounted = ComponentTypes
            .Select(t => t.FullName!)
            .Concat(Excluded.Keys.Select(t => t.FullName!))
            .Order(StringComparer.Ordinal);

        Assert.Equal(shipped, accounted);
        Assert.All(Excluded.Values, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }



    [Theory]
    [InlineData(WorkspaceAccessResult.Allowed)]
    [InlineData(WorkspaceAccessResult.NotLicensed)]
    [InlineData(WorkspaceAccessResult.NotPermitted)]
    public void Workspace_layout_text_comes_from_the_localizer_in_every_state(WorkspaceAccessResult access)
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new FixedAccess(access));

        var nodes = Render(typeof(global::Wolfgang.Wms.Web.Configure.ConfigureLayout));

        Assert.Empty(Literals(nodes));
    }



    [Fact]
    public void Workspace_layout_text_comes_from_the_localizer_while_access_is_pending()
    {
        _context.Services.AddSingleton<IWorkspaceAccess>(new PendingAccess());

        var nodes = Render(typeof(global::Wolfgang.Wms.Web.Configure.ConfigureLayout));

        Assert.Equal("⟦layout.loading⟧", AllNodes(nodes).OfType<IElement>().Single(e => e.ClassList.Contains("workspace-loading")).TextContent);
        Assert.Empty(Literals(nodes));
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



    private INodeList Render(Type component)
    {
        var isLayout = typeof(LayoutComponentBase).IsAssignableFrom(component);
        return _context.Render(builder =>
        {
            builder.OpenComponent(0, component);
            if (isLayout)
            {
                builder.AddComponentParameter(1, nameof(LayoutComponentBase.Body), (RenderFragment)(_ => { }));
            }

            builder.CloseComponent();
        }).Nodes;
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
            // Never completes during the test: the layout stays in its loading state.
            return Task
                .Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                .ContinueWith(_ => WorkspaceAccessResult.Allowed, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
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
