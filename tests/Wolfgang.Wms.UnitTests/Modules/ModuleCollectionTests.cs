// Copyright (c) Chris Wolfgang. All rights reserved. SPDX-License-Identifier: LicenseRef-TBD

using Microsoft.Extensions.DependencyInjection;
using Wolfgang.Wms.Core.Modules;

namespace Wolfgang.Wms.UnitTests.Modules;

public sealed class ModuleCollectionTests
{
    [Fact]
    public void Modules_are_kept_in_registration_order()
    {
        var collection = new ModuleCollection();

        collection.Add(ModuleDescriptor.Create("Picking"));
        collection.Add(ModuleDescriptor.Create("Inventory"));

        Assert.Equal(["Picking", "Inventory"], collection.Modules.Select(m => m.Name));
    }



    [Fact]
    public void Registering_the_same_module_name_twice_is_rejected()
    {
        var collection = new ModuleCollection();
        collection.Add(ModuleDescriptor.Create("Picking"));

        var exception = Assert.Throws<InvalidOperationException>(() => collection.Add(ModuleDescriptor.Create("Picking")));

        Assert.Contains("Picking", exception.Message, StringComparison.Ordinal);
    }



    [Fact]
    public void Adding_a_null_module_is_rejected()
    {
        var collection = new ModuleCollection();

        Assert.Throws<ArgumentNullException>(() => collection.Add(null!));
    }



    [Fact]
    public void Descriptor_requires_a_name()
    {
        Assert.Throws<ArgumentException>(() => ModuleDescriptor.Create(" "));
    }



    [Fact]
    public void Descriptor_accumulates_endpoint_mappers_in_order_without_mutating_the_original()
    {
        var original = ModuleDescriptor.Create("Picking");
        var calls = new List<string>();

        var extended = original
            .WithEndpoints(_ => calls.Add("first"))
            .WithEndpoints(_ => calls.Add("second"));
        foreach (var map in extended.EndpointMappers)
        {
            map(null!);
        }

        Assert.Empty(original.EndpointMappers);
        Assert.Equal(["first", "second"], calls);
    }



    [Fact]
    public void Descriptor_rejects_a_null_endpoint_mapper()
    {
        Assert.Throws<ArgumentNullException>(() => ModuleDescriptor.Create("Picking").WithEndpoints(null!));
    }



    [Fact]
    public void AddWmsModules_registers_one_collection_and_returns_it_on_every_call()
    {
        var services = new ServiceCollection();

        var first = services.AddWmsModules();
        var second = services.AddWmsModules();

        Assert.Same(first, second);
        Assert.Single(services, d => d.ServiceType == typeof(ModuleCollection));
        Assert.Same(first, services.BuildServiceProvider().GetRequiredService<ModuleCollection>());
    }



    [Fact]
    public void AddWmsModule_adds_to_the_hosts_collection()
    {
        var services = new ServiceCollection();

        services.AddWmsModule(ModuleDescriptor.Create("Picking"));

        Assert.Equal(["Picking"], services.AddWmsModules().Modules.Select(m => m.Name));
    }



    [Fact]
    public void Service_registration_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => WmsModuleServiceCollectionExtensions.AddWmsModules(null!));
        Assert.Throws<ArgumentNullException>(() => WmsModuleServiceCollectionExtensions.AddWmsModule(null!, ModuleDescriptor.Create("Picking")));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddWmsModule(null!));
    }
}
