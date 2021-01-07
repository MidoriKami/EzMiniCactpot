using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.Component.GUI;
using System.Threading.Tasks;
using System.Threading;
using FFXIVClientStructs.Client.UI;

namespace MiniCactpotSolver
{
    public sealed class MiniCactpotPlugin : IDalamudPlugin
    {
        public string Name => "ezMiniCactpot";

        internal DalamudPluginInterface Interface;

        private const int TotalNumbers = PerfectCactpot.TotalNumbers;
        private const int TotalLanes = PerfectCactpot.TotalLanes;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            this.Interface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi_Overlay;
            this.QueueLoopToken = new CancellationTokenSource();
            this.QueueLoopTask = Task.Run(() => GameBoardUpdaterLoop(QueueLoopToken.Token));
        }

        public async void Dispose()
        {
            this.Interface.UiBuilder.OnBuildUi -= UiBuilder_OnBuildUi_Overlay;
            this.QueueLoopToken.Cancel();
            await this.QueueLoopTask;
        }

        #region GameLogic

        private readonly PerfectCactpot PerfectCactpot = new PerfectCactpot();
        private Task QueueLoopTask;
        private CancellationTokenSource QueueLoopToken;

        private async void GameBoardUpdaterLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100);
                GameBoardUpdater();
            }
        }

        private unsafe void GameBoardUpdater()
        {
            if (Interface.ClientState.TerritoryType != 144)  // Golden Saucer
                return;

            bool ready = false;
            var addon = Interface.Framework.Gui.GetUiObjectByName("LotteryDaily", 1);
            if (addon != IntPtr.Zero)
            {
                var uiAddon = MiniCactpotGameData.Addon = (AddonDailyLottery*)addon;
                var rootNode = uiAddon->AtkUnitBase.RootNode;
                if (rootNode != null)
                {
                    MiniCactpotGameData.X = rootNode->X;
                    MiniCactpotGameData.Y = rootNode->Y;
                    MiniCactpotGameData.Width = (ushort)(rootNode->Width * rootNode->ScaleX);
                    MiniCactpotGameData.Height = (ushort)(rootNode->Height * rootNode->ScaleY);
                    MiniCactpotGameData.IsVisible = uiAddon->AtkUnitBase.IsVisible;
                    ready = true;
                }
            }

            if (!ready)
            {
                MiniCactpotGameData.Addon = null;
                MiniCactpotGameData.X = 0;
                MiniCactpotGameData.Y = 0;
                MiniCactpotGameData.Width = 0;
                MiniCactpotGameData.Height = 0;
                MiniCactpotGameData.IsVisible = false;
                for (int i = 0; i < TotalNumbers; i++)
                    MiniCactpotGameData.GameState[i] = 0;
            }

            if (!MiniCactpotGameData.IsVisible)
                return;

            var gameState = GetGameState();
            if (!Enumerable.SequenceEqual(gameState, MiniCactpotGameData.GameState))
            {
                MiniCactpotGameData.GameState = gameState;

                if (!gameState.Contains(0))
                {
                    // Perform this check for when the entire board is revealed, no unknowns/zeroes
                    for (var i = 0; i < TotalNumbers; i++)
                        ToggleGameNode(i, false);
                    for (var i = 0; i < TotalLanes; i++)
                        ToggleLaneNode(i, false);
                }
                else
                {
                    for (var i = 0; i < TotalNumbers; i++)
                        ToggleGameNode(i, false);  // Reset the number colors

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
                            ToggleGameNode(i, false);  // Reset the number colors

                        for (var i = 0; i < TotalLanes; i++)
                            ToggleLaneNode(i, solution[i]);
                    }
                    else
                    {
                        for (var i = 0; i < TotalNumbers; i++)
                            ToggleGameNode(i, solution[i]);
                    }
                }
            }
        }

        internal static unsafe class MiniCactpotGameData
        {
            public static AddonDailyLottery* Addon = null;
            public static float X = 0;
            public static float Y = 0;
            public static ushort Width = 0;
            public static ushort Height = 0;
            public static bool IsVisible = false;
            public static int[] GameState = new int[TotalNumbers];
        }

        private unsafe int[] GetGameState()
        {
            return Enumerable.Range(0, TotalNumbers).Select(i => MiniCactpotGameData.Addon->GameNumbers[i]).ToArray();
        }

        private unsafe void ToggleGameNode(int i, bool enable)
        {
            ToggleNode(MiniCactpotGameData.Addon->GameBoard[i]->AtkComponentButton.AtkComponentBase.OwnerNode, enable);
        }

        private unsafe void ToggleLaneNode(int i, bool enable)
        {
            ToggleNode(MiniCactpotGameData.Addon->LaneSelector[i]->AtkComponentBase.OwnerNode, enable);
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

        #region ImGui

        private void UiBuilder_OnBuildUi_Overlay()
        {
            if (!MiniCactpotGameData.IsVisible)
                return;

            var x = MiniCactpotGameData.X;
            var y = MiniCactpotGameData.Y;
            var w = MiniCactpotGameData.Width;
            var h = MiniCactpotGameData.Height;
            ImGui.SetNextWindowPos(new Vector2(x + w / 2 - 3, y + h - 35), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w / 2, 30), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.0f);

            ImGui.PushStyleColor(ImGuiCol.ResizeGrip, 0);  // Hide the resize window grip
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));

            bool alwaysTrue = true;
            ImGui.Begin("FFXIV Cactpot Solver", ref alwaysTrue,
                ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoTitleBar);

            var poweredText = $"Powered by PerfectCactpot ";
            var tooltipText = "(?)";
            var textSize = ImGui.CalcTextSize(poweredText + tooltipText);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetColumnWidth() - textSize.X - ImGui.GetScrollX() - 2 * ImGui.GetStyle().ItemSpacing.X);  // right aligned
            ImGui.Text(poweredText);
            ImGui.SameLine();
            ImGui.TextDisabled(tooltipText);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("https://super-aardvark.github.io/yuryu/");
                ImGui.Text("Original by /u/yuryu");
                ImGui.Text("Improved recommendations by /u/aureolux and /u/super_aardvark");
                ImGui.EndTooltip();
            }

            ImGui.End();

            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
        }

        #endregion ImGui
    }
}
