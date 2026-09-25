using System.Diagnostics;
using System.Text;

namespace GammaControl;

internal sealed record ProcessChoice(int Id, string Name, string Title, string ExecutablePath)
{
    public override string ToString() => $"{Name} (PID {Id})  {Title}";
}

internal static class ProcessPicker
{
    internal static IReadOnlyList<ProcessChoice> GetRunningWindows()
    {
        var choices = new List<ProcessChoice>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    {
                        continue;
                    }

                    var handle = NativeMethods.OpenProcess(
                        NativeMethods.ProcessQueryLimitedInformation, false, (uint)process.Id);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        var path = new StringBuilder(32768);
                        uint length = (uint)path.Capacity;
                        if (NativeMethods.QueryFullProcessImageName(handle, 0, path, ref length))
                        {
                            choices.Add(new ProcessChoice(process.Id, process.ProcessName,
                                process.MainWindowTitle, path.ToString()));
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(handle);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception or System.Security.SecurityException)
                {
                    // A process can exit while the list is being built.
                }
            }
        }

        return choices.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    internal static ProcessChoice? ShowDialog(IWin32Window owner)
    {
        using var dialog = new Form
        {
            Text = UiText.Get(TextId.ChooseProcessTitle),
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(680, 490),
            MinimumSize = new Size(500, 350),
            Font = new Font("Segoe UI", 10F),
            AutoScaleMode = AutoScaleMode.Dpi
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var help = new Label { Text = UiText.Get(TextId.ProcessPickerHelp), AutoSize = true,
            Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false,
            HorizontalScrollbar = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0, 10, 0, 0) };
        var select = new Button { Text = UiText.Get(TextId.ProcessSelect), AutoSize = true,
            DialogResult = DialogResult.OK, Enabled = false };
        var cancel = new Button { Text = UiText.Get(TextId.ProcessCancel), AutoSize = true,
            DialogResult = DialogResult.Cancel };
        var refresh = new Button { Text = UiText.Get(TextId.ProcessRefresh), AutoSize = true };
        void Populate()
        {
            list.BeginUpdate();
            list.Items.Clear();
            foreach (var choice in GetRunningWindows()) list.Items.Add(choice);
            list.EndUpdate();
            select.Enabled = false;
            help.Text = list.Items.Count == 0 ? UiText.Get(TextId.NoProcessesFound)
                : UiText.Get(TextId.ProcessPickerHelp);
        }
        list.SelectedIndexChanged += (_, _) => select.Enabled = list.SelectedItem is ProcessChoice;
        list.DoubleClick += (_, _) => { if (select.Enabled) dialog.DialogResult = DialogResult.OK; };
        refresh.Click += (_, _) => Populate();
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(select);
        buttons.Controls.Add(refresh);
        layout.Controls.Add(help, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = select;
        dialog.CancelButton = cancel;
        Populate();
        return dialog.ShowDialog(owner) == DialogResult.OK ? list.SelectedItem as ProcessChoice : null;
    }
}
