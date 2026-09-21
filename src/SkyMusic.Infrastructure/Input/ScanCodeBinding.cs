// 模块：SkyMusic.Infrastructure 输入设备 ScanCodeBinding
namespace SkyMusic.Infrastructure.Input;

public readonly record struct ScanCodeBinding(ushort ScanCode, bool IsExtended = false);
