internal sealed class MainForm : Form
{
    private const string HtmlReportOption = "HTML";
    private const string TextReportOption = "Text";

    private readonly XmlDiffService _diffService = new();
    private readonly TextBox _leftPathTextBox = CreatePathTextBox();
    private readonly TextBox _rightPathTextBox = CreatePathTextBox();
    private readonly TextBox _outputPathTextBox = CreatePathTextBox();
    private readonly TextBox _sortRulesTextBox = new()
    {
        AcceptsReturn = true,
        AcceptsTab = false,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };
    private readonly CheckBox _formatXmlCheckBox = new() { Text = "Format XML fragments" };
    private readonly CheckBox _formatJsonCheckBox = new() { Text = "Format embedded JSON" };
    private readonly CheckBox _sortTagsCheckBox = new() { Text = "Sort sibling tags by name" };
    private readonly ComboBox _reportFormatComboBox = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 140
    };
    private readonly Button _compareButton = new() { Text = "Compare", AutoSize = true };
    private readonly Button _saveReportButton = new() { Text = "Save report", AutoSize = true, Enabled = false };
    private readonly Label _statusLabel = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly WebBrowser _reportView = new() { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };

    private XmlDiffExecutionResult? _lastResult;

    public MainForm()
    {
        Text = "xmldiff UI";
        Width = 1400;
        Height = 900;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        _reportFormatComboBox.Items.AddRange([HtmlReportOption, TextReportOption]);
        _reportFormatComboBox.SelectedIndex = 0;
        _reportFormatComboBox.SelectedIndexChanged += (_, _) => RefreshPreview();
        _compareButton.Click += async (_, _) => await CompareAsync();
        _saveReportButton.Click += (_, _) => SaveCurrentReport();

        DragEnter += HandleFileDragEnter;
        DragDrop += HandleFormDrop;

        ConfigureDropTarget(_leftPathTextBox, filePath => _leftPathTextBox.Text = filePath);
        ConfigureDropTarget(_rightPathTextBox, filePath => _rightPathTextBox.Text = filePath);

        Controls.Add(CreateLayout());
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        NavigatePreview(ReportPreviewBuilder.CreateEmptyState("Drop one or two XML files onto the window, adjust the xmldiff options, and run a comparison."));
        LoadConfiguredDefaults();
    }

    private Control CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateConfigurationPanel(), 0, 0);
        root.Controls.Add(_reportView, 0, 1);
        root.Controls.Add(_statusLabel, 0, 2);
        return root;
    }

    private Control CreateConfigurationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 6,
            Padding = new Padding(0, 0, 0, 12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        AddPathRow(panel, 0, "Left XML", _leftPathTextBox, () => BrowseForInputFile(_leftPathTextBox));
        AddPathRow(panel, 1, "Right XML", _rightPathTextBox, () => BrowseForInputFile(_rightPathTextBox));
        AddPathRow(panel, 2, "Output path", _outputPathTextBox, BrowseForOutputFile);

        var optionsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 6, 3, 3)
        };
        optionsPanel.Controls.Add(_formatXmlCheckBox);
        optionsPanel.Controls.Add(_formatJsonCheckBox);
        optionsPanel.Controls.Add(_sortTagsCheckBox);
        optionsPanel.Controls.Add(new Label { Text = "Report format", AutoSize = true, Margin = new Padding(18, 6, 6, 0) });
        optionsPanel.Controls.Add(_reportFormatComboBox);
        panel.Controls.Add(optionsPanel, 1, 3);
        panel.SetColumnSpan(optionsPanel, 2);

        var sortRulesLabel = new Label
        {
            Text = "Sort rules",
            AutoSize = true,
            Margin = new Padding(0, 9, 12, 0)
        };
        panel.Controls.Add(sortRulesLabel, 0, 4);
        panel.Controls.Add(_sortRulesTextBox, 1, 4);
        panel.SetColumnSpan(_sortRulesTextBox, 2);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 8, 3, 0)
        };
        buttonPanel.Controls.Add(_compareButton);
        buttonPanel.Controls.Add(_saveReportButton);
        buttonPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(16, 8, 0, 0),
            Text = "Drop files on the window to fill left/right paths. One sort rule per line."
        });
        panel.Controls.Add(buttonPanel, 1, 5);
        panel.SetColumnSpan(buttonPanel, 2);

        return panel;
    }

    private static void AddPathRow(TableLayoutPanel panel, int rowIndex, string labelText, TextBox textBox, Action browseAction)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 9, 12, 0) }, 0, rowIndex);
        panel.Controls.Add(textBox, 1, rowIndex);
        panel.Controls.Add(new Button { Text = "Browse...", AutoSize = true, Margin = new Padding(8, 3, 0, 3) }, 2, rowIndex);
        ((Button)panel.GetControlFromPosition(2, rowIndex)!).Click += (_, _) => browseAction();
    }

    private async Task CompareAsync()
    {
        try
        {
            SetBusyState(true, "Comparing XML files...");
            var request = CreateRequest();
            _lastResult = await Task.Run(() => _diffService.Generate(request, string.Empty));
            _saveReportButton.Enabled = true;
            RefreshPreview();

            if (!string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
            {
                SaveCurrentReport(_outputPathTextBox.Text);
                _statusLabel.Text = $"Comparison complete. Report saved to '{_outputPathTextBox.Text}'.";
                return;
            }

            _statusLabel.Text = "Comparison complete.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.Xml.XmlException)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusyState(false, _statusLabel.Text);
        }
    }

    private XmlDiffRequest CreateRequest()
    {
        var sortRules = _sortRulesTextBox.Lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        return new XmlDiffRequest(
            _leftPathTextBox.Text.Trim(),
            _rightPathTextBox.Text.Trim(),
            sortRules,
            _formatXmlCheckBox.Checked,
            _formatJsonCheckBox.Checked,
            _sortTagsCheckBox.Checked);
    }

    private void LoadConfiguredDefaults()
    {
        try
        {
            var defaults = _diffService.LoadDefaults();
            _sortRulesTextBox.Lines = defaults.SortRules.ToArray();
            _formatXmlCheckBox.Checked = defaults.FormatXml;
            _formatJsonCheckBox.Checked = defaults.FormatJson;
            _sortTagsCheckBox.Checked = defaults.SortByTagName;
            _statusLabel.Text = defaults == XmlDiffRequest.Empty
                ? "No shared xmldiff defaults were found."
                : "Loaded shared xmldiff defaults from the xmlssort configuration file.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _statusLabel.Text = $"Unable to load defaults: {ex.Message}";
        }
    }

    private void RefreshPreview()
    {
        if (_lastResult is null)
        {
            return;
        }

        var preview = SelectedReportFormat == HtmlReportOption
            ? _lastResult.HtmlReport
            : ReportPreviewBuilder.CreateTextPreview("xmldiff report", _lastResult.TextReport);
        NavigatePreview(preview);
    }

    private string SelectedReportFormat => _reportFormatComboBox.SelectedItem as string ?? HtmlReportOption;

    private void SaveCurrentReport()
    {
        if (_lastResult is null)
        {
            ShowError("Run a comparison before saving a report.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
        {
            BrowseForOutputFile();
        }

        if (string.IsNullOrWhiteSpace(_outputPathTextBox.Text))
        {
            return;
        }

        SaveCurrentReport(_outputPathTextBox.Text);
        _statusLabel.Text = $"Report saved to '{_outputPathTextBox.Text}'.";
    }

    private void SaveCurrentReport(string outputPath)
    {
        if (_lastResult is null)
        {
            return;
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);

        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, SelectedReportFormat == HtmlReportOption ? _lastResult.HtmlReport : _lastResult.TextReport);
    }

    private void BrowseForInputFile(TextBox target)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void BrowseForOutputFile()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "HTML reports (*.html)|*.html|Text reports (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = SelectedReportFormat == HtmlReportOption ? "html" : "txt",
            AddExtension = true,
            FileName = SelectedReportFormat == HtmlReportOption ? "xmldiff-report.html" : "xmldiff-report.txt"
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputPathTextBox.Text = dialog.FileName;
        }
    }

    private void SetBusyState(bool isBusy, string statusText)
    {
        UseWaitCursor = isBusy;
        _compareButton.Enabled = !isBusy;
        _saveReportButton.Enabled = !isBusy && _lastResult is not null;
        _statusLabel.Text = statusText;
    }

    private void ShowError(string message)
    {
        _statusLabel.Text = message;
        MessageBox.Show(this, message, "xmldiff UI", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void NavigatePreview(string html)
    {
        _reportView.DocumentText = html;
    }

    private static TextBox CreatePathTextBox()
    {
        return new TextBox
        {
            Dock = DockStyle.Fill
        };
    }

    private static void ConfigureDropTarget(Control control, Action<string> onFileDropped)
    {
        control.AllowDrop = true;
        control.DragEnter += HandleFileDragEnter;
        control.DragDrop += (_, e) =>
        {
            var files = GetDroppedFiles(e.Data);

            if (files.Length > 0)
            {
                onFileDropped(files[0]);
            }
        };
    }

    private void HandleFormDrop(object? sender, DragEventArgs e)
    {
        var files = GetDroppedFiles(e.Data);

        if (files.Length == 0)
        {
            return;
        }

        if (files.Length == 1)
        {
            if (string.IsNullOrWhiteSpace(_leftPathTextBox.Text))
            {
                _leftPathTextBox.Text = files[0];
                return;
            }

            _rightPathTextBox.Text = files[0];
            return;
        }

        _leftPathTextBox.Text = files[0];
        _rightPathTextBox.Text = files[1];
    }

    private static void HandleFileDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = GetDroppedFiles(e.Data).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private static string[] GetDroppedFiles(IDataObject? dataObject)
    {
        return dataObject?.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(File.Exists).ToArray()
            : [];
    }
}
