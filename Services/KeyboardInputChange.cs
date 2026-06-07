namespace ComboLab.Services;

public readonly record struct KeyboardInputChange(
    PhysicalKey Key,
    bool IsKeyDown);

public readonly record struct KeyboardSendResult(
    int RequestedCount,
    int SentCount,
    uint Timestamp = 0);
