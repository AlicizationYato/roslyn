﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.ProjectSystem
{
    internal partial class VisualStudioWorkspaceImpl
    {
        private abstract class ProjectPropertyStorage
        {
            // For CPS projects, we prefer to use IVsBuildPropertyStorage since that allows us to easily modify the project
            // properties independent of configuration. However this is problematic in the legacy project system, where setting
            // the build property would indeed change its value, however that change would only be visible after the project is
            // unloaded and reloaded. The language service would not be updated (the error we're trying to fix would still
            // be visible) and any checkbox in the project properties dialog would not be updated. The language service issue
            // could be worked around by using VSProject.Refresh, but that does not fix the project properties dialog. Therefore
            // for the legacy project system, we choose to use ConfigurationManager to iterate & update each configuration,
            // which works correctly, even though it is a little less convinient because it creates a more verbose project file
            // (although we're dealing with a legacy project file... it's already verbose anyway).

            // It's important to note that the property name may differ in these two implementations. The build property name
            // corresponds to the name of the property in the project file (for example LangVersion), whereas the configuration
            // property name comes from an interface such as CSharpProjectConfigurationProperties3 (for example LanguageVersion).

            public static ProjectPropertyStorage Create(Project project, IServiceProvider serviceProvider)
            {
                var solution = (IVsSolution)serviceProvider.GetService(typeof(SVsSolution));
                solution.GetProjectOfUniqueName(project.UniqueName, out var hierarchy);

                return hierarchy.IsCapabilityMatch("CPS")
                    ? new BuildPropertyStorage((IVsBuildPropertyStorage)hierarchy)
                    : new PerConfigurationPropertyStorage(project.ConfigurationManager) as ProjectPropertyStorage;
            }

            public abstract void SetProperty(string buildPropertyName, string configurationPropertyName, string value);

            private sealed class BuildPropertyStorage : ProjectPropertyStorage
            {
                private readonly IVsBuildPropertyStorage propertyStorage;

                public BuildPropertyStorage(IVsBuildPropertyStorage propertyStorage)
                    => this.propertyStorage = propertyStorage;

                public override void SetProperty(string buildPropertyName, string configurationPropertyName, string value)
                {
                    propertyStorage.SetPropertyValue(buildPropertyName, null, (uint)_PersistStorageType.PST_PROJECT_FILE, value);
                }
            }

            private sealed class PerConfigurationPropertyStorage : ProjectPropertyStorage
            {
                private readonly ConfigurationManager configurationManager;

                public PerConfigurationPropertyStorage(ConfigurationManager configurationManager)
                    => this.configurationManager = configurationManager;

                public override void SetProperty(string buildPropertyName, string configurationPropertyName, string value)
                {
                    foreach (Configuration configuration in configurationManager)
                    {
                        configuration.Properties.Item(configurationPropertyName).Value = value;
                    }
                }
            }
        }
    }
}
