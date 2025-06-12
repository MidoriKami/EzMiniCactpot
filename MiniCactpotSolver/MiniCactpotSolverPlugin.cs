using Dalamud.Plugin;
using KamiLib.Window;
using KamiToolKit;

namespace MiniCactpotSolver;

public sealed class MiniCactpotPlugin : IDalamudPlugin {

    public MiniCactpotPlugin(IDalamudPluginInterface pluginInterface) {
        pluginInterface.Create<Service>();

        Service.Config = Configuration.Load();
        Service.WindowManager = new WindowManager(Service.PluginInterface);
        Service.WindowManager.AddWindow(new ConfigWindow(), WindowFlags.IsConfigWindow);

        Service.NativeController = new NativeController(pluginInterface);
        Service.AddonController = new LotteryDailyController();
    }

    public void Dispose() {
        Service.WindowManager.Dispose();
        
        Service.AddonController.Dispose();
        Service.NativeController.Dispose();
    }
}