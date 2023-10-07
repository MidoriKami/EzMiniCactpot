using Dalamud.Plugin;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace MiniCactpotSolver;

public sealed class MiniCactpotPlugin : IDalamudPlugin
{
    private const int TotalNumbers = PerfectCactpot.TotalNumbers;
    private const int TotalLanes = PerfectCactpot.TotalLanes;
    private int[] gameState = new int[TotalNumbers];

    public MiniCactpotPlugin(DalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Service.Framework.Update += FrameworkLotteryPoll;
    }

    public void Dispose()
    {
        Service.Framework.Update -= FrameworkLotteryPoll;
    }

    #region GameLogic

    private readonly PerfectCactpot perfectCactpot = new();
    private Task gameTask;

    private void FrameworkLotteryPoll(IFramework framework)
    {
        try
        {
            if (Service.ClientState.TerritoryType != 144)  // Golden Saucer
                return;

            var addonPtr = Service.GameGui.GetAddonByName("LotteryDaily");
            if (addonPtr == IntPtr.Zero)
                return;

            if (gameTask == null || gameTask.IsCompleted || gameTask.IsFaulted || gameTask.IsCanceled)
            {
                gameTask = Task.Run(() => GameUpdater(addonPtr));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "Updater has crashed");
            Service.ChatGui.PrintError($"ezMiniCactpot has encountered a critical error");
        }
    }

    private unsafe void GameUpdater(IntPtr addonPtr)
    {
        var ready = false;
        var isVisible = false;
        AddonLotteryDaily* addon = (AddonLotteryDaily*)addonPtr;

        var rootNode = addon->AtkUnitBase.RootNode;
        if (rootNode != null)
        {
            isVisible = addon->AtkUnitBase.IsVisible;
            ready = true;
        }

        if (!ready)
        {
            for (int i = 0; i < TotalNumbers; i++)
            {
                this.gameState[i] = 0;
            }
        }

        if (!isVisible)
            return;

        var localGameState = GetGameState(addon);
        if (!localGameState.SequenceEqual(this.gameState))
        {
            this.gameState = localGameState;

            if (!localGameState.Contains(0))
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

                var solution = perfectCactpot.Solve(localGameState);

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
        ToggleNode(addon->LaneSelector[i]->AtkComponentBase.OwnerNode, enable);
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

    #endregion GameLogic
}