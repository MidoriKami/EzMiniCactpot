using System.Numerics;
using KamiLib.Configuration;

namespace MiniCactpotSolver;

public class Configuration {
	public bool EnableAnimations = true;
	
	public Vector4 ButtonColor = new(1.0f, 1.0f, 1.0f, 0.80f);
	public Vector4 LaneColor = new(1.0f, 1.0f, 1.0f, 1.0f);

	public bool UseCustomIcon = true;
	
	public uint CustomIconId = 61332;
	
	public static Configuration Load()
		=> Service.PluginInterface.LoadConfigFile("EzMiniCactpot.config.json", () => new Configuration());

	public void Save()
		=> Service.PluginInterface.SaveConfigFile("EzMiniCactpot.config.json", this);
}