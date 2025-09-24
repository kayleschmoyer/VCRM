// HistoryEntry.cs: Represents a row of contextual history data for vehicle timelines.
using System;
using MudBlazor;

namespace CRMAdapter.UI.Components.Vehicles;

public sealed record HistoryEntry(
    string Primary,
    string Secondary,
    string Tertiary,
    string? Quaternary,
    string Status,
    Color StatusColor,
    Guid? ReferenceId = null);
