using System.Text;
using ComboLab.Models;

namespace ComboLab.Services;

public sealed class ShareTextService
{
    public string Create(ComboRecipe combo, ComboLibrary library)
    {
        var text = new StringBuilder();
        text.AppendLine($"【コンボ名】{ValueOrDash(combo.ComboName)}");
        text.AppendLine($"【ダメージ】{combo.Damage?.ToString() ?? "-"}");
        text.AppendLine(
            $"【タグ】{(combo.Tags.Count == 0 ? "なし" : string.Join(" / ", combo.Tags))}");
        text.AppendLine("【入力タイムライン】");

        if (combo.Steps.Count == 0)
        {
            text.AppendLine("未登録");
        }
        else
        {
            ExpandCombo(
                text,
                combo,
                library,
                baseFrame: 0,
                depth: 0,
                path: []);
        }

        if (!string.IsNullOrWhiteSpace(combo.Notes))
        {
            text.AppendLine();
            text.AppendLine("【メモ】");
            text.AppendLine(combo.Notes.Trim());
        }

        return text.ToString().TrimEnd();
    }

    private static void ExpandCombo(
        StringBuilder text,
        ComboRecipe combo,
        ComboLibrary library,
        int baseFrame,
        int depth,
        HashSet<Guid> path)
    {
        var indent = new string(' ', depth * 2);
        if (!path.Add(combo.Id))
        {
            text.AppendLine($"{indent}- [ループ参照: {combo.ComboName}]");
            return;
        }

        foreach (var node in combo.Steps)
        {
            var absoluteFrame = baseFrame + (node.InputFrame ?? 0);
            switch (node.NodeType)
            {
                case TimelineNodeType.Action:
                    text.AppendLine(
                        $"{indent}- [{absoluteFrame}F] {ValueOrDash(node.ActionName)}");
                    break;

                case TimelineNodeType.Note:
                    text.AppendLine(
                        $"{indent}- [{absoluteFrame}F] メモ: {ValueOrDash(node.MemoText)}");
                    break;

                case TimelineNodeType.ComboCall:
                    ExpandComboCall(
                        text,
                        node,
                        library,
                        absoluteFrame,
                        depth,
                        path);
                    break;
            }
        }

        path.Remove(combo.Id);
    }

    private static void ExpandComboCall(
        StringBuilder text,
        ComboStep node,
        ComboLibrary library,
        int absoluteFrame,
        int depth,
        HashSet<Guid> path)
    {
        var indent = new string(' ', depth * 2);
        if (node.CalledComboId is not Guid calledId)
        {
            text.AppendLine(
                $"{indent}- [{absoluteFrame}F] コンボ呼び出し: 参照先未設定");
            return;
        }

        var calledCombo = library.Combos.FirstOrDefault(combo => combo.Id == calledId);
        if (calledCombo is null)
        {
            text.AppendLine(
                $"{indent}- [{absoluteFrame}F] コンボ呼び出し: "
                + $"{ValueOrDash(node.CalledComboName)}（参照先なし）");
            return;
        }

        text.AppendLine(
            $"{indent}- [{absoluteFrame}F] コンボ呼び出し: {calledCombo.ComboName}");
        ExpandCombo(
            text,
            calledCombo,
            library,
            absoluteFrame,
            depth + 1,
            path);
    }

    private static string ValueOrDash(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
