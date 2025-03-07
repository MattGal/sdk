﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.DotNet.ApiCompatibility.Logging
{
    /// <summary>
    /// Collection of Suppressions which is able to add suppressions, check if a specific error is suppressed, and write all suppressions
    /// down to a file. The engine is thread-safe.
    /// </summary>
    public class SuppressionEngine : ISuppressionEngine
    {
        protected HashSet<Suppression> _validationSuppressions;
        private readonly ReaderWriterLockSlim _readerWriterLock = new();
        private readonly XmlSerializer _serializer = new(typeof(Suppression[]), new XmlRootAttribute("Suppressions"));
        private readonly HashSet<string> _noWarn;

        public bool BaselineAllErrors { get; }

        /// <inheritdoc/>
        public SuppressionEngine(string? suppressionFile = null,
            string? noWarn = null,
            bool baselineAllErrors = false)
        {
            _validationSuppressions = ParseSuppressionFile(suppressionFile);
            _noWarn = string.IsNullOrEmpty(noWarn) ? new HashSet<string>() : new HashSet<string>(noWarn!.Split(';'));
            BaselineAllErrors = baselineAllErrors;
        }

        /// <inheritdoc/>
        public bool IsErrorSuppressed(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            return IsErrorSuppressed(new Suppression(diagnosticId)
            {
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            });
        }

        /// <inheritdoc/>
        public bool IsErrorSuppressed(Suppression error)
        {
            if (_noWarn.Contains(error.DiagnosticId))
            {
                return true;
            }

            _readerWriterLock.EnterReadLock();
            try
            {
                if (_validationSuppressions.Contains(error))
                {
                    return true;
                }
                else if (error.DiagnosticId.StartsWith("cp", StringComparison.InvariantCultureIgnoreCase) &&
                         (_validationSuppressions.Contains(new Suppression(error.DiagnosticId) { Target = error.Target, IsBaselineSuppression = error.IsBaselineSuppression }) ||
                          _validationSuppressions.Contains(new Suppression(error.DiagnosticId) { Left = error.Left, Right = error.Right, IsBaselineSuppression = error.IsBaselineSuppression })))
                {
                    // See if the error is globally suppressed by checking if the same diagnosticid and target or with the same left and right
                    return true;
                }
            }
            finally
            {
                _readerWriterLock.ExitReadLock();
            }

            if (BaselineAllErrors)
            {
                AddSuppression(error);
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void AddSuppression(string diagnosticId, string? target, string? left = null, string? right = null, bool isBaselineSuppression = false)
        {
            AddSuppression(new Suppression(diagnosticId)
            {
                Target = target,
                Left = left,
                Right = right,
                IsBaselineSuppression = isBaselineSuppression
            });
        }

        /// <inheritdoc/>
        public void AddSuppression(Suppression suppression)
        {
            _readerWriterLock.EnterUpgradeableReadLock();
            try
            {
                if (!_validationSuppressions.Contains(suppression))
                {
                    _readerWriterLock.EnterWriteLock();
                    try
                    {
                        _validationSuppressions.Add(suppression);
                    }
                    finally
                    {
                        _readerWriterLock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _readerWriterLock.ExitUpgradeableReadLock();
            }
        }

        /// <inheritdoc/>
        public bool WriteSuppressionsToFile(string supressionFile)
        {
            if (_validationSuppressions.Count == 0)
                return false;

            using (Stream writer = GetWritableStream(supressionFile))
            {
                _readerWriterLock.EnterReadLock();
                try
                {
                    XmlTextWriter xmlWriter = new(writer, Encoding.UTF8)
                    {
                        Formatting = Formatting.Indented,
                        Indentation = 2
                    };
                    _serializer.Serialize(xmlWriter, _validationSuppressions.ToArray());
                    AfterWrittingSuppressionsCallback(writer);
                }
                finally
                {
                    _readerWriterLock.ExitReadLock();
                }
            }

            return true;
        }

        protected virtual void AfterWrittingSuppressionsCallback(Stream stream)
        {
            // Do nothing. Used for tests.
        }

        private HashSet<Suppression> ParseSuppressionFile(string? file)
        {
            if (string.IsNullOrEmpty(file?.Trim()))
            {
                return new HashSet<Suppression>();
            }

            using (Stream reader = GetReadableStream(file!))
            {
                if (_serializer.Deserialize(reader) is Suppression[] deserializedSuppressions)
                {
                    return new HashSet<Suppression>(deserializedSuppressions);
                }

                return new HashSet<Suppression>();
            }
        }

        // FileAccess.Read and FileShare.Read are specified to allow multiple processes to concurrently read from the suppression file.
        protected virtual Stream GetReadableStream(string supressionFile) => new FileStream(supressionFile, FileMode.Open, FileAccess.Read, FileShare.Read);

        protected virtual Stream GetWritableStream(string suppressionFile) => new FileStream(suppressionFile, FileMode.Create);
    }
}
