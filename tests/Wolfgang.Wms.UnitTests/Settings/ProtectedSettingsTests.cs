// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Wolfgang.Wms.Core.Secrets;
using Wolfgang.Wms.Core.Settings;
using Wolfgang.Wms.Domain.Keys;
using Wolfgang.Wms.Domain.Settings;

namespace Wolfgang.Wms.UnitTests.Settings;

/// <summary>
/// E8.3 as a decorator: a secret written through <see cref="ProtectedSettings"/> is validated in the clear
/// and stored protected, a typed read reveals it, a default passes through in the clear, and every other key
/// and operation reaches the store untouched.
/// </summary>
public sealed class ProtectedSettingsTests
{
    private static readonly SettingKey<int> MaxTotes = new("picking.max_totes", 3, "Totes a picker may carry.");
    private static readonly SettingKey<SecretText> Password = new("smtp.password", new SecretText("s3cret"), "SMTP password.") { Scopes = SettingScopes.Organization, Validator = v => v.Value.Length <= 8 ? null : "at most 8 characters" };
    private static readonly SettingRegistry Registry = new([MaxTotes, Password]);
    private static readonly SettingScopeRef Organization = SettingScopeRef.Organization;

    private readonly InMemorySettings _store = new(Registry);
    private readonly ProtectedSettings _settings;



    public ProtectedSettingsTests()
    {
        _settings = new ProtectedSettings(_store, Registry, new FakeProtector());
    }



    [Fact]
    public async Task SetAsync_with_a_secret_stores_protected_text_and_GetAsync_reveals_it()
    {
        var written = await _settings.SetAsync(Password, Organization, new SecretText("hunter2"), "alice", CancellationToken.None);
        var stored = _store.Rows[(Organization, Password.Name)];
        var read = await _settings.GetAsync(Password, Organization, CancellationToken.None);

        Assert.StartsWith(ProtectedText.Prefix, stored, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", stored, StringComparison.Ordinal);
        Assert.Equal("hunter2", read.Value);
        Assert.Equal("••••••", written.ConfiguredValue);
    }



    [Fact]
    public async Task SetTextAsync_with_a_secret_validates_the_clear_text_then_protects()
    {
        var tooLong = await Assert.ThrowsAsync<SettingException>(() => _settings.SetTextAsync(Password.Name, Organization, "longer than eight", "alice", CancellationToken.None));
        var wrongScope = await Assert.ThrowsAsync<SettingException>(() => _settings.SetTextAsync(Password.Name, SettingScopeRef.Site(1), "short", "alice", CancellationToken.None));
        await _settings.SetTextAsync(Password.Name, Organization, "short", "alice", CancellationToken.None);

        Assert.Equal(SettingErrorCodes.InvalidValue, tooLong.Code);
        Assert.Equal(SettingErrorCodes.ScopeNotAllowed, wrongScope.Code);
        Assert.Equal(1, _store.Writes);
        Assert.StartsWith(ProtectedText.Prefix, _store.Rows[(Organization, Password.Name)], StringComparison.Ordinal);
    }



    [Fact]
    public async Task GetAsync_of_an_unset_secret_returns_the_default_in_the_clear()
    {
        var read = await _settings.GetAsync(Password, Organization, CancellationToken.None);

        Assert.Equal("s3cret", read.Value);
    }



    [Fact]
    public async Task Other_kinds_and_operations_pass_through_untouched()
    {
        await _settings.SetAsync(MaxTotes, Organization, 4, "alice", CancellationToken.None);
        await _settings.SetTextAsync(MaxTotes.Name, Organization, "5", "alice", CancellationToken.None);
        await _settings.SetTextAsync(Password.Name, Organization, null, "alice", CancellationToken.None);
        await _settings.ResetAsync(MaxTotes, SettingScopeRef.Site(2), "alice", CancellationToken.None);
        await _settings.SetModeAsync(MaxTotes, Organization, CascadeMode.PerSite, "alice", CancellationToken.None);
        var list = await _settings.ListAsync(Organization, CancellationToken.None);
        var found = await _settings.FindAsync(MaxTotes.Name, Organization, CancellationToken.None);

        Assert.Equal(5, await _settings.GetAsync(MaxTotes, Organization, CancellationToken.None));
        Assert.Equal(5, _store.Writes);
        Assert.Equal("5", _store.Rows[(Organization, MaxTotes.Name)]);
        Assert.Equal(2, list.Count);
        Assert.Equal("5", found.ConfiguredValue);
    }



    [Fact]
    public void Constructor_refuses_a_null_dependency()
    {
        var protector = new FakeProtector();

        Assert.Throws<ArgumentNullException>(() => new ProtectedSettings(null!, Registry, protector));
        Assert.Throws<ArgumentNullException>(() => new ProtectedSettings(_store, null!, protector));
        Assert.Throws<ArgumentNullException>(() => new ProtectedSettings(_store, Registry, null!));
    }



    private sealed class FakeProtector : ISecretProtector
    {
        public string Protect(string plainText)
        {
            return ProtectedText.Wrap(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText)));
        }



        public string Unprotect(string protectedText)
        {
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(ProtectedText.Unwrap(protectedText)));
        }
    }
}
