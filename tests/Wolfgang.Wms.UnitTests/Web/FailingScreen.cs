// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// A screen that throws the first time it renders and renders a marker afterwards. It stands in for the body of
/// a <c>WorkspaceLayout</c>: the layout's error boundary must contain the first render's failure (the chrome and
/// the scanner stay up) and, on Retry, render the screen again so the marker appears.
/// </summary>
public sealed class FailingScreen : ComponentBase
{
    /// <summary>
    /// How many renders were attempted; the first one throws.
    /// </summary>
    [Parameter]
    public Counter Attempts { get; set; } = new();



    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (Attempts.Count++ == 0)
        {
            throw new InvalidOperationException("The screen failed to render.");
        }

        builder.AddMarkupContent(0, "<p class=\"screen\">⟦screen.ok⟧</p>");
    }



    /// <summary>
    /// A render counter shared between the test and the screen.
    /// </summary>
    public sealed class Counter
    {
        /// <summary>
        /// Renders attempted so far.
        /// </summary>
        public int Count { get; set; }
    }
}
