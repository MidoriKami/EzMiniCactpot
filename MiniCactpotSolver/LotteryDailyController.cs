using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.UI;
using KamiToolKit;

namespace MiniCactpotSolver;

public unsafe class LotteryDailyController : IDisposable {
	private readonly AddonController<AddonLotteryDaily> addonLotteryDaily;
    private readonly PerfectCactpot perfectCactpot = new();

    private int[]? boardState;
    private GameGrid? gameGrid;
    private Task? gameTask;

    public LotteryDailyController() {
		addonLotteryDaily = new AddonController<AddonLotteryDaily>(Service.PluginInterface, "LotteryDaily");
		
		addonLotteryDaily.OnAttach += AddonLotteryDailyOnAttach;
		addonLotteryDaily.OnDetach += AddonLotteryDailyOnDetach;
		addonLotteryDaily.OnUpdate += AddonLotteryDailyOnUpdate;
		
		addonLotteryDaily.Enable();
    }

	public void Dispose() {
		addonLotteryDaily.Dispose();
		
		Service.NativeController.DetachNode(gameGrid, () => {
			gameGrid?.Dispose();
			gameGrid = null;
		});
		
		gameTask?.Dispose();
	}

	public void DisableAnimations()
		=> gameGrid?.Timeline?.StartAnimation(201);

	public void EnableAnimations()
		=> gameGrid?.Timeline?.StartAnimation(200);

	public void UpdateIcons(uint icon)
		=> gameGrid?.UpdateIcons(icon);

	public void UpdateButtonColors(Vector4 color) 
		=> gameGrid?.UpdateButtonColors(color);
	
	public void UpdateLaneColors(Vector4 color)
		=> gameGrid?.UpdateLaneColors(color);

	private void AddonLotteryDailyOnAttach(AddonLotteryDaily* addon) {
		if (addon is null) return;

		var buttonContainerNode = addon->GetNodeById(8);
		if (buttonContainerNode is null) return;

		gameGrid = new GameGrid {
			Size = new Vector2(542.0f, 320.0f),
			IsVisible = true,
		};
		
		Service.NativeController.AttachNode(gameGrid, buttonContainerNode);
	}
	
	private void AddonLotteryDailyOnUpdate(AddonLotteryDaily* addon) {
		var newState = Enumerable.Range(0, 9).Select(i => addon->GameNumbers[i]).ToArray();
		if (!boardState?.SequenceEqual(newState) ?? true) {
			try {
				if (gameTask is null or { Status: TaskStatus.RanToCompletion or TaskStatus.Faulted or TaskStatus.Canceled }) {
					gameTask = Task.Run(() => {
			    
						if (!newState.Contains(0)) {
							gameGrid?.SetActiveButtons(null);
							gameGrid?.SetActiveLanes(null);
						}
						else {
							var solution = perfectCactpot.Solve(newState);
							var activeIndexes = solution
								.Select((value, index) => new { value, index })
								.Where(item => item.value)
								.Select(item => item.index)
								.ToArray();
					
							if (solution.Length is 8) {
								gameGrid?.SetActiveButtons(null);
								gameGrid?.SetActiveLanes(activeIndexes);
							}
							else {
								gameGrid?.SetActiveButtons(activeIndexes);
								gameGrid?.SetActiveLanes(null);
							}
						}
					});
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception ex) {
				Service.Log.Error(ex, "Updater has crashed");
			}
		}
		
		boardState = newState;
	}
	
	private void AddonLotteryDailyOnDetach(AddonLotteryDaily* addon) {
		Service.NativeController.DetachNode(gameGrid, () => {
			gameGrid?.Dispose();
			gameGrid = null;
		});
	}
}