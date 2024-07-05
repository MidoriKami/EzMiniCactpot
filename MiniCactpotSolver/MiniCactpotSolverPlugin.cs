using Dalamud.Plugin;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace MiniCactpotSolver;

public sealed class MiniCactpotPlugin : IDalamudPlugin
{
    private readonly PerfectCactpot perfectCactpot = new();
    private Task gameTask;

    private const int TotalNumbers = PerfectCactpot.TotalNumbers;
    private const int TotalLanes = PerfectCactpot.TotalLanes;
    private int[] gameState = new int[TotalNumbers];

    public MiniCactpotPlugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "LotteryDaily", AddonSetupDetour);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "LotteryDaily", AddonFinalizeDetour);
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(AddonSetupDetour);
        Service.AddonLifecycle.UnregisterListener(AddonStateChanged);
        Service.AddonLifecycle.UnregisterListener(AddonFinalizeDetour);
    }

    private void AddonSetupDetour(AddonEvent type, AddonArgs args)
    {
        // This addon calls Refresh before Setup, so we have to wait until it is setup completely before we can start to listen for refreshes.
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, "LotteryDaily", AddonStateChanged);
        
        // However, this causes us to miss the first refresh, so we have to trigger our calculation now that setup is done.
        AddonStateChanged(type, args);
    }
    
    private void AddonStateChanged(AddonEvent type, AddonArgs args)
    {
        try
        {
            if (gameTask is null or { Status: TaskStatus.RanToCompletion or TaskStatus.Faulted or TaskStatus.Canceled })
            {
                gameTask = Task.Run(() => GameUpdater(args.Addon));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Updater has crashed");
            Service.ChatGui.PrintError("ezMiniCactpot has encountered a critical error");
        }
    }
        
    private void AddonFinalizeDetour(AddonEvent type, AddonArgs args)
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "LotteryDaily", AddonStateChanged);
    }

    private unsafe void GameUpdater(IntPtr addonPtr)
    {
        var addon = (AddonLotteryDaily*)addonPtr;
        
        gameState = GetGameState(addon);
        if (!gameState.Contains(0))
        {
            // Perform this check for when the entire board is revealed, no unknowns/zeroes
            for (var i = 0; i < TotalNumbers; i++)
                ToggleGameNode(addon, i, false);
            for (var i = 0; i < TotalLanes; i++)
                ToggleLaneNode(addon, i, false);
        }
        else
        {
            for (var i = 0; i < TotalNumbers; i++)
                ToggleGameNode(addon, i, false);  // Reset the number colors

            var solution = perfectCactpot.Solve(gameState);

            if (solution.Length == 8)
            {
                // The PerfectCactbot lane array is formatted differently than the UI when it gives lane solutions.
                solution = new[]
                {
                    solution[6],  // major diagonal
                    solution[3],  // left column
                    solution[4],  // center column
                    solution[5],  // right column
                    solution[7],  // minor diagonal
                    solution[0],  // top row
                    solution[1],  // middle row
                    solution[2],  // bottom row
                };

                for (var i = 0; i < TotalNumbers; i++)
                    ToggleGameNode(addon, i, false);  // Reset the number colors

                for (var i = 0; i < TotalLanes; i++)
                    ToggleLaneNode(addon, i, solution[i]);
            }
            else
            {
                for (var i = 0; i < TotalNumbers; i++)
                    ToggleGameNode(addon, i, solution[i]);
            }
        }
    }

    private unsafe int[] GetGameState(AddonLotteryDaily* addon)
    {
        return Enumerable.Range(0, TotalNumbers).Select(i => addon->GameNumbers[i]).ToArray();
    }

    private unsafe void ToggleGameNode(AddonLotteryDaily* addon, int i, bool enable)
    {
        ToggleNode(addon->GameBoard[i]->AtkComponentButton.AtkComponentBase.OwnerNode, enable);
    }

    private unsafe void ToggleLaneNode(AddonLotteryDaily* addon, int i, bool enable)
    {
        ToggleNode(addon->LaneSelector[i]->OwnerNode, enable);
    }

    private unsafe void ToggleNode(AtkComponentNode* node, bool enable)
    {
        if (enable)
        {
            node->AtkResNode.MultiplyRed = 0;
            node->AtkResNode.MultiplyGreen = 100;
            node->AtkResNode.MultiplyBlue = 0;
        }
        else
        {
            node->AtkResNode.MultiplyRed = 100;
            node->AtkResNode.MultiplyGreen = 100;
            node->AtkResNode.MultiplyBlue = 100;
        }
    }
}