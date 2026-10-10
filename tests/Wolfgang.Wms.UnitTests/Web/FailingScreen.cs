// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Wolfgang.Wms.UnitTests.Web;

/// <summary>
/// A screen that throws the first time it renders and renders a marker afterwards: the body a
/// <c>WorkspaceLayout</c> error boundary must contain and then recover.
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
