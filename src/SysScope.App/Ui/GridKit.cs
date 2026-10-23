using SysScope.Core.Formatting;

namespace SysScope;

internal static class GridKit
{
    private static bool _fitting;

    public static DataGridView Create()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = true,
            MultiSelect = true,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            RowHeadersVisible = false,
            ColumnHeadersVisible = true,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
            GridColor = Theme.Line,
            EnableHeadersVisualStyles = false,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ShowCellToolTips = true,
            ScrollBars = ScrollBars.Both,
            Font = Theme.Body(),
            RowTemplate = { MinimumHeight = 26 }
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.HeaderRow;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Ink;
        grid.ColumnHeadersDefaultCellStyle.Font = Theme.Emphasis();
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.ForeColor = Theme.Ink;
        grid.DefaultCellStyle.SelectionBackColor = Theme.Select;
        grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.DefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 250, 252);
        ApplyRowMetrics(grid);
        typeof(DataGridView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(grid, true);
        grid.CellFormatting += FormatKnownValues;
        grid.ColumnWidthChanged += (_, e) =>
        {
            if (_fitting || e.Column is null)
                return;
            RefreshTips(grid, e.Column);
        };
        grid.CellToolTipTextNeeded += ShowTipIfTruncated;
        AddCopyMenu(grid);
        return grid;
    }

    public static DataGridViewTextBoxColumn Column(string property, string header, int minWidth, float fill = 100)
    {
        var headerFont = Theme.Emphasis();
        var measured = TextRenderer.MeasureText(header, headerFont).Width + 36;
        return new DataGridViewTextBoxColumn
        {
            Name = property,
            DataPropertyName = property,
            HeaderText = header,
            MinimumWidth = Math.Max(minWidth, measured),
            FillWeight = fill,
            SortMode = DataGridViewColumnSortMode.Automatic,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = new DataGridViewCellStyle { WrapMode = DataGridViewTriState.False }
        };
    }

    public static void Bind<T>(DataGridView grid, IEnumerable<T> rows)
    {
        grid.DataSource = new BindingSource { DataSource = new SortableBindingList<T>(rows) };
        Fit(grid);
    }

    /// <summary>
    /// Keeps every header on one line and at least as wide as its title, then widens to the cell text.
    /// </summary>
    public static void Fit(DataGridView grid)
    {
        if (_fitting || grid.Columns.Count == 0)
            return;
        _fitting = true;
        try
        {
            grid.ColumnHeadersVisible = true;
            grid.EnableHeadersVisualStyles = false;
            ApplyRowMetrics(grid);
            var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? Theme.Emphasis();
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
                var headerWidth = TextRenderer.MeasureText(column.HeaderText, headerFont).Width + 36;
                var width = Math.Max(column.MinimumWidth, headerWidth);
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                    var cell = row.Cells[column.Index];
                    var text = cell.FormattedValue?.ToString() ?? "";
                    if (text.Length == 0)
                        continue;
                    var font = cell.InheritedStyle?.Font ?? grid.Font;
                    var cellWidth = TextRenderer.MeasureText(text, font).Width + 22;
                    if (cellWidth > width)
                        width = cellWidth;
                }

                if (column.Name == "FreeText")
                    width += 96;
                column.MinimumWidth = headerWidth;
                column.Width = width;
            }

            foreach (DataGridViewColumn column in grid.Columns)
                RefreshTips(grid, column);
        }
        finally
        {
            _fitting = false;
        }
    }

    public static void FitAll(params DataGridView[] grids)
    {
        foreach (var grid in grids)
            Fit(grid);
    }

    private static void ApplyRowMetrics(DataGridView grid)
    {
        var headerFont = grid.ColumnHeadersDefaultCellStyle.Font ?? Theme.Emphasis();
        var rowFont = grid.DefaultCellStyle.Font ?? grid.Font;
        var headerHeight = TextRenderer.MeasureText("Ag", headerFont).Height + 14;
        var rowHeight = TextRenderer.MeasureText("Ag", rowFont).Height + 10;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = Math.Max(30, headerHeight);
        grid.RowTemplate.Height = Math.Max(26, rowHeight);
        grid.RowTemplate.MinimumHeight = grid.RowTemplate.Height;
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Height = grid.RowTemplate.Height;
            row.MinimumHeight = grid.RowTemplate.Height;
        }
    }

    private static void RefreshTips(DataGridView grid, DataGridViewColumn column)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow)
                continue;
            var cell = row.Cells[column.Index];
            var text = cell.FormattedValue?.ToString() ?? "";
            cell.ToolTipText = IsTruncated(grid, cell, text) ? text : "";
        }
    }

    private static void ShowTipIfTruncated(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;
        var cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        var text = cell.FormattedValue?.ToString() ?? "";
        if (IsTruncated(grid, cell, text))
            e.ToolTipText = text;
    }

    private static bool IsTruncated(DataGridView grid, DataGridViewCell cell, string text)
    {
        if (text.Length == 0)
            return false;
        var font = cell.InheritedStyle?.Font ?? grid.Font;
        var pad = cell.InheritedStyle?.Padding ?? Padding.Empty;
        var measured = TextRenderer.MeasureText(text, font).Width;
        var available = cell.Size.Width - pad.Horizontal - 8;
        return measured > available;
    }

    private static void FormatKnownValues(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid || e.ColumnIndex < 0 || e.Value is null)
            return;
        var property = grid.Columns[e.ColumnIndex].DataPropertyName;
        if (e.Value is DateTimeOffset dto && property is "InstallDate" or "InstalledOn" or "DriverDate" or "BiosDate")
        {
            e.Value = DisplayFormatter.Date(dto);
            e.FormattingApplied = true;
            return;
        }

        if (e.Value is long bytes && property is "SizeBytes" or "AdapterRamBytes")
        {
            e.Value = ByteSizeFormatter.Format(bytes);
            e.FormattingApplied = true;
        }
    }

    private static void AddCopyMenu(DataGridView grid)
    {
        var menu = new ContextMenuStrip();
        var cell = new ToolStripMenuItem("Copy value");
        var row = new ToolStripMenuItem("Copy row");
        cell.Click += (_, _) => CopyCell(grid);
        row.Click += (_, _) => CopyRow(grid);
        menu.Items.Add(cell);
        menu.Items.Add(row);
        grid.ContextMenuStrip = menu;
        grid.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.C && grid.CurrentCell is not null)
            {
                CopyCell(grid);
                e.Handled = true;
            }
        };
    }

    public static event Action<string>? Copied;

    public static void CopyCell(DataGridView grid)
    {
        var text = grid.CurrentCell?.FormattedValue?.ToString() ?? grid.CurrentCell?.Value?.ToString();
        if (string.IsNullOrEmpty(text))
            return;
        Clipboard.SetText(text);
        Copied?.Invoke(text);
    }

    private static void CopyRow(DataGridView grid)
    {
        if (grid.CurrentCell is null)
            return;
        var values = grid.Columns.Cast<DataGridViewColumn>()
            .OrderBy(column => column.DisplayIndex)
            .Select(column => grid.Rows[grid.CurrentCell.RowIndex].Cells[column.Index].FormattedValue?.ToString() ?? "")
            .ToArray();
        var text = string.Join("\t", values);
        Clipboard.SetText(text);
        Copied?.Invoke(text);
    }
}
