﻿// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;

    public class TwinReportStateCommandFactory : ICommandFactory
    {
        readonly ICommandFactory underlying;
        readonly IDeviceClient deviceClient;
        readonly IEnvironment environment;

        public TwinReportStateCommandFactory(
            ICommandFactory underlying,
            IDeviceClient deviceClient,
            IEnvironment environment
        )
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.environment = Preconditions.CheckNotNull(environment, nameof(environment));
            this.deviceClient = Preconditions.CheckNotNull(deviceClient, nameof(deviceClient));
        }

        async Task UpdateReportedProperties(ModuleSet moduleSetBefore, ModuleSet moduleSetAfter)
        {
            // TODO: Should we de-bounce calls to this function?

            Diff diff = moduleSetAfter.Diff(moduleSetBefore);

            // add the modules that are still running
            var modulesMap = new Dictionary<string, IModule>(diff.Updated.ToImmutableDictionary(m => m.Name));

            // add removed modules by assigning 'null' as the value
            foreach (string moduleName in diff.Removed)
            {
                modulesMap.Add(moduleName, null);
            }

            var reportedProps = new TwinCollection
            {
                ["modules"] = modulesMap
            };
            await this.deviceClient.UpdateReportedPropertiesAsync(reportedProps);
        }

        public ICommand Create(IModule module) => new TwinReportStateCommand(this.underlying.Create(module), this);

        public ICommand Pull(IModule module) => new TwinReportStateCommand(this.underlying.Pull(module), this);

        public ICommand Update(IModule current, IModule next) => new TwinReportStateCommand(this.underlying.Update(current, next), this);

        public ICommand Remove(IModule module) => new TwinReportStateCommand(this.underlying.Remove(module), this);

        public ICommand Start(IModule module) => new TwinReportStateCommand(this.underlying.Start(module), this);

        public ICommand Stop(IModule module) => new TwinReportStateCommand(this.underlying.Stop(module), this);

        class TwinReportStateCommand : ICommand
        {
            readonly ICommand underlying;
            readonly TwinReportStateCommandFactory factory;

            public TwinReportStateCommand(ICommand underlying, TwinReportStateCommandFactory factory)
            {
                this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
                this.factory = factory;
            }

            public string Show() => this.underlying.Show();

            public async Task ExecuteAsync(CancellationToken token)
            {
                ModuleSet moduleSetBefore = await this.factory.environment.GetModulesAsync(token);
                await this.underlying.ExecuteAsync(token);
                ModuleSet moduleSetAfter = await this.factory.environment.GetModulesAsync(token);

                await this.factory.UpdateReportedProperties(moduleSetBefore, moduleSetAfter);
            }

            public async Task UndoAsync(CancellationToken token)
            {
                ModuleSet moduleSetBefore = await this.factory.environment.GetModulesAsync(token);
                await this.underlying.UndoAsync(token);
                ModuleSet moduleSetAfter = await this.factory.environment.GetModulesAsync(token);

                await this.factory.UpdateReportedProperties(moduleSetBefore, moduleSetAfter);
            }
        }
    }
}
