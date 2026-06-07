using System.Collections.ObjectModel;
using ComboLab.Models;

namespace ComboLab.Services;

public sealed class ComboLibraryMigrationService
{
    public ComboLibrary NormalizeAndMigrate(ComboLibrary library)
    {
        library.Combos ??= [];
        library.ActionDefinitions ??= [];

        var folderPaths = BuildFolderPaths(library.Folders ?? []);
        foreach (var combo in library.Combos)
        {
            NormalizeCombo(combo, folderPaths);
        }

        NormalizeActionDefinitions(library.ActionDefinitions);
        RefreshCalledComboNames(library);
        library.Folders = null;
        library.SchemaVersion = 7;
        return library;
    }

    private static void NormalizeActionDefinitions(
        ObservableCollection<ActionDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (definition.Id == Guid.Empty)
            {
                definition.Id = Guid.NewGuid();
            }

            definition.ActionName = string.IsNullOrWhiteSpace(definition.ActionName)
                ? "新しいアクション"
                : definition.ActionName.Trim();
            definition.Description ??= string.Empty;
            definition.InputEvents ??= [];

            foreach (var inputEvent in definition.InputEvents)
            {
                inputEvent.Frame = Math.Max(0, inputEvent.Frame);
                inputEvent.LogicalInputs ??= [];
                inputEvent.LogicalInputs = new ObservableCollection<string>(
                    inputEvent.LogicalInputs
                        .Where(input => !string.IsNullOrWhiteSpace(input))
                        .Select(input => input.Trim())
                        .Distinct(StringComparer.CurrentCultureIgnoreCase));
            }
        }
    }

    private static void NormalizeCombo(
        ComboRecipe combo,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> folderPaths)
    {
        if (combo.Id == Guid.Empty)
        {
            combo.Id = Guid.NewGuid();
        }

        combo.GameName = "Street Fighter 6";
        combo.ComboName = string.IsNullOrWhiteSpace(combo.ComboName)
            ? "新しいコンボ"
            : combo.ComboName.Trim();
        combo.Tags ??= [];
        combo.Notes ??= string.Empty;
        combo.Steps ??= [];
        combo.Steps = MigrateTimeline(combo.Steps);

        var migrationTags = new List<string>();
        AddIfPresent(migrationTags, combo.CharacterName);
        AddIfPresent(migrationTags, combo.ScreenPosition);
        AddIfPresent(migrationTags, combo.MeterUsage);
        AddIfPresent(migrationTags, combo.Purpose);
        AddIfPresent(migrationTags, combo.FreeCategory);

        if (folderPaths.TryGetValue(combo.FolderId, out var folderPath))
        {
            migrationTags.AddRange(folderPath);
        }

        foreach (var tag in migrationTags)
        {
            if (!combo.Tags.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
            {
                combo.Tags.Add(tag);
            }
        }

        NormalizeTags(combo.Tags);
        combo.FolderId = Guid.Empty;
        combo.CharacterName = string.Empty;
        combo.ScreenPosition = string.Empty;
        combo.StarterCondition = string.Empty;
        combo.MeterUsage = string.Empty;
        combo.Purpose = string.Empty;
        combo.FreeCategory = string.Empty;
    }

    private static ObservableCollection<ComboStep> MigrateTimeline(
        IReadOnlyList<ComboStep> steps)
    {
        var currentFrame = 0;
        var migrated = new ObservableCollection<ComboStep>();

        foreach (var step in steps)
        {
            if (step.LegacyStepType is null)
            {
                NormalizeNode(step);
                migrated.Add(step);
                continue;
            }

            currentFrame = step.InputFrame is int frame && frame > 0
                ? frame
                : currentFrame + step.LegacyWaitFrames;

            var node = ConvertLegacyStep(step, currentFrame);
            migrated.Add(node);

            if (!string.IsNullOrWhiteSpace(step.LegacyMemo)
                && step.LegacyStepType != ComboStepType.Note
                && step.LegacyStepType != ComboStepType.ManualWait)
            {
                migrated.Add(new ComboStep
                {
                    NodeType = TimelineNodeType.Note,
                    InputFrame = currentFrame,
                    MemoText = step.LegacyMemo.Trim()
                });
            }

            if (step.LegacyStepType == ComboStepType.ManualWait)
            {
                currentFrame = 0;
            }
        }

        return migrated;
    }

    private static ComboStep ConvertLegacyStep(ComboStep step, int inputFrame)
    {
        var legacyType = step.LegacyStepType ?? ComboStepType.Input;
        if (legacyType is ComboStepType.Note or ComboStepType.ManualWait)
        {
            return new ComboStep
            {
                NodeType = TimelineNodeType.Note,
                InputFrame = legacyType == ComboStepType.ManualWait ? 0 : inputFrame,
                MemoText = legacyType == ComboStepType.ManualWait
                    ? $"手動待機: {step.LegacyMemo}".TrimEnd(' ', ':')
                    : step.LegacyMemo?.Trim() ?? string.Empty
            };
        }

        return new ComboStep
        {
            NodeType = TimelineNodeType.Action,
            InputFrame = inputFrame,
            ActionName = FormatLegacyAction(step, legacyType)
        };
    }

    private static string FormatLegacyAction(
        ComboStep step,
        ComboStepType legacyType)
    {
        var input = step.LegacyInputName?.Trim() ?? string.Empty;
        return legacyType switch
        {
            ComboStepType.Wait => "待機",
            ComboStepType.Hold => string.IsNullOrWhiteSpace(input)
                ? $"長押し {step.LegacyHoldFrames}F"
                : $"{input} 長押し {step.LegacyHoldFrames}F",
            ComboStepType.Release => string.IsNullOrWhiteSpace(input)
                ? "離す"
                : $"{input} を離す",
            _ => input
        };
    }

    private static void NormalizeNode(ComboStep node)
    {
        node.InputFrame = Math.Max(0, node.InputFrame ?? 0);
        node.ActionName ??= string.Empty;
        node.MemoText ??= string.Empty;
        node.CalledComboName ??= string.Empty;
    }

    private static void RefreshCalledComboNames(ComboLibrary library)
    {
        var comboNames = library.Combos.ToDictionary(
            combo => combo.Id,
            combo => combo.ComboName);

        foreach (var node in library.Combos.SelectMany(combo => combo.Steps))
        {
            if (node.NodeType == TimelineNodeType.ComboCall
                && node.CalledComboId is Guid calledId
                && comboNames.TryGetValue(calledId, out var comboName))
            {
                node.CalledComboName = comboName;
            }
        }
    }

    private static void NormalizeTags(ObservableCollection<string> tags)
    {
        var normalized = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Where(tag => !string.Equals(
                tag,
                "未分類",
                StringComparison.CurrentCultureIgnoreCase))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(tag => tag, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        tags.Clear();
        foreach (var tag in normalized)
        {
            tags.Add(tag);
        }
    }

    private static Dictionary<Guid, IReadOnlyList<string>> BuildFolderPaths(
        IEnumerable<ComboFolder> folders)
    {
        var result = new Dictionary<Guid, IReadOnlyList<string>>();
        AddFolderPaths(folders, [], result);
        return result;
    }

    private static void AddFolderPaths(
        IEnumerable<ComboFolder> folders,
        IReadOnlyList<string> parentPath,
        IDictionary<Guid, IReadOnlyList<string>> result)
    {
        foreach (var folder in folders)
        {
            var name = folder.Name?.Trim();
            var path = string.IsNullOrWhiteSpace(name)
                ? parentPath
                : parentPath.Append(name).ToArray();
            result[folder.Id] = path;
            AddFolderPaths(folder.Children ?? [], path, result);
        }
    }

    private static void AddIfPresent(ICollection<string> tags, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !string.Equals(
                value.Trim(),
                "未分類",
                StringComparison.CurrentCultureIgnoreCase))
        {
            tags.Add(value.Trim());
        }
    }
}
