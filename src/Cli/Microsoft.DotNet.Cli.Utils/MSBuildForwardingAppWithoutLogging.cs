﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class MSBuildForwardingAppWithoutLogging
    {
        private static readonly bool ExecuteMSBuildOutOfProcByDefault = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_EXEC_MSBUILD");

        private const string MSBuildAppClassName = "Microsoft.Build.CommandLine.MSBuildApp";

        private const string MSBuildExeName = "MSBuild.dll";

        private const string SdksDirectoryName = "Sdks";

        // Null if we're running MSBuild in-proc.
        private ForwardingAppImplementation _forwardingApp;

        private IEnumerable<string> _argsToForward;

        private string _msbuildPath;

        internal static string MSBuildExtensionsPathTestHook = null;

        internal bool ExecuteMSBuildOutOfProc => _forwardingApp != null;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", MSBuildExtensionsPathTestHook ?? AppContext.BaseDirectory },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() },
                { "DOTNET_HOST_PATH", GetDotnetPath() },
            };

        private readonly IEnumerable<string> _msbuildRequiredParameters =
            new List<string> { "-maxcpucount", "-verbosity:m" };

        public MSBuildForwardingAppWithoutLogging(IEnumerable<string> argsToForward, string msbuildPath = null, bool? executeOutOfProc = null)
        {
            _argsToForward = argsToForward;
            _msbuildPath = msbuildPath ?? GetMSBuildExePath();

            if (executeOutOfProc == true || (executeOutOfProc == null && ExecuteMSBuildOutOfProcByDefault))
            {
                _forwardingApp = new ForwardingAppImplementation(
                    _msbuildPath,
                    _msbuildRequiredParameters.Concat(argsToForward.Select(Escape)),
                    environmentVariables: _msbuildRequiredEnvironmentVariables);
            }
        }

        public virtual ProcessStartInfo GetProcessStartInfo()
        {
            Debug.Assert(_forwardingApp != null, "Can't get ProcessStartInfo when not executing out-of-proc");
            return _forwardingApp.GetProcessStartInfo();
        }

        public string[] GetAllArgumentsUnescaped()
        {
            return _msbuildRequiredParameters.Concat(_argsToForward.Select(Escape)).ToArray();
        }

        public void EnvironmentVariable(string name, string value)
        {
            if (_forwardingApp != null)
            {
                _forwardingApp.WithEnvironmentVariable(name, value);
            }
            else
            {
                _msbuildRequiredEnvironmentVariables.Add(name, value);
            }

            if (value == string.Empty || value == "\0")
            {
                // Unlike ProcessStartInfo.EnvironmentVariables, Environment.SetEnvironmentVariable can't set a variable
                // to an empty value, so we just fall back to calling MSBuild out-of-proc if we encounter this.
                // https://github.com/dotnet/runtime/issues/34446
                _forwardingApp = new ForwardingAppImplementation(
                    _msbuildPath,
                    _msbuildRequiredParameters.Concat(_argsToForward.Select(Escape)),
                    environmentVariables: _msbuildRequiredEnvironmentVariables);
            }
        }

        public int Execute()
        {
            if (_forwardingApp != null)
            {
                return GetProcessStartInfo().Execute();
            }
            else
            {
                return ExecuteInProc(GetAllArgumentsUnescaped());
            }
        }

        public int ExecuteInProc(string[] arguments)
        {
            // Save current environment variables before overwriting them.
            Dictionary<string, string> savedEnvironmentVariables = new Dictionary<string, string>();
            try
            {
                foreach (KeyValuePair<string, string> kvp in _msbuildRequiredEnvironmentVariables)
                {
                    savedEnvironmentVariables[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }

                try
                {
                    return Microsoft.Build.CommandLine.MSBuildApp.Main(arguments);
                }
                catch (Exception exception)
                {
                    Console.Error.Write("Unhandled exception: ");
                    Console.Error.WriteLine(exception.ToString());

                    return unchecked((int)0xe0434352); // EXCEPTION_COMPLUS
                }
            }
            finally
            {
                // Restore saved environment variables.
                foreach (KeyValuePair<string, string> kvp in savedEnvironmentVariables)
                {
                    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
                }
            }
        }

        private static string Escape(string arg) =>
             // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
             IsRestoreSources(arg) ?
                arg.Replace(";", "%3B")
                   .Replace("://", ":%2F%2F") :
                arg;

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                MSBuildExeName);
        }

        private static string GetMSBuildSDKsPath()
        {
            var envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

            if (envMSBuildSDKsPath != null)
            {
                return envMSBuildSDKsPath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                SdksDirectoryName);
        }

        private static string GetDotnetPath()
        {
            return new Muxer().MuxerPath;
        }

        private static bool IsRestoreSources(string arg)
        {
            return arg.StartsWith("/p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/property:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-property:RestoreSources=", StringComparison.OrdinalIgnoreCase);
        }
    }
}
