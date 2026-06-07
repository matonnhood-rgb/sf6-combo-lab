namespace ComboLab.Services;

public readonly record struct PhysicalKey(
    ushort VirtualKey,
    ushort ScanCode,
    string DisplayName,
    bool IsExtended = false);
