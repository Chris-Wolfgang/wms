// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json.Serialization;

namespace Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

/// <summary>
/// Source-generated JSON for the channel messages, so the host and the tool stay AOT-clean (E1.9).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AdminChannelRequest))]
[JsonSerializable(typeof(AdminChannelResponse))]
public sealed partial class AdminChannelJsonContext : JsonSerializerContext
{
}
