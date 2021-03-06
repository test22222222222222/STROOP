﻿using STROOP.Controls;
using STROOP.Structs;
using STROOP.Structs.Configurations;
using STROOP.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STROOP.Managers
{
    public class TasManager : DataManager
    {
        private DataGridView _dataGridViewTas;
        private CheckBox _checkBoxTasRecordData;
        private Button _buttonTasClearData;
        private RichTextBox _richTextBoxTasInstructions;

        private static readonly int WAITING_TIME_MS = 0;

        private uint _waitingGlobalTimer;
        private DateTime _waitingDateTime;
        private uint _lastUpdatedGlobalTimer;

        private Dictionary<uint, TasDataStruct> _dataDictionary;
        private Dictionary<uint, DataGridViewRow> _rowDictionary;

        //private static readonly int TABLE_INDEX_GLOBAL_TIMER = 0;
        //private static readonly int TABLE_INDEX_CURRENT_CAM_ANGLE = 1;
        private static readonly int TABLE_INDEX_NEXT_CAM_ANGLE = 2;
        //private static readonly int TABLE_INDEX_MARIO_FACING_ANGLE = 3;
        //private static readonly int TABLE_INDEX_MARIO_INTEND_ANGLE = 4;
        //private static readonly int TABLE_INDEX_DANGLE = 5;
        private static readonly int TABLE_INDEX_X_INPUT = 6;
        private static readonly int TABLE_INDEX_Y_INPUT = 7;

        private static readonly List<VariableGroup> ALL_VAR_GROUPS =
            new List<VariableGroup>()
            {
                VariableGroup.Basic,
                VariableGroup.Advanced,
                VariableGroup.TAS,
                VariableGroup.Point,
            };

        private static readonly List<VariableGroup> VISIBLE_VAR_GROUPS =
            new List<VariableGroup>()
            {
                VariableGroup.Basic,
                VariableGroup.Advanced,
                VariableGroup.Point,
            };

        public TasManager(string varFilePath, TabPage tabControl, WatchVariableFlowLayoutPanel watchVariablePanel)
            : base(varFilePath, watchVariablePanel, ALL_VAR_GROUPS, VISIBLE_VAR_GROUPS)
        {
            SplitContainer splitContainerTas = tabControl.Controls["splitContainerTas"] as SplitContainer;
            SplitContainer splitContainerTasTable = splitContainerTas.Panel1.Controls["splitContainerTasTable"] as SplitContainer;
            _dataGridViewTas = splitContainerTasTable.Panel2.Controls["dataGridViewTas"] as DataGridView;
            _checkBoxTasRecordData = splitContainerTasTable.Panel1.Controls["checkBoxTasRecordData"] as CheckBox;
            _buttonTasClearData = splitContainerTasTable.Panel1.Controls["buttonTasClearData"] as Button;
            _buttonTasClearData.Click += (sender, e) => ClearData();
            _richTextBoxTasInstructions = splitContainerTasTable.Panel1.Controls["richTextBoxTasInstructions"] as RichTextBox;

            Button buttonTasStoreMarioPosition = splitContainerTasTable.Panel1.Controls["buttonTasStoreMarioPosition"] as Button;
            buttonTasStoreMarioPosition.Click += (sender, e) => StoreMarioInfo(x: true, y: true, z: true);
            ControlUtilities.AddContextMenuStripFunctions(
                buttonTasStoreMarioPosition,
                new List<string>() { "Store Position", "Store Lateral Position", "Store X", "Store Y", "Store Z" },
                new List<Action>() {
                    () => StoreMarioInfo(x: true, y: true, z: true),
                    () => StoreMarioInfo(x: true, z: true),
                    () => StoreMarioInfo(x: true),
                    () => StoreMarioInfo(y: true),
                    () => StoreMarioInfo(z: true),
                });

            Button buttonTasStoreMarioAngle = splitContainerTasTable.Panel1.Controls["buttonTasStoreMarioAngle"] as Button;
            buttonTasStoreMarioAngle.Click += (sender, e) => StoreMarioInfo(angle: true);

            _waitingGlobalTimer = 0;
            _waitingDateTime = DateTime.Now;
            _lastUpdatedGlobalTimer = 0;

            _dataDictionary = new Dictionary<uint, TasDataStruct>();
            _rowDictionary = new Dictionary<uint, DataGridViewRow>();
        }

        private void StoreMarioInfo(
            bool x = false, bool y = false, bool z = false, bool angle = false)
        {
            if (x) SpecialConfig.CustomX = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.XOffset);
            if (y) SpecialConfig.CustomY = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.YOffset);
            if (z) SpecialConfig.CustomZ = Config.Stream.GetSingle(MarioConfig.StructAddress + MarioConfig.ZOffset);
            if (angle) SpecialConfig.CustomAngle = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);
        }

        private class TasDataStruct
        {
            public readonly uint GlobalTimer;
            public readonly ushort MarioFacingAngle;
            public readonly ushort MarioIntendAngle;
            public readonly short DAngle;
            public readonly ushort CameraAngle;
            public readonly sbyte BufferedXInput;
            public readonly sbyte BufferedYInput;
            public readonly sbyte CurrentXInput;
            public readonly sbyte CurrentYInput;

            public TasDataStruct(uint? globalTimer = null)
            {
                GlobalTimer = globalTimer ?? Config.Stream.GetUInt32(MiscConfig.GlobalTimerAddress);
                MarioFacingAngle = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.FacingYawOffset);
                MarioIntendAngle = Config.Stream.GetUInt16(MarioConfig.StructAddress + MarioConfig.IntendedYawOffset);
                DAngle = WatchVariableSpecialUtilities.GetDeltaYawIntendedFacing();
                CameraAngle = Config.Stream.GetUInt16(CameraConfig.StructAddress + CameraConfig.CentripetalAngleOffset);
                BufferedXInput = Config.Stream.GetSByte(InputConfig.BufferedInputAddress + InputConfig.ControlStickXOffset);
                BufferedYInput = Config.Stream.GetSByte(InputConfig.BufferedInputAddress + InputConfig.ControlStickYOffset);
                CurrentXInput = Config.Stream.GetSByte(InputConfig.CurrentInputAddress + InputConfig.ControlStickXOffset);
                CurrentYInput = Config.Stream.GetSByte(InputConfig.CurrentInputAddress + InputConfig.ControlStickYOffset);
            }

            public override bool Equals(object obj)
            {
                if (!(obj is TasDataStruct)) return false;
                TasDataStruct other = obj as TasDataStruct;
                return this.GlobalTimer == other.GlobalTimer
                    && this.MarioFacingAngle == other.MarioFacingAngle
                    && this.MarioIntendAngle == other.MarioIntendAngle
                    && this.DAngle == other.DAngle
                    && this.CameraAngle == other.CameraAngle
                    && this.BufferedXInput == other.BufferedXInput
                    && this.BufferedYInput == other.BufferedYInput
                    && this.CurrentXInput == other.CurrentXInput
                    && this.CurrentYInput == other.CurrentYInput;
            }

            public object[] GetValues()
            {
                return new object[]
                {
                    GlobalTimer,
                    MarioFacingAngle,
                    MarioIntendAngle,
                    DAngle,
                    CameraAngle,
                    BufferedXInput,
                    BufferedYInput,
                    CurrentXInput,
                    CurrentYInput
                };
            }

            public override int GetHashCode()
            {
                return GetValues().GetHashCode();
            }
        }

        private void ClearData()
        {
            _waitingGlobalTimer = 0;
            _waitingDateTime = DateTime.Now;
            _lastUpdatedGlobalTimer = 0;

            _dataGridViewTas.Rows.Clear();
            _dataDictionary.Clear();
            _rowDictionary.Clear();

            List<string> newInstructions = new List<string>()
            {
                "(1)", "(2)", "(3)", "(4)", "(5)",
            };
            _richTextBoxTasInstructions.Text = String.Join("\r\n", newInstructions);
        }

        private void ClearDataAtAndAfter(uint globalTimerThreshold)
        {
            List<uint> allGlobalTimers = _dataDictionary.Keys.ToList();
            List<uint> toBeRemovedGlobalTimers =
                allGlobalTimers.FindAll(globalTimer => globalTimer >= globalTimerThreshold);
            foreach (uint globalTimer in toBeRemovedGlobalTimers)
            {
                DataGridViewRow row = _rowDictionary[globalTimer];
                _dataDictionary.Remove(globalTimer);
                _rowDictionary.Remove(globalTimer);
                _dataGridViewTas.Rows.Remove(row);
            }
        }

        public void ShowTaserVariables()
        {
            _variablePanel.ShowOnlyVariableGroups(
                new List<VariableGroup>() { VariableGroup.TAS, VariableGroup.Custom });
        }

        public override void Update(bool updateView)
        {
            if (!updateView) return;
            base.Update(updateView);

            if (!_checkBoxTasRecordData.Checked) return;

            // Get current data
            uint currentGlobalTimer = Config.Stream.GetUInt32(MiscConfig.GlobalTimerAddress);
            TasDataStruct currentData = new TasDataStruct(currentGlobalTimer);

            // Only proceed if it's a new global timer value that's been waited for
            if (currentGlobalTimer == _lastUpdatedGlobalTimer) return;
            if (currentGlobalTimer != _waitingGlobalTimer)
            {
                _waitingGlobalTimer = currentGlobalTimer;
                _waitingDateTime = DateTime.Now;
                return;
            }
            else
            {
                DateTime currentTime = DateTime.Now;
                double waitingTime = currentTime.Subtract(_waitingDateTime).TotalMilliseconds;
                if (waitingTime < WAITING_TIME_MS) return;
                _lastUpdatedGlobalTimer = currentGlobalTimer;
            }

            // Clear any bad data
            if (_dataDictionary.ContainsKey(currentGlobalTimer) &&
                !currentData.Equals(_dataDictionary[currentGlobalTimer]))
            {
                ClearDataAtAndAfter(currentGlobalTimer);
            }

            // Add current data if we don't have it
            if (!_dataDictionary.ContainsKey(currentGlobalTimer))
            {
                _dataDictionary.Add(currentGlobalTimer, currentData);
                _dataGridViewTas.Rows.Add(
                    currentData.GlobalTimer,
                    currentData.CameraAngle,
                    "" /* NextCameraAngle */,
                    currentData.MarioFacingAngle,
                    currentData.MarioIntendAngle,
                    currentData.DAngle,
                    "" /* X Input */,
                    "" /* Z Input */);
                DataGridViewRow lastRow = _dataGridViewTas.Rows[_dataGridViewTas.RowCount - 1];
                _rowDictionary.Add(currentGlobalTimer, lastRow);
            }

            // Select the current row
            DataGridViewRow currentRow = _rowDictionary[currentGlobalTimer];
            currentRow.Selected = true;
            _dataGridViewTas.FirstDisplayedScrollingRowIndex = _dataGridViewTas.Rows.IndexOf(currentRow);

            // If we have the next row, then calculate the inputs of the current row
            uint nextGlobalTimer = currentGlobalTimer + 1;
            if (_dataDictionary.ContainsKey(nextGlobalTimer))
            {
                TasDataStruct nextData = _dataDictionary[nextGlobalTimer];
                ushort nextCameraAngle = nextData.CameraAngle;
                ushort goalAngle = currentData.MarioFacingAngle;
                (int xInput, int yInput) = MoreMath.CalculateInputsForAngle(goalAngle, nextCameraAngle);
                currentRow.Cells[TABLE_INDEX_NEXT_CAM_ANGLE].Value = nextCameraAngle;
                currentRow.Cells[TABLE_INDEX_X_INPUT].Value = xInput;
                currentRow.Cells[TABLE_INDEX_Y_INPUT].Value = -1 * yInput;

                List<string> newInstructions = new List<string>()
                {
                    "(1) Set input to (" + xInput + "," + (-1 * yInput) + ")",
                    "(2) Frame advance",
                    "(3) Savestate",
                    "(4) Frame advance",
                    "(5) Revert",
                };
                _richTextBoxTasInstructions.Text = String.Join("\r\n", newInstructions);
            }
        }
    }
}
