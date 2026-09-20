// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using System.Text.Json;
using System.Text.Json.Serialization;
using Wolfgang.Wms.Core.Schema;
using Wolfgang.Wms.Core.Settings;

namespace Wolfgang.Wms.Core.Json;

/// <summary>
/// Source-generated JSON metadata for every record the API sends or receives (E1.9, E1.14): camelCase,
/// no reflection, so the host stays trim- and AOT-clean. Add a <c>[JsonSerializable]</c> line for each new
/// API record; the host inserts this context first in its resolver chain.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SchemaStatus))]
[JsonSerializable(typeof(List<SettingDescriptor>))]
public sealed partial class WmsJsonContext : JsonSerializerContext
{
}
