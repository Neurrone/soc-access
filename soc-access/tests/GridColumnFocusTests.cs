using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class GridColumnFocusTests
    {
        [TestMethod]
        public void InventoryGridStartsUnvisitedColumnAtFirstRowAndRestoresPreviousColumnRow()
        {
            InventoryGridWidget grid = BuildInventoryGrid(3, 3);

            Assert.IsTrue(grid.SetFocusedCell(0, 1));
            Assert.IsTrue(grid.HandleAction(AccessibilityActions.NextColumn));
            Assert.AreEqual(1, grid.FocusedColumnIndex);
            Assert.AreEqual(0, grid.FocusedRowIndex);

            Assert.IsTrue(grid.HandleAction(AccessibilityActions.PreviousColumn));
            Assert.AreEqual(0, grid.FocusedColumnIndex);
            Assert.AreEqual(1, grid.FocusedRowIndex);
        }

        [TestMethod]
        public void InventoryGridRestoresRememberedRowsAcrossFocusStateRestore()
        {
            InventoryGridWidget grid = BuildInventoryGrid(3, 3);
            grid.SetFocusedCell(0, 2);
            grid.HandleAction(AccessibilityActions.NextColumn);
            grid.HandleAction(AccessibilityActions.NextRow);
            InventoryGridWidget.FocusState state = grid.CaptureFocusState();

            InventoryGridWidget restored = BuildInventoryGrid(3, 3);
            Assert.IsTrue(restored.RestoreFocusState(state));
            Assert.AreEqual(1, restored.FocusedColumnIndex);
            Assert.AreEqual(1, restored.FocusedRowIndex);

            Assert.IsTrue(restored.HandleAction(AccessibilityActions.PreviousColumn));
            Assert.AreEqual(0, restored.FocusedColumnIndex);
            Assert.AreEqual(2, restored.FocusedRowIndex);
        }

        [TestMethod]
        public void ArmyExchangeGridStartsUnvisitedColumnAtFirstRowAndRestoresPreviousColumnRow()
        {
            ArmyExchangeGridWidget grid = BuildArmyGrid(3, 3);

            Assert.IsTrue(grid.SetFocusedCell(0, 1));
            Assert.IsTrue(grid.HandleAction(AccessibilityActions.NextColumn));
            Assert.AreEqual(1, grid.FocusedColumnIndex);
            Assert.AreEqual(0, grid.FocusedRowIndex);

            Assert.IsTrue(grid.HandleAction(AccessibilityActions.PreviousColumn));
            Assert.AreEqual(0, grid.FocusedColumnIndex);
            Assert.AreEqual(1, grid.FocusedRowIndex);
        }

        [TestMethod]
        public void ArmyExchangeGridRestoresRememberedRowsAcrossFocusStateRestore()
        {
            ArmyExchangeGridWidget grid = BuildArmyGrid(3, 3);
            grid.SetFocusedCell(0, 2);
            grid.HandleAction(AccessibilityActions.NextColumn);
            grid.HandleAction(AccessibilityActions.NextRow);
            ArmyExchangeGridWidget.FocusState state = grid.CaptureFocusState();

            ArmyExchangeGridWidget restored = BuildArmyGrid(3, 3);
            Assert.IsTrue(restored.RestoreFocusState(state));
            Assert.AreEqual(1, restored.FocusedColumnIndex);
            Assert.AreEqual(1, restored.FocusedRowIndex);

            Assert.IsTrue(restored.HandleAction(AccessibilityActions.PreviousColumn));
            Assert.AreEqual(0, restored.FocusedColumnIndex);
            Assert.AreEqual(2, restored.FocusedRowIndex);
        }

        [TestMethod]
        public void TableGridHorizontalMovementKeepsCurrentRow()
        {
            TableWidget table = BuildTable();
            table.SetFocusedCell(2, 0);

            Assert.IsTrue(table.HandleAction(AccessibilityActions.NextColumn));

            Assert.AreEqual(1, table.FocusedColumnIndex);
            Assert.AreEqual(2, table.FocusedRowIndex);
        }

        private static InventoryGridWidget BuildInventoryGrid(params int[] rowCounts)
        {
            List<InventoryGridWidget.Column> columns = new List<InventoryGridWidget.Column>();
            for (int column = 0; column < rowCounts.Length; column++)
            {
                List<InventoryGridWidget.Cell> cells = new List<InventoryGridWidget.Cell>();
                for (int row = 0; row < rowCounts[column]; row++)
                {
                    cells.Add(new InventoryGridWidget.Cell(
                        "inventory-" + column + "-" + row,
                        "Inventory " + column + " " + row,
                        null));
                }

                columns.Add(new InventoryGridWidget.Column("inventory-" + column, "Inventory " + column, cells));
            }

            return new InventoryGridWidget("inventory-grid", columns);
        }

        private static ArmyExchangeGridWidget BuildArmyGrid(int leftRows, int rightRows)
        {
            return new ArmyExchangeGridWidget(
                "army-grid",
                "Left",
                "Right",
                BuildSlots(leftRows),
                BuildSlots(rightRows),
                null);
        }

        private static IReadOnlyList<TroopHudAdapter.SlotItem> BuildSlots(int count)
        {
            List<TroopHudAdapter.SlotItem> slots = new List<TroopHudAdapter.SlotItem>();
            for (int i = 0; i < count; i++)
            {
                slots.Add(new TroopHudAdapter.SlotItem(null, null));
            }

            return slots;
        }

        private static TableWidget BuildTable()
        {
            return new TableWidget(
                "table",
                "Table",
                new[]
                {
                    new TableWidget.Column("first", "First", null, null),
                    new TableWidget.Column("second", "Second", null, null)
                },
                new[]
                {
                    BuildRow("first"),
                    BuildRow("second"),
                    BuildRow("third")
                });
        }

        private static TableWidget.Row BuildRow(string id)
        {
            return new TableWidget.Row(
                id,
                id,
                columnId => id + " " + columnId,
                null,
                null,
                null);
        }
    }
}
