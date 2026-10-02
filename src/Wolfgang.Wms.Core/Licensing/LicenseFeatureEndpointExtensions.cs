// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Http;
using Wolfgang.Wms.Domain.Keys;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// The feature edge (E79.2, E79.4): an endpoint behind a paid feature declares it once, and a tier without
/// the feature gets <c>403 license.feature_not_licensed</c> naming the feature and the tier. The check runs
/// after authorization, so an unauthenticated caller still sees 401.
/// </summary>
public static class LicenseFeatureEndpointExtensions
{
    /// <summary>
    /// Refuses the endpoint unless the license grants <paramref name="feature"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static TBuilder RequireLicenseFeature<TBuilder>(this TBuilder builder, LicenseFeature feature)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(feature);

        builder.Add(endpoint => endpoint.Metadata.Add(feature));
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var license = context.HttpContext.RequestServices.GetRequiredService<ILicense>();
            return license.HasFeature(feature)
                ? await next(context).ConfigureAwait(false)
                : ApiProblems.Problem(LicenseErrorCodes.FeatureNotLicensed, detail: null, feature.Name, license.Current.Tier.Name);
        });
    }
}
