// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

namespace Wolfgang.Wms.Core.Logging;

/// <summary>
/// A timed elevation as posted (E12.4): "Debug for 30 minutes".
/// </summary>
/// <param name="Level">Trace, Debug or Information.</param>
/// <param name="Minutes">How long, up to the <c>logging.elevation_max_minutes</c> setting.</param>
public sealed record ElevateLogLevelRequest(string Level, int Minutes);
