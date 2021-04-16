using Dalamud.Plugin;
using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Threading.Tasks;
using Dalamud.Game.Internal;

namespace MiniCactpotSolver
{
    public sealed class MiniCactpotPlugin : IDalamudPlugin
    {
        public string Name => "ezMiniCactpot";

        internal DalamudPluginInterface Interface;

        private const int TotalNumbers = PerfectCactpot.TotalNumbers;
        private const int TotalLanes = PerfectCactpot.TotalLanes;
        private int[] GameState = new int[TotalNumbers];

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            Interface.Framework.OnUpdateEvent += GameUpdater;
        }

        public void Dispose()
        {
            Interface.Framework.OnUpdateEvent -= GameUpdater;
        }

        #region GameLogic

        private readonly PerfectCactpot PerfectCactpot = new PerfectCactpot();
        private Task GameTask;

        private void GameUpdater(Framework framework)
        {
            try
            {
                if (GameTask == null || GameTask.IsCompleted || GameTask.IsFaulted || GameTask.IsCanceled)
                {
                    GameTask = new Task(GameUpdater);
                    GameTask.Start();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Updater has crashed");
                Interface.Framework.Gui.Chat.PrintError($"{Name} has encountered a critical error");
            }
        }

        private unsafe void GameUpdater()
        {
            if (Interface.ClientState.TerritoryType != 144)  // Golden Saucer
                return;

            var ready = false;
            var isVisible = false;
            AddonLotteryDaily* addon = null;
            var addonPtr = Interface.Framework.Gui.GetUiObjectByName("LotteryDaily", 1);

            if (addonPtr != IntPtr.Zero)
            {
                addon = (AddonLotteryDaily*)addonPtr;
                var rootNode = addon->AtkUnitBase.RootNode;
                if (rootNode != null)
                {
                    isVisible = addon->AtkUnitBase.IsVisible;
                    ready = true;
                }
            }

            if (!ready)
                for (int i = 0; i < TotalNumbers; i++)
                    GameState[i] = 0;

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
