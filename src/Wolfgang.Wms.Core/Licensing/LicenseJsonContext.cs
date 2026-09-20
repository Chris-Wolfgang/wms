// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Licensing;

/// <summary>
/// Source-generated JSON for key documents (E79.3): snake_case names, nulls omitted, no reflection.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LicenseKeyPayload))]
[JsonSerializable(typeof(LicenseKeyEnvelope))]
internal sealed partial class LicenseJsonContext : JsonSerializerContext
{
}
