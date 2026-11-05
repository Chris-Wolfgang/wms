// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.AspNetCore.DataProtection;
using Wolfgang.Wms.Core.Identity.BreakGlass;
using Wolfgang.Wms.Core.Identity.BreakGlass.AdminChannel;

namespace Wolfgang.Wms.AotSmoke;

/// <summary>
/// The admin channel's messages go through source-generated JSON (<see cref="AdminChannelJsonContext"/>) on both
/// ends; under NativeAOT a missing <c>[JsonSerializable]</c> would surface only at run time, so a request and a
/// response round-trip here.
/// </summary>
internal static class AdminChannelSmoke
{
    public static void SealedRequestAndResponseRoundTrip()
    {
        var protector = new EphemeralDataProtectionProvider().CreateProtector(AdminChannelProtocol.Purpose);
        var issued = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var request = new AdminChannelRequest(AdminChannelCommands.Unlock, 15, "HOST\\ops", issued);

        var opened = AdminChannelProtocol.Open(AdminChannelProtocol.Seal(request, protector), protector, issued.AddSeconds(5));
        Smoke.SequenceEqual([request.Command, request.Minutes!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), request.OsUser], [opened.Command, opened.Minutes!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), opened.OsUser]);
        if (opened.IssuedAt != issued)
        {
            throw new SmokeFailureException("the issued-at timestamp did not survive the round trip");
        }

        var response = new AdminChannelResponse(Ok: true, "done", new LocalLoginStatus(true, true, issued.AddMinutes(15), false));
        var decoded = AdminChannelProtocol.Decode(AdminChannelProtocol.Encode(response)) ?? throw new SmokeFailureException("the response did not decode");
        Smoke.SequenceEqual([response.Message, response.Status!.UnlockedUntil!.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture)], [decoded.Message, decoded.Status!.UnlockedUntil!.Value.ToString("O", System.Globalization.CultureInfo.InvariantCulture)]);

        Smoke.Throws<InvalidOperationException>(() => AdminChannelProtocol.Open(protector.Protect("not a message"), protector, issued), "not a channel message");
    }
}
