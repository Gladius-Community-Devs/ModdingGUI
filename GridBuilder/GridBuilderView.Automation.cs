namespace ModdingGUI
{
    using System.Text.Json;
    using ModdingGUI.Core.Grids;

    /// <summary>
    /// The Grid Builder's automation surface: what the UI driver (and therefore an AI agent) can read and change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view is a <see cref="UserControl"/> with custom painting, so the generic driver commands (click a button, set a
    /// text box) cannot express "put Start1 on cell 12,7". These methods do, and they go through the same
    /// <see cref="GridFile"/> the UI edits, so a scripted edit and a mouse edit are the same edit.
    /// </para>
    /// <para>
    /// Every method returns a JSON string; failures carry <c>ok:false</c> with a reason rather than throwing, because the
    /// caller is a script that cannot see a stack trace.
    /// </para>
    /// </remarks>
    public partial class GridBuilderView
    {
        private static string Json(object value) => JsonSerializer.Serialize(value);

        private static string Fail(string reason) => Json(new { ok = false, reason });

        /// <summary>Project, arena, slots and dirty state: enough to know what is loaded before touching cells.</summary>
        public string AutomationState()
        {
            GridFile grid = CurrentGrid;

            var slots = new List<object>();
            for (int slot = 0; slot < GridFile.NumSlots; slot++)
            {
                string tag = grid.GetTagNameForSlot(slot);
                if (string.IsNullOrWhiteSpace(tag) || tag == GridFile.NO_VALUE) continue;

                slots.Add(new { slot, tag, active = grid.IsSlotActive(slot), draw = (CurrentDrawMask & (1 << slot)) != 0 });
            }

            int painted = 0;
            for (int row = 0; row < GridFile.ArenaSize; row++)
            {
                for (int col = 0; col < GridFile.ArenaSize; col++)
                {
                    if (grid.GetValue(row, col) != 0) painted++;
                }
            }

            return Json(new
            {
                ok = true,
                project = ProjectRoot,
                arena = _arenaBox.SelectedItem?.ToString(),
                arenas = _arenaBox.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
                layoutVersion = grid.LayoutVersion,
                writable = grid.IsWritableLayout,
                dirty = grid.IsDirty,
                size = GridFile.ArenaSize,
                paintedCells = painted,
                drawMask = CurrentDrawMask,
                slots,
                mapVisible = _btnToggleMap.Text == "Hide Map",
                paintTags = _paintSlotBox.Items.Cast<object>().Select(o => o.ToString()).ToArray(),
                hasMapImage = _currentArenaMapImage is not null,
            });
        }

        /// <summary>One cell: its raw value and the tags whose bits are set.</summary>
        public string AutomationCell(int row, int col)
        {
            if (!InRange(row, col)) return Fail($"cell {row},{col} is outside 0..{GridFile.ArenaSize - 1}");

            GridFile grid = CurrentGrid;
            int value = grid.GetValue(row, col);
            var tags = new List<string>();

            for (int slot = 0; slot < GridFile.NumSlots; slot++)
            {
                if ((value & (1 << slot)) == 0) continue;
                string tag = grid.GetTagNameForSlot(slot);
                if (!string.IsNullOrWhiteSpace(tag) && tag != GridFile.NO_VALUE) tags.Add(tag);
            }

            return Json(new { ok = true, row, col, value, tags });
        }

        /// <summary>Set or clear one tag on one cell, by tag name.</summary>
        public string AutomationSetTag(int row, int col, string tag, bool on)
        {
            if (!InRange(row, col)) return Fail($"cell {row},{col} is outside 0..{GridFile.ArenaSize - 1}");

            GridFile grid = CurrentGrid;
            int slot = grid.IndexOfTag(tag);
            if (slot < 0)
            {
                // Tell the caller what it can use: an agent should be able to recover without another round trip.
                return Json(new
                {
                    ok = false,
                    reason = $"this grid has no active tag named '{tag}'",
                    availableTags = AvailableTags(),
                });
            }

            int value = grid.GetValue(row, col);
            int updated = on ? value | (1 << slot) : value & ~(1 << slot);
            grid.SetValue(row, col, updated);

            _panel.Invalidate();
            _btnSave.Enabled = true;
            AppendLog($"{(on ? "Set" : "Cleared")} {tag} on {row},{col} (value {value} -> {updated}).", "Gray");

            return Json(new { ok = true, row, col, tag, slot, before = value, value = updated });
        }

        /// <summary>Clear every tag from a cell.</summary>
        public string AutomationClearCell(int row, int col)
        {
            if (!InRange(row, col)) return Fail($"cell {row},{col} is outside 0..{GridFile.ArenaSize - 1}");

            GridFile grid = CurrentGrid;
            int before = grid.GetValue(row, col);
            grid.SetValue(row, col, 0);

            _panel.Invalidate();
            _btnSave.Enabled = true;
            AppendLog($"Cleared {row},{col} (value {before} -> 0).", "Gray");

            return Json(new { ok = true, row, col, before, value = 0 });
        }

        /// <summary>Choose which tag mouse painting applies, by tag name.</summary>
        public string AutomationSelectSlot(string tag)
        {
            for (int index = 0; index < _paintSlotBox.Items.Count; index++)
            {
                string? item = _paintSlotBox.Items[index]?.ToString();
                if (!string.Equals(item, tag, StringComparison.OrdinalIgnoreCase)) continue;

                _paintSlotBox.SelectedIndex = index;
                return Json(new { ok = true, drawMask = CurrentDrawMask, slot = index, tag = item });
            }

            return Json(new
            {
                ok = false,
                reason = $"'{tag}' is not in this grid's paint slot list",
                availableTags = AvailableTags(),
            });
        }

        /// <summary>Load an arena by name from the current project.</summary>
        public string AutomationLoadArena(string name)
        {
            if (string.IsNullOrWhiteSpace(ProjectRoot)) return Fail("no project is selected; pick one in the project tree");

            for (int index = 0; index < _arenaBox.Items.Count; index++)
            {
                string? item = _arenaBox.Items[index]?.ToString();
                if (!string.Equals(item, name, StringComparison.OrdinalIgnoreCase)) continue;

                _arenaBox.SelectedIndex = index;   // raises SelectedIndexChanged, which loads the grid
                return Json(new
                {
                    ok = true,
                    arena = item,
                    layoutVersion = CurrentGrid.LayoutVersion,
                    writable = CurrentGrid.IsWritableLayout,
                });
            }

            return Fail($"'{name}' is not available for this project");
        }

        /// <summary>Write the open grid back to its .grd.</summary>
        public string AutomationSave()
        {
            if (!_btnSave.Enabled) return Fail("nothing to save: the grid is unmodified or none is loaded");

            SaveArena();
            return Json(new { ok = true, dirty = CurrentGrid.IsDirty });
        }

        /// <summary>Show or hide the arena map under the grid, and optionally set its transform.</summary>
        public string AutomationSetMap(bool visible, int? offsetX, int? offsetY, decimal? scale, int? rotationIndex)
        {
            if (visible && _btnToggleMap.Text != "Hide Map") ToggleMap_Click(_btnToggleMap, EventArgs.Empty);
            if (!visible && _btnToggleMap.Text == "Hide Map") ToggleMap_Click(_btnToggleMap, EventArgs.Empty);

            if (offsetX is int x) _mapOffsetXNumeric.Value = Math.Clamp(x, (int)_mapOffsetXNumeric.Minimum, (int)_mapOffsetXNumeric.Maximum);
            if (offsetY is int y) _mapOffsetYNumeric.Value = Math.Clamp(y, (int)_mapOffsetYNumeric.Minimum, (int)_mapOffsetYNumeric.Maximum);
            if (scale is decimal s) _mapScaleNumeric.Value = Math.Clamp(s, _mapScaleNumeric.Minimum, _mapScaleNumeric.Maximum);
            if (rotationIndex is int r) _mapRotationBox.SelectedIndex = Math.Clamp(r, 0, Math.Max(0, _mapRotationBox.Items.Count - 1));

            _panel.Invalidate();

            bool nowVisible = _btnToggleMap.Text == "Hide Map";
            if (visible && !nowVisible)
            {
                // The toggle only turns on when the arena has a map image; say so instead of reporting success.
                return Json(new { ok = false, reason = $"no map image for '{_arenaBox.SelectedItem}'", visible = false });
            }

            return Json(new
            {
                ok = true,
                visible = nowVisible,
                offsetX = (int)_mapOffsetXNumeric.Value,
                offsetY = (int)_mapOffsetYNumeric.Value,
                scale = _mapScaleNumeric.Value,
                rotation = _mapRotationBox.SelectedIndex,
            });
        }

        /// <summary>Every tag this grid can address, for failure messages.</summary>
        private string[] AvailableTags()
        {
            var tags = new List<string>();
            GridFile grid = CurrentGrid;

            for (int slot = 0; slot < GridFile.NumSlots; slot++)
            {
                if (!grid.IsSlotActive(slot)) continue;

                string tag = grid.GetTagNameForSlot(slot);
                if (!string.IsNullOrWhiteSpace(tag) && tag != GridFile.NO_VALUE) tags.Add(tag);
            }

            return tags.ToArray();
        }

        private static bool InRange(int row, int col) =>
            row >= 0 && row < GridFile.ArenaSize && col >= 0 && col < GridFile.ArenaSize;
    }
}
