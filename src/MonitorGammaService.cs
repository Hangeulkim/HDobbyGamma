using System.Drawing;
using System.Runtime.InteropServices;

namespace GammaControl;

internal sealed class DisplayTarget
{
    internal DisplayTarget(
        string deviceName,
        string settingsKey,
        string friendlyName,
        Rectangle bounds,
        bool isPrimary,
        GammaRamp? originalRamp,
        string? readError)
    {
        DeviceName = deviceName;
        SettingsKey = settingsKey;
        FriendlyName = friendlyName;
        Bounds = bounds;
        IsPrimary = isPrimary;
        OriginalRamp = originalRamp;
        LastAppliedRamp = null;
        ForceRestoreOriginal = false;
        ReadError = readError;
        CurrentGamma = 1.0;
    }

    internal string DeviceName { get; private set; }
    internal string SettingsKey { get; }
    internal string FriendlyName { get; private set; }
    internal Rectangle Bounds { get; private set; }
    internal bool IsPrimary { get; private set; }
    internal GammaRamp? OriginalRamp { get; }
    internal GammaRamp? LastAppliedRamp { get; set; }
    internal bool ForceRestoreOriginal { get; set; }
    internal string? ReadError { get; }
    internal double CurrentGamma { get; set; }
    internal bool SupportsGamma => OriginalRamp != null;

    internal void UpdateMetadata(string deviceName, string friendlyName, Rectangle bounds, bool isPrimary)
    {
        DeviceName = deviceName;
        FriendlyName = friendlyName;
        Bounds = bounds;
        IsPrimary = isPrimary;
    }

    public override string ToString()
    {
        var primary = IsPrimary ? " · " + UiText.Get(TextId.PrimaryMonitor) : string.Empty;
        var support = SupportsGamma ? string.Empty : " · " + UiText.Get(TextId.GammaUnsupported);
        var friendlyName = string.IsNullOrWhiteSpace(FriendlyName)
            ? UiText.Get(TextId.GenericMonitor)
            : FriendlyName;
        return $"{DeviceName.Replace("\\\\.\\", string.Empty)} · {friendlyName} · " +
               $"{Bounds.Width}×{Bounds.Height}{primary}{support}";
    }
}

internal sealed class ApplyResult
{
    internal ApplyResult()
    {
        ChangedDevices = new List<string>();
        FailedDevices = new List<string>();
        UnverifiedDevices = new List<string>();
        UnexpectedlyChangedDevices = new List<string>();
        RollbackFailedDevices = new List<string>();
    }

    internal List<string> ChangedDevices { get; }
    internal List<string> FailedDevices { get; }
    internal List<string> UnverifiedDevices { get; }
    internal List<string> UnexpectedlyChangedDevices { get; }
    internal List<string> RollbackFailedDevices { get; }
    internal string? ErrorMessage { get; set; }
    internal string? RequestedDeviceName { get; set; }
    internal bool RolledBack { get; set; }
    internal bool IsSuccess => ChangedDevices.Count > 0 && FailedDevices.Count == 0 && !RolledBack;
}

internal sealed class MonitorGammaService : IDisposable
{
    private sealed record TemporaryGammaState(
        string SettingsKey, GammaRamp Before, GammaRamp? PreviousOwner,
        bool PreviousForceRestore, double PreviousGamma);

    // Gamma ramps are commonly quantized to 8-bit steps (about 128 units of round-off
    // in the 16-bit API). Keep enough room for that without accepting a ignored 0.01-
    // 0.02 gamma change as verified.
    internal const int ReadbackTolerance = 192;
    internal const int LinkedMonitorTolerance = 128;
    private readonly object _sync = new();
    private readonly Dictionary<string, HashSet<string>> _linkedGroupsBySettingsKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DisplayTarget> _knownTargets = new(StringComparer.OrdinalIgnoreCase);
    private List<DisplayTarget> _targets = new();
    private TemporaryGammaState? _temporaryGamma;
    private bool _disposed;

    internal IReadOnlyList<DisplayTarget> Targets
    {
        get
        {
            lock (_sync)
            {
                return _targets.ToArray();
            }
        }
    }

    internal bool HasOwnedRamps
    {
        get
        {
            lock (_sync)
            {
                return _knownTargets.Values.Any(target =>
                    target.LastAppliedRamp != null || target.ForceRestoreOriginal);
            }
        }
    }

    internal bool HasOwnedRampsOnConnectedTargets
    {
        get
        {
            lock (_sync)
            {
                return _targets.Any(target =>
                    target.LastAppliedRamp != null || target.ForceRestoreOriginal);
            }
        }
    }

    internal bool IsLinked(string deviceName)
    {
        lock (_sync)
        {
            var target = _targets.FirstOrDefault(item =>
                string.Equals(item.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            return target != null && _linkedGroupsBySettingsKey.ContainsKey(target.SettingsKey);
        }
    }

    internal string? TemporaryGammaDeviceKey
    {
        get { lock (_sync) { return _temporaryGamma?.SettingsKey; } }
    }

    internal bool TryStartTemporaryGamma(string deviceName, double gamma, out string? error)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            error = null;
            if (_temporaryGamma != null && !TryEndTemporaryGamma(out error)) return false;
            var target = _targets.FirstOrDefault(item =>
                string.Equals(item.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
            if (target == null || !target.SupportsGamma)
            {
                error = UiText.Get(TextId.MonitorUnsupported);
                return false;
            }
            var before = TryReadRamp(target.DeviceName, out error);
            if (before == null) return false;
            var session = new TemporaryGammaState(target.SettingsKey, before,
                target.LastAppliedRamp?.Clone(), target.ForceRestoreOriginal, target.CurrentGamma);
            var result = ApplyGamma(target.DeviceName, gamma);
            if (!result.IsSuccess || result.UnverifiedDevices.Count > 0 ||
                result.UnexpectedlyChangedDevices.Count > 0)
            {
                error = result.ErrorMessage ?? UiText.Get(TextId.ProgramGammaApplyFailed);
                if (result.ChangedDevices.Count > 0 && !result.RolledBack)
                {
                    _temporaryGamma = session;
                    TryEndTemporaryGamma(out _);
                }
                return false;
            }
            _temporaryGamma = session;
            return true;
        }
    }

    internal bool TryEndTemporaryGamma(out string? error)
    {
        lock (_sync)
        {
            error = null;
            var session = _temporaryGamma;
            if (session == null) return true;
            var target = _targets.FirstOrDefault(item =>
                string.Equals(item.SettingsKey, session.SettingsKey, StringComparison.OrdinalIgnoreCase));
            if (target == null)
            {
                _temporaryGamma = null;
                return true;
            }
            var current = TryReadRamp(target.DeviceName, out error);
            if (current == null) return false;
            if (target.LastAppliedRamp != null && !target.ForceRestoreOriginal &&
                current.MaxDifference(target.LastAppliedRamp) > ReadbackTolerance)
            {
                // A different color manager took ownership. Leave its ramp intact.
                target.LastAppliedRamp = null;
                target.ForceRestoreOriginal = false;
                _temporaryGamma = null;
                return true;
            }
            if (!TrySetRamp(target.DeviceName, session.Before, out error)) return false;
            var readback = TryReadRamp(target.DeviceName, out error);
            if (readback == null || readback.MaxDifference(session.Before) > ReadbackTolerance)
            {
                target.LastAppliedRamp = readback?.Clone() ?? target.LastAppliedRamp;
                target.ForceRestoreOriginal = readback == null;
                error ??= UiText.Get(TextId.ProgramGammaRestoreFailed);
                return false;
            }
            target.LastAppliedRamp = session.PreviousOwner?.Clone();
            target.ForceRestoreOriginal = session.PreviousForceRestore;
            target.CurrentGamma = session.PreviousGamma;
            _temporaryGamma = null;
            return true;
        }
    }

    internal IReadOnlyList<DisplayTarget> Refresh()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            var discovered = new List<DisplayTarget>();
            var displayConfigIdentities = GetDisplayConfigIdentities();
            NativeMethods.MonitorEnumProc callback = delegate(
                IntPtr monitor,
                IntPtr monitorDc,
                ref NativeMethods.NativeRect rect,
                IntPtr data)
            {
                var info = new NativeMethods.MonitorInfoEx
                {
                    Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                    DeviceName = string.Empty
                };

                if (!NativeMethods.GetMonitorInfo(monitor, ref info) || string.IsNullOrWhiteSpace(info.DeviceName))
                {
                    return true;
                }

                var bounds = Rectangle.FromLTRB(
                    info.Monitor.Left,
                    info.Monitor.Top,
                    info.Monitor.Right,
                    info.Monitor.Bottom);
                var identity = GetMonitorIdentity(info.DeviceName, displayConfigIdentities);
                var isPrimary = (info.Flags & NativeMethods.MonitorInfoPrimary) != 0;
                var settingsKey = identity.SettingsKey;
                if (discovered.Any(target =>
                        string.Equals(target.SettingsKey, settingsKey, StringComparison.OrdinalIgnoreCase)))
                {
                    // A clone/mirror driver can expose the same physical interface more than once.
                    // Keep those logical outputs distinct while retaining stable IDs normally.
                    settingsKey += "|logical:" + info.DeviceName;
                }

                if (_knownTargets.TryGetValue(settingsKey, out var existing))
                {
                    existing.UpdateMetadata(info.DeviceName, identity.FriendlyName, bounds, isPrimary);
                    discovered.Add(existing);
                    return true;
                }

                var original = TryReadRamp(info.DeviceName, out var readError);
                var created = new DisplayTarget(
                    info.DeviceName,
                    settingsKey,
                    identity.FriendlyName,
                    bounds,
                    isPrimary,
                    original,
                    readError);
                _knownTargets[settingsKey] = created;
                discovered.Add(created);
                return true;
            };

            if (!NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero))
            {
                throw new InvalidOperationException(UiText.Get(TextId.EnumerationFailed));
            }

            _targets = discovered
                .OrderByDescending(target => target.IsPrimary)
                .ThenBy(target => target.Bounds.Left)
                .ThenBy(target => target.Bounds.Top)
                .ToList();
            return _targets.ToArray();
        }
    }

    internal ApplyResult ApplyGamma(string? deviceName, double gamma)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_temporaryGamma != null && !TryEndTemporaryGamma(out var restoreError))
            {
                return new ApplyResult { ErrorMessage = restoreError ??
                    UiText.Get(TextId.ProgramGammaRestoreFailed) };
            }
            var ramp = GammaRampBuilder.Build(gamma);
            var selected = string.IsNullOrWhiteSpace(deviceName)
                ? _targets.ToList()
                : _targets.Where(target =>
                    string.Equals(target.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)).ToList();

            var result = new ApplyResult();
            if (selected.Count == 0)
            {
                result.ErrorMessage = UiText.Get(TextId.SelectedNotFound);
                return result;
            }

            result.RequestedDeviceName = selected.Count == 1 ? selected[0].DeviceName : null;


            if (selected.Count == 1 && _linkedGroupsBySettingsKey.ContainsKey(selected[0].SettingsKey))
            {
                result.FailedDevices.Add(selected[0].DeviceName);
                result.ErrorMessage = UiText.Get(TextId.SharedLutUseAll);
                return result;
            }

            var beforeRamps = new Dictionary<string, GammaRamp>(StringComparer.OrdinalIgnoreCase);
            var ownershipBefore = new Dictionary<string, GammaRamp?>(StringComparer.OrdinalIgnoreCase);
            var forceRestoreBefore = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var gammaBefore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (selected.Count == 1)
            {
                foreach (var candidate in _targets)
                {
                    var before = TryReadRamp(candidate.DeviceName, out _);
                    if (before != null)
                    {
                        beforeRamps[candidate.DeviceName] = before;
                    }

                    ownershipBefore[candidate.DeviceName] = candidate.LastAppliedRamp?.Clone();
                    forceRestoreBefore[candidate.DeviceName] = candidate.ForceRestoreOriginal;
                    gammaBefore[candidate.DeviceName] = candidate.CurrentGamma;
                }
            }

            foreach (var target in selected)
            {
                string? setError = target.SupportsGamma
                    ? null
                    : UiText.Get(TextId.MonitorUnsupported);
                if (!target.SupportsGamma || !TrySetRamp(target.DeviceName, ramp, out setError))
                {
                    result.FailedDevices.Add(target.DeviceName);
                    if (!string.IsNullOrWhiteSpace(setError))
                    {
                        result.ErrorMessage = setError;
                    }
                    continue;
                }

                target.CurrentGamma = gamma;
                result.ChangedDevices.Add(target.DeviceName);

                var readback = TryReadRamp(target.DeviceName, out _);
                target.LastAppliedRamp = (readback ?? ramp).Clone();
                target.ForceRestoreOriginal = readback == null;
                if (readback == null || readback.MaxDifference(ramp) > ReadbackTolerance)
                {
                    result.UnverifiedDevices.Add(target.DeviceName);
                }
            }

            if (selected.Count == 1)
            {
                foreach (var before in beforeRamps.Where(item =>
                             !string.Equals(item.Key, selected[0].DeviceName, StringComparison.OrdinalIgnoreCase)))
                {
                    var after = TryReadRamp(before.Key, out _);
                    if (after != null && before.Value.MaxDifference(after) > LinkedMonitorTolerance)
                    {
                        result.UnexpectedlyChangedDevices.Add(before.Key);
                    }
                }

                if (result.UnexpectedlyChangedDevices.Count > 0)
                {
                    result.RolledBack = true;
                    var linkedSettingsKeys = new List<string> { selected[0].SettingsKey };
                    foreach (var linkedDevice in result.UnexpectedlyChangedDevices)
                    {
                        var linkedTarget = _targets.FirstOrDefault(target =>
                            string.Equals(target.DeviceName, linkedDevice, StringComparison.OrdinalIgnoreCase));
                        if (linkedTarget != null)
                        {
                            linkedSettingsKeys.Add(linkedTarget.SettingsKey);
                        }
                    }
                    MarkLinkedGroup(linkedSettingsKeys);

                    var rollbackOrder = result.UnexpectedlyChangedDevices
                        .Concat(new[] { selected[0].DeviceName })
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    foreach (var rollbackDevice in rollbackOrder)
                    {
                        if (beforeRamps.TryGetValue(rollbackDevice, out var beforeRamp))
                        {
                            TrySetRamp(rollbackDevice, beforeRamp, out _);
                        }
                    }

                    // A later write to a shared LUT can change an output that was already checked.
                    // Verify every affected output only after all rollback writes have completed.
                    var rollbackReadbacks = new Dictionary<string, GammaRamp?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rollbackDevice in rollbackOrder)
                    {
                        var rollbackReadback = TryReadRamp(rollbackDevice, out _);
                        rollbackReadbacks[rollbackDevice] = rollbackReadback;
                        if (!beforeRamps.TryGetValue(rollbackDevice, out var beforeRamp))
                        {
                            result.RollbackFailedDevices.Add(rollbackDevice);
                            continue;
                        }

                        if (rollbackReadback == null || rollbackReadback.MaxDifference(beforeRamp) > ReadbackTolerance)
                        {
                            result.RollbackFailedDevices.Add(rollbackDevice);
                        }
                    }

                    var rollbackSucceeded = result.RollbackFailedDevices.Count == 0;
                    foreach (var rollbackDevice in rollbackOrder)
                    {
                        var restoredTarget = _targets.First(target =>
                            string.Equals(target.DeviceName, rollbackDevice, StringComparison.OrdinalIgnoreCase));
                        if (rollbackSucceeded)
                        {
                            restoredTarget.LastAppliedRamp =
                                ownershipBefore.TryGetValue(rollbackDevice, out var previousOwner)
                                    ? previousOwner?.Clone()
                                    : null;
                            restoredTarget.ForceRestoreOriginal =
                                forceRestoreBefore.TryGetValue(rollbackDevice, out var previousForce) && previousForce;
                            if (gammaBefore.TryGetValue(rollbackDevice, out var previousGamma))
                            {
                                restoredTarget.CurrentGamma = previousGamma;
                            }
                            continue;
                        }

                        // Rollback is atomic for a coupled set. If any member failed, retain
                        // ownership of every member at its final observed value so a retry cannot
                        // mistake our own partial rollback for an external takeover.
                        var finalReadback = rollbackReadbacks.GetValueOrDefault(rollbackDevice);
                        restoredTarget.LastAppliedRamp = (finalReadback ??
                            beforeRamps.GetValueOrDefault(rollbackDevice) ??
                            restoredTarget.OriginalRamp)?.Clone();
                        restoredTarget.ForceRestoreOriginal = finalReadback == null;
                    }

                    result.ErrorMessage = result.RollbackFailedDevices.Count == 0
                        ? UiText.Get(TextId.SharedLutRolledBack)
                        : UiText.Get(TextId.SharedLutRollbackFailed);
                }
            }

            return result;
        }
    }

    internal ApplyResult ReapplySavedGammas(IReadOnlyDictionary<string, double> gammaByDevice)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            var combined = new ApplyResult();
            var configured = _targets
                .Where(target => gammaByDevice.ContainsKey(target.SettingsKey))
                .Select(target => new { Target = target, Gamma = gammaByDevice[target.SettingsKey] })
                .ToArray();
            if (configured.Length == _targets.Count && configured.Length > 1 &&
                configured.All(item => Math.Abs(item.Gamma - configured[0].Gamma) <= 0.001))
            {
                return ApplyGamma(null, Math.Clamp(
                    configured[0].Gamma,
                    GammaRampBuilder.MinimumGamma,
                    GammaRampBuilder.MaximumGamma));
            }

            foreach (var target in _targets)
            {
                if (!gammaByDevice.TryGetValue(target.SettingsKey, out var savedGamma))
                {
                    continue;
                }

                var gamma = savedGamma;
                gamma = Math.Clamp(gamma, GammaRampBuilder.MinimumGamma, GammaRampBuilder.MaximumGamma);
                var result = ApplyGamma(target.DeviceName, gamma);
                combined.ChangedDevices.AddRange(result.ChangedDevices);
                combined.FailedDevices.AddRange(result.FailedDevices);
                combined.UnverifiedDevices.AddRange(result.UnverifiedDevices);
                combined.UnexpectedlyChangedDevices.AddRange(result.UnexpectedlyChangedDevices);
                combined.RollbackFailedDevices.AddRange(result.RollbackFailedDevices);
                combined.RolledBack |= result.RolledBack;
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    combined.ErrorMessage = result.ErrorMessage;
                }
            }

            foreach (var target in _targets)
            {
                if (target.LastAppliedRamp == null)
                {
                    continue;
                }

                var readback = TryReadRamp(target.DeviceName, out _);
                if (readback == null || readback.MaxDifference(target.LastAppliedRamp) > ReadbackTolerance)
                {
                    if (!combined.UnverifiedDevices.Contains(target.DeviceName, StringComparer.OrdinalIgnoreCase))
                    {
                        combined.UnverifiedDevices.Add(target.DeviceName);
                    }
                }
            }

            return combined;
        }
    }

    internal int RestoreOwnedRamps()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return 0;
            }

            var ownedTargets = _targets.Where(target =>
                    target.OriginalRamp != null &&
                    (target.LastAppliedRamp != null || target.ForceRestoreOriginal))
                .ToArray();
            var currentRamps = new Dictionary<string, GammaRamp?>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in ownedTargets)
            {
                currentRamps[target.SettingsKey] = TryReadRamp(target.DeviceName, out _);
            }

            var externalTakeovers = ownedTargets.Where(target =>
                !target.ForceRestoreOriginal && target.LastAppliedRamp != null &&
                currentRamps[target.SettingsKey] is { } current &&
                current.MaxDifference(target.LastAppliedRamp) > ReadbackTolerance)
                .Select(target => target.SettingsKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var candidates = new List<DisplayTarget>();
            // Never address a disconnected target through its old \\.\DISPLAYn name: Windows
            // may already have reassigned that name to another physical monitor. A reconnected
            // target is reused by its stable SettingsKey during Refresh and can then be restored.
            foreach (var target in ownedTargets)
            {
                var linkedGroupTakenOver =
                    _linkedGroupsBySettingsKey.TryGetValue(target.SettingsKey, out var linkedGroup) &&
                    linkedGroup.Any(externalTakeovers.Contains);
                var individuallyTakenOver = !target.ForceRestoreOriginal &&
                                            target.LastAppliedRamp != null &&
                                            externalTakeovers.Contains(target.SettingsKey);
                if (linkedGroupTakenOver || individuallyTakenOver)
                {
                    // Another program has taken ownership since our last verified write.
                    // Do not overwrite it and do not block exit for a ramp we no longer own.
                    target.LastAppliedRamp = null;
                    target.ForceRestoreOriginal = false;
                    continue;
                }

                candidates.Add(target);
            }

            foreach (var target in candidates)
            {
                TrySetRamp(target.DeviceName, target.OriginalRamp!, out _);
            }

            // Verify only after every write. On a shared LUT, a later display write can
            // overwrite an earlier one even though the earlier immediate read-back succeeded.
            var finalReadbacks = new Dictionary<string, GammaRamp?>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in candidates)
            {
                finalReadbacks[target.SettingsKey] = TryReadRamp(target.DeviceName, out _);
            }

            var allRestored = candidates.All(target =>
                finalReadbacks[target.SettingsKey] is { } readback &&
                readback.MaxDifference(target.OriginalRamp!) <= ReadbackTolerance);
            if (allRestored)
            {
                foreach (var target in candidates)
                {
                    target.LastAppliedRamp = null;
                    target.ForceRestoreOriginal = false;
                }

                return candidates.Count;
            }

            foreach (var target in candidates)
            {
                var readback = finalReadbacks[target.SettingsKey];
                if (readback != null)
                {
                    target.LastAppliedRamp = readback.Clone();
                    target.ForceRestoreOriginal = false;
                }
                else
                {
                    target.ForceRestoreOriginal = true;
                }
            }

            return 0;
        }
    }

    internal GammaRamp? ReadCurrentRamp(string deviceName, out string? error)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return TryReadRamp(deviceName, out error);
        }
    }

    private void MarkLinkedGroup(IEnumerable<string> settingsKeys)
    {
        var merged = new HashSet<string>(settingsKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var settingsKey in merged.ToArray())
        {
            if (_linkedGroupsBySettingsKey.TryGetValue(settingsKey, out var existingGroup))
            {
                merged.UnionWith(existingGroup);
            }
        }

        // A merged group may reveal another group transitively; repeat until stable.
        var previousCount = -1;
        while (previousCount != merged.Count)
        {
            previousCount = merged.Count;
            foreach (var member in merged.ToArray())
            {
                if (_linkedGroupsBySettingsKey.TryGetValue(member, out var existingGroup))
                {
                    merged.UnionWith(existingGroup);
                }
            }
        }

        foreach (var member in merged)
        {
            _linkedGroupsBySettingsKey[member] = merged;
        }
    }

    private static IReadOnlyDictionary<string, (string FriendlyName, string SettingsKey)>
        GetDisplayConfigIdentities()
    {
        var identities = new Dictionary<string, (string FriendlyName, string SettingsKey)>(
            StringComparer.OrdinalIgnoreCase);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var bufferResult = NativeMethods.GetDisplayConfigBufferSizes(
                NativeMethods.QdcOnlyActivePaths,
                out var pathCount,
                out var modeCount);
            if (bufferResult != NativeMethods.ErrorSuccess || pathCount == 0 ||
                pathCount > int.MaxValue || modeCount > int.MaxValue)
            {
                return identities;
            }

            var paths = new NativeMethods.DisplayConfigPathInfo[(int)pathCount];
            var modes = new NativeMethods.DisplayConfigModeInfo[(int)modeCount];
            var queryResult = NativeMethods.QueryDisplayConfig(
                NativeMethods.QdcOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);
            if (queryResult == NativeMethods.ErrorInsufficientBuffer)
            {
                continue;
            }

            if (queryResult != NativeMethods.ErrorSuccess)
            {
                return identities;
            }

            foreach (var path in paths.Take((int)pathCount))
            {
                var sourceName = new NativeMethods.DisplayConfigSourceDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigDeviceInfoGetSourceName,
                        Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigSourceDeviceName>(),
                        AdapterId = path.SourceInfo.AdapterId,
                        Id = path.SourceInfo.Id
                    },
                    ViewGdiDeviceName = string.Empty
                };
                if (NativeMethods.DisplayConfigGetSourceDeviceName(ref sourceName) !=
                    NativeMethods.ErrorSuccess || string.IsNullOrWhiteSpace(sourceName.ViewGdiDeviceName))
                {
                    continue;
                }

                var targetName = new NativeMethods.DisplayConfigTargetDeviceName
                {
                    Header = new NativeMethods.DisplayConfigDeviceInfoHeader
                    {
                        Type = NativeMethods.DisplayConfigDeviceInfoGetTargetName,
                        Size = (uint)Marshal.SizeOf<NativeMethods.DisplayConfigTargetDeviceName>(),
                        AdapterId = path.TargetInfo.AdapterId,
                        Id = path.TargetInfo.Id
                    },
                    MonitorFriendlyDeviceName = string.Empty,
                    MonitorDevicePath = string.Empty
                };
                if (NativeMethods.DisplayConfigGetTargetDeviceName(ref targetName) !=
                    NativeMethods.ErrorSuccess)
                {
                    continue;
                }

                var persistentId = targetName.MonitorDevicePath?.Trim();
                var settingsKey = !string.IsNullOrWhiteSpace(persistentId)
                    ? "monitor:" + persistentId
                    : $"target:{path.TargetInfo.AdapterId.HighPart:X8}{path.TargetInfo.AdapterId.LowPart:X8}:" +
                      path.TargetInfo.Id.ToString("X8");
                identities[sourceName.ViewGdiDeviceName.Trim()] = (
                    targetName.MonitorFriendlyDeviceName?.Trim() ?? string.Empty,
                    settingsKey);
            }

            return identities;
        }

        return identities;
    }

    private static (string FriendlyName, string SettingsKey) GetMonitorIdentity(
        string deviceName,
        IReadOnlyDictionary<string, (string FriendlyName, string SettingsKey)> displayConfigIdentities)
    {
        if (displayConfigIdentities.TryGetValue(deviceName, out var displayConfigIdentity))
        {
            return displayConfigIdentity;
        }

        static NativeMethods.DisplayDevice NewDisplayDevice() => new()
        {
            Size = Marshal.SizeOf<NativeMethods.DisplayDevice>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceId = string.Empty,
            DeviceKey = string.Empty
        };

        var device = NewDisplayDevice();
        var found = NativeMethods.EnumDisplayDevices(
            deviceName,
            0,
            ref device,
            NativeMethods.EddGetDeviceInterfaceName);
        if (!found)
        {
            device = NewDisplayDevice();
            found = NativeMethods.EnumDisplayDevices(deviceName, 0, ref device, 0);
        }

        var friendlyName = found && !string.IsNullOrWhiteSpace(device.DeviceString)
            ? device.DeviceString.Trim()
            : string.Empty;
        var persistentId = found && !string.IsNullOrWhiteSpace(device.DeviceId)
            ? device.DeviceId.Trim()
            : string.Empty;
        var settingsKey = string.IsNullOrWhiteSpace(persistentId)
            ? "logical:" + deviceName
            : "monitor:" + persistentId;
        return (friendlyName, settingsKey);
    }

    private static GammaRamp? TryReadRamp(string deviceName, out string? error)
    {
        error = null;
        var dc = NativeMethods.CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            error = UiText.Format(TextId.DisplayDcFailed, deviceName);
            return null;
        }

        try
        {
            var values = new ushort[GammaRamp.TotalLength];
            var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                if (!NativeMethods.GetDeviceGammaRamp(dc, handle.AddrOfPinnedObject()))
                {
                    error = UiText.Format(TextId.RampReadFailed, deviceName);
                    return null;
                }
            }
            finally
            {
                handle.Free();
            }

            return new GammaRamp(values);
        }
        finally
        {
            NativeMethods.DeleteDC(dc);
        }
    }

    private static bool TrySetRamp(string deviceName, GammaRamp ramp, out string? error)
    {
        error = null;
        var dc = NativeMethods.CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
        if (dc == IntPtr.Zero)
        {
            error = UiText.Format(TextId.DisplayDcFailed, deviceName);
            return false;
        }

        try
        {
            var values = (ushort[])ramp.Values.Clone();
            var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                if (!NativeMethods.SetDeviceGammaRamp(dc, handle.AddrOfPinnedObject()))
                {
                    error = UiText.Format(TextId.DriverRejected, deviceName);
                    return false;
                }

                return true;
            }
            finally
            {
                handle.Free();
            }
        }
        finally
        {
            NativeMethods.DeleteDC(dc);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MonitorGammaService));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}
