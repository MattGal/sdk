// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Jab;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;

namespace Microsoft.DotNet.ApiCompat
{
    [ServiceProvider(RootServices = new[] { typeof(IEnumerable<IRule>) })]
    [Import(typeof(IApiCompatServiceProviderModule))]
    internal partial class ValidateAssembliesServiceProvider : IApiCompatServiceProviderModule
    {
        public Func<ICompatibilityLogger> LogFactory { get; }

        public Func<ISuppressionEngine> SuppressionEngineFactory { get; }

        public Func<IRuleFactory> RuleFactory { get; }

        public ValidateAssembliesServiceProvider(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            Func<ISuppressionEngine> suppressionEngineFactory,
            Func<ICompatibilityLogger, IRuleFactory> ruleFactory)
        {
            // It's important to use GetService<T> here instead of directly invoking the factory
            // to avoid two instances being created when retrieving a singleton.
            LogFactory = () => logFactory(GetService<ISuppressionEngine>());
            SuppressionEngineFactory = suppressionEngineFactory;
            RuleFactory = () => ruleFactory(GetService<ICompatibilityLogger>());
        }
    }

    internal static class ValidateAssemblies
    {
        public static void Run(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            bool generateSuppressionFile,
            string? suppressionFile,
            string? noWarn,
            string[]? excludeAttributesFiles,
            string[] leftAssemblies,
            string[] rightAssemblies,
            bool enableStrictMode,
            string[][]? leftAssembliesReferences,
            string[][]? rightAssembliesReferences,
            bool createWorkItemPerAssembly,
            (string CaptureGroupPattern, string ReplacementString)[]? leftAssembliesTransformationPatterns,
            (string CaptureGroupPattern, string ReplacementString)[]? rightAssembliesTransformationPatterns)
        {
            // Configure the suppression engine. Ignore the passed in suppression file if it should be generated and doesn't yet exist.
            string? suppressionFileForEngine = generateSuppressionFile && !File.Exists(suppressionFile) ? null : suppressionFile;

            // Initialize the service provider
            ValidateAssembliesServiceProvider serviceProvider = new(logFactory,
                () => new SuppressionEngine(suppressionFileForEngine, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log, excludeAttributesFiles));

            IApiCompatRunner apiCompatRunner = serviceProvider.GetService<IApiCompatRunner>();
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode);

            // Optionally provide a string transformer if a transformation pattern is passed in.
            RegexStringTransformer? leftAssembliesStringTransformer = leftAssembliesTransformationPatterns != null ? new RegexStringTransformer(leftAssembliesTransformationPatterns) : null;
            RegexStringTransformer? rightAssembliesStringTransformer = rightAssembliesTransformationPatterns != null ? new RegexStringTransformer(rightAssembliesTransformationPatterns) : null;

            if (createWorkItemPerAssembly)
            {
                if (leftAssemblies.Length != rightAssemblies.Length)
                {
                    throw new Exception(CommonResources.CreateWorkItemPerAssemblyAssembliesNotEqual);
                }

                for (int i = 0; i < leftAssemblies.Length; i++)
                {
                    IReadOnlyList<MetadataInformation> leftMetadataInformation = GetMetadataInformation(leftAssemblies[i], GetAssemblyReferences(leftAssembliesReferences, i), leftAssembliesStringTransformer);
                    IReadOnlyList<MetadataInformation> rightMetadataInformation = GetMetadataInformation(rightAssemblies[i], GetAssemblyReferences(rightAssembliesReferences, i), rightAssembliesStringTransformer);

                    // Enqueue the work item
                    ApiCompatRunnerWorkItem workItem = new(leftMetadataInformation, apiCompatOptions, rightMetadataInformation);
                    apiCompatRunner.EnqueueWorkItem(workItem);
                }
            }
            else
            {
                // Create the work item that corresponds to the passed in left assembly.
                List<MetadataInformation> leftAssembliesMetadataInformation = new(leftAssemblies.Length);
                for (int i = 0; i < leftAssemblies.Length; i++)
                {
                    leftAssembliesMetadataInformation.AddRange(GetMetadataInformation(leftAssemblies[i], GetAssemblyReferences(leftAssembliesReferences, i), leftAssembliesStringTransformer));
                }

                List<MetadataInformation> rightAssembliesMetadataInformation = new(rightAssemblies.Length);
                for (int i = 0; i < rightAssemblies.Length; i++)
                {
                    rightAssembliesMetadataInformation.AddRange(GetMetadataInformation(rightAssemblies[i], GetAssemblyReferences(rightAssembliesReferences, i), rightAssembliesStringTransformer));
                }

                // Enqueue the work item
                ApiCompatRunnerWorkItem workItem = new(leftAssembliesMetadataInformation, apiCompatOptions, rightAssembliesMetadataInformation);
                apiCompatRunner.EnqueueWorkItem(workItem);
            }

            // Execute the enqueued work item(s).
            apiCompatRunner.ExecuteWorkItems();

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.GetService<ISuppressionEngine>(),
                    serviceProvider.GetService<ICompatibilityLogger>(),
                    suppressionFile);
            }
        }

        private static string[]? GetAssemblyReferences(string[][]? assemblyReferences, int counter)
        {
            if (assemblyReferences == null || assemblyReferences.Length == 0)
                return null;

            if (assemblyReferences.Length > counter)
            {
                return assemblyReferences[counter];
            }

            // If explicit assembly references weren't provided for an assembly, return the ones provided first
            // so that consumers can provide one shareable set of references for all left/right inputs.
            return assemblyReferences[0];
        }

        private static IReadOnlyList<MetadataInformation> GetMetadataInformation(string path,
            IEnumerable<string>? assemblyReferences,
            RegexStringTransformer? regexStringTransformer)
        {
            List<MetadataInformation> metadataInformation = new();
            foreach (string assembly in GetFilesFromPath(path))
            {
                metadataInformation.Add(new MetadataInformation(
                    assemblyName: Path.GetFileNameWithoutExtension(assembly),
                    assemblyId: regexStringTransformer?.Transform(assembly) ?? assembly,
                    fullPath: assembly,
                    references: assemblyReferences));
            }

            return metadataInformation;
        }

        private static IEnumerable<string> GetFilesFromPath(string path)
        {
            // Check if the given path is a directory
            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*.dll");
            }

            // If the path isn't a directory, see if it's a glob expression.
            string filename = Path.GetFileName(path);
#if NETCOREAPP
            if (filename.Contains('*'))
#else
            if (filename.Contains("*"))
#endif
            {
                string? directoryName = Path.GetDirectoryName(path);
                if (directoryName != null)
                {
                    try
                    {
                        return Directory.EnumerateFiles(directoryName, filename);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return new string[] { path };
        }
    }
}
