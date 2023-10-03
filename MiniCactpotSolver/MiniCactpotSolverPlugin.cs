using Dalamud.Plugin;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Logging;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace MiniCactpotSolver
{
    public sealed class MiniCactpotPlugin : IDalamudPlugin
    {
        private const int TotalNumbers = PerfectCactpot.TotalNumbers;
        private const int TotalLanes = PerfectCactpot.TotalLanes;
        private int[] GameState = new int[TotalNumbers];

        internal DalamudPluginInterface Interface { get; init; }
        internal IChatGui ChatGui { get; init; }
        internal IClientState ClientState { get; init; }
        internal IFramework Framework { get; init; }
        internal IGameGui GameGui { get; init; }
        internal IPluginLog PluginLog { get; init; }

        public MiniCactpotPlugin(
            DalamudPluginInterface pluginInterface,
            IChatGui chatGui,
            IClientState clientState,
            IFramework framework,
            IGameGui gameGui,
            IPluginLog pluginLog)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            ChatGui = chatGui;
            ClientState = clientState;
            Framework = framework;
            GameGui = gameGui;
            PluginLog = pluginLog;

            Framework.Update += FrameworkLotteryPoll;
        }

        public void Dispose()
        {
            Framework.Update -= FrameworkLotteryPoll;
        }

        #region GameLogic

        private readonly PerfectCactpot PerfectCactpot = new();
        private Task GameTask;

        private void FrameworkLotteryPoll(IFramework framework)
        {
            try
            {
                if (ClientState.TerritoryType != 144)  // Golden Saucer
                    return;

                var addonPtr = GameGui.GetAddonByName("LotteryDaily", 1);
                if (addonPtr == IntPtr.Zero)
                    return;

                if (GameTask == null || GameTask.IsCompleted || GameTask.IsFaulted || GameTask.IsCanceled)
                {
                    GameTask = Task.Run(() => GameUpdater(addonPtr));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Updater has crashed");
                ChatGui.PrintError($"ezMiniCactpot has encountered a critical error");
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
                    GameState[i] = 0;
                }
            }

            if (!isVisible)
                return;

            var gameState = GetGameState(addon);
            if (!Enumerable.SequenceEqual(gameState, GameState))
            {
                GameState = gameState;

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

                    var solution = PerfectCactpot.Solve(gameState);

                    if (solution.Length == 8)
                    {
                        // The PerfectCactbot lane array is formatted differently than the UI when it gives lane solutions.
                        solution = new bool[]
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
}
