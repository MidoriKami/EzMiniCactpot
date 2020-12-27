using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.Component.GUI;
using Dalamud.Game.Internal;
using System.Threading.Tasks;
using System.Threading;

namespace MiniCactpotSolver
{
    public sealed class MiniCactpotPlugin : IDalamudPlugin
    {
        public string Name => "ezMiniCactpot";

        private const int TotalNumbers = 9;
        private const int TotalLanes = 8;

        internal DalamudPluginInterface Interface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");

            this.Interface.UiBuilder.OnBuildUi += UiBuilder_OnBuildUi_Overlay;
            this.Interface.Framework.OnUpdateEvent += Framework_OnUpdateEvent;
        }

        public void Dispose()
        {
            this.Interface.UiBuilder.OnBuildUi -= UiBuilder_OnBuildUi_Overlay;
            this.Interface.Framework.OnUpdateEvent -= Framework_OnUpdateEvent;
        }

        #region GameLogic

        private readonly PerfectCactpot PerfectCactpot = new PerfectCactpot();
        private int[] previousState = new int[9];

        private async void Framework_OnUpdateEvent(Framework framework)
        {
            if (Interface.ClientState.TerritoryType != 144)  // Golden Saucer
                return;

            UpdateGameData();

            if (!MiniCactpotGameData.IsVisible)
                return;

            var gameState = GetSolverState();
            if (!Enumerable.SequenceEqual(gameState, previousState))
            {
                previousState = gameState;

                if (!gameState.Contains(0))
                {
                    // Perform this check for when the entire board is revealed, no unknowns/zeroes
                    for (var i = 0; i < TotalNumbers; i++)
                        ToggleNumberNode(i, false);
                    for (var i = 0; i < TotalLanes; i++)
                        ToggleLaneNode(i, false);
                }
                else
                {
                    var solution = await Task.Run(() => PerfectCactpot.Solve(gameState));

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
                            ToggleNumberNode(i, false);  // Reset the number colors

                        for (var i = 0; i < TotalLanes; i++)
                            ToggleLaneNode(i, solution[i]);
                    }
                    else
                    {
                        for (var i = 0; i < TotalNumbers; i++)
                            ToggleNumberNode(i, solution[i]);
                    }
                }
            }
        }

        internal static unsafe class MiniCactpotGameData
        {
            public static float X;
            public static float Y;
            public static ushort Width;
            public static ushort Height;
            public static bool IsVisible;
            public static AtkComponentNode*[] NumberNodes = new AtkComponentNode*[TotalNumbers];
            public static AtkComponentNode*[] LaneNodes = new AtkComponentNode*[TotalLanes];
        }

        private unsafe void UpdateGameData()
        {
            var addon = Interface.Framework.Gui.GetAddonByName("LotteryDaily", 1);

            if (addon is null || addon.Address == IntPtr.Zero)
                MiniCactpotGameData.IsVisible = false;

            var uiAddon = (AtkUnitBase*)addon.Address;

            MiniCactpotGameData.X = uiAddon->RootNode->X;
            MiniCactpotGameData.Y = uiAddon->RootNode->Y;
            MiniCactpotGameData.Width = (ushort)(uiAddon->RootNode->Width * uiAddon->RootNode->ScaleX);
            MiniCactpotGameData.Height = (ushort)(uiAddon->RootNode->Height * uiAddon->RootNode->ScaleY);
            MiniCactpotGameData.IsVisible = (uiAddon->Flags & 0x20) == 0x20;

            var baseParentNode = uiAddon->RootNode
                ->ChildNode->PrevSiblingNode->PrevSiblingNode
                ->ChildNode->PrevSiblingNode->PrevSiblingNode->PrevSiblingNode;

            var numberNodeParent = baseParentNode->ChildNode->PrevSiblingNode;
            var numberNode = numberNodeParent->ChildNode;
            for (var i = 0; i < TotalNumbers; i++)
            {
                if ((ulong)numberNode == 0)
                    throw new Exception("Problem fetching number node");
                MiniCactpotGameData.NumberNodes[TotalNumbers - 1 - i] = (AtkComponentNode*)numberNode;
                numberNode = numberNode->PrevSiblingNode;
            }

            var laneNodeParent = numberNodeParent->PrevSiblingNode;
            var laneNode = laneNodeParent->ChildNode;
            for (var i = 0; i < TotalLanes; i++)
            {
                if ((ulong)laneNode == 0)
                    throw new Exception("Problem fetching lane node");
                MiniCactpotGameData.LaneNodes[TotalLanes - 1 - i] = (AtkComponentNode*)laneNode;
                laneNode = laneNode->PrevSiblingNode;
            }
        }

        /// <summary>
        /// Get the list of revealed numbers PerfectCactbot style
        /// </summary>
        /// <param name="numberNodes">Current UI data</param>
        /// <returns>Int array of numbers, 0 for unknown.</returns>
        private unsafe int[] GetSolverState()
        {
            var state = new int[9];
            for (var i = 0; i < MiniCactpotGameData.NumberNodes.Length; i++)
            {
                var node = MiniCactpotGameData.NumberNodes[i];
                var compNode = (AtkComponentCheckBox*)node->Component;
                var textNode = (AtkTextNode*)compNode->AtkComponentButton.AtkComponentBase.ULDData.NodeList[2];
                if ((ulong)textNode == 0)
                    throw new Exception("Problem getting text node");

                var numberByte = textNode->NodeText.InlineBuffer[0];
                if (numberByte == 0)
                    state[i] = 0;
                else
                    state[i] = numberByte - 48;  // ASCII ordinal
            }
            return state;
        }

        private unsafe void ToggleNumberNode(int index, bool enable) => ToggleNode(MiniCactpotGameData.NumberNodes[index], enable);
        private unsafe void ToggleLaneNode(int index, bool enable) => ToggleNode(MiniCactpotGameData.LaneNodes[index], enable);

        /// <summary>
        /// Flipflop the color of a given node to/from green
        /// </summary>
        /// <param name="node">Node to color</param>
        /// <param name="enable">Green or not</param>
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
