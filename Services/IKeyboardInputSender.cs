namespace ComboLab.Services;

public interface IKeyboardInputSender
{
    KeyboardSendResult SendBatch(
        IReadOnlyList<KeyboardInputChange> changes);
}
