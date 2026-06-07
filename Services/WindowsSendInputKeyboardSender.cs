using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ComboLab.Services;

public sealed class WindowsSendInputKeyboardSender : IKeyboardInputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;

    public KeyboardSendMode SendMode { get; set; } = KeyboardSendMode.ScanCode;

    public KeyboardSendResult SendBatch(
        IReadOnlyList<KeyboardInputChange> changes)
    {
        if (changes.Count == 0)
        {
            return new KeyboardSendResult(0, 0);
        }

        var batchTimestamp = GetTickCount();
        var inputs = changes
            .Select(change =>
            {
                var preview = CreateInputPreview(
                    change.Key,
                    SendMode,
                    isKeyUp: !change.IsKeyDown);
                return new Input
                {
                    Type = InputKeyboard,
                    Union = new InputUnion
                    {
                        Keyboard = new KeyboardInput
                        {
                            VirtualKey = preview.VirtualKey,
                            ScanCode = preview.ScanCode,
                            Flags = preview.Flags,
                            Time = batchTimestamp
                        }
                    }
                };
            })
            .ToArray();

        var sentCount = (int)SendInput(
            (uint)inputs.Length,
            inputs,
            InputStructureSize);
        if (sentCount != inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Windowsへキー入力を送信できませんでした。"
                + $"（要求: {inputs.Length}、送信: {sentCount}）");
        }

        return new KeyboardSendResult(
            inputs.Length,
            sentCount,
            batchTimestamp);
    }

    public static int InputStructureSize => Marshal.SizeOf<Input>();

    public static KeyboardInputPreview CreateInputPreview(
        PhysicalKey key,
        KeyboardSendMode sendMode,
        bool isKeyUp) =>
        new(
            sendMode == KeyboardSendMode.VirtualKey
                ? key.VirtualKey
                : (ushort)0,
            sendMode == KeyboardSendMode.ScanCode
                ? key.ScanCode
                : (ushort)0,
            (key.IsExtended ? KeyEventExtendedKey : 0)
            | (sendMode == KeyboardSendMode.ScanCode
                ? KeyEventScanCode
                : 0)
            | (isKeyUp ? KeyEventKeyUp : 0));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        Input[] inputs,
        int inputSize);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }
}

public readonly record struct KeyboardInputPreview(
    ushort VirtualKey,
    ushort ScanCode,
    uint Flags);
