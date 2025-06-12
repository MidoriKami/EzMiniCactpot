using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KamiLib.Window;
using KamiToolKit;

namespace MiniCactpotSolver;

internal class Service {
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    
    internal static NativeController NativeController { get; set; } = null!;
    internal static LotteryDailyController AddonController { get; set; } = null!;
    internal static Configuration Config { get; set; } = null!;
    internal static WindowManager WindowManager { get; set; } = null!;
}