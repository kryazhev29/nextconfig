// Offline UI contract: never construct clOpros or open a COM port.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows.Forms;
using NextConfig;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]

internal static class RoundedLayout {
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static void Check(bool ok, string message) {
        if (!ok) throw new Exception(message);
        checks++;
    }
    static Control Field(Form form, string name) {
        var property = form.GetType().GetProperty(name, Flags);
        return (Control)(property != null ? property.GetValue(form, null) : form.GetType().GetField(name, Flags).GetValue(form));
    }
    static System.Collections.Generic.IEnumerable<Control> Descendants(Control root) {
        foreach (Control child in root.Controls) {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
    static void Snapshot(Control control, string path) {
        using (var image = new Bitmap(control.Width, control.Height)) {
            control.DrawToBitmap(image, new Rectangle(Point.Empty, control.Size));
            image.Save(path);
        }
    }
    [STAThread] static int Main(string[] args) {
        try {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Check(Theme.FontBody.FontFamily.Name == "Montserrat", "embedded regular font loaded");
            Check(Theme.FontBodyBold.FontFamily.Name == "Montserrat SemiBold", "embedded semibold font loaded");
            var poller = (clOpros)FormatterServices.GetUninitializedObject(typeof(clOpros));
            using (var form = new Form2(poller, "offline", "000000000000000", 0)) {
                form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, typeof(Form2).GetMethod("Form2_Load", Flags));
                typeof(Form2).GetField("bootOffered", Flags).SetValue(form, true);
                typeof(Form2).GetField("linkLoading", Flags).SetValue(form, true);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                Field(form, "lblIMEI").Text = "867442070217472";
                Field(form, "lblHeadModel").Text = "Модуль SLM336";
                Field(form, "lblHeadHW").Text = "Плата 139";
                Field(form, "lblHeadFW").Text = "Прошивка 71";
                Field(form, "lblHeadBoot").Text = "Загрузчик 0x32";
                Field(form, "lblPortInfo").Text = "COM5";
                Theme.DotText((Label)Field(form, "lblConnState"), Theme.eDot.Good, "На связи");
                Theme.Dot((Label)Field(form, "dotConn"), Theme.eDot.Good);
                var home = Field(form, "tabState");
                var service = Field(form, "tabObject");
                foreach (var name in new[] { "Label20", "btnTranspMode", "btnFactory" }) {
                    var action = Field(form, name);
                    Check(home.Contains(action), name + " on home");
                    Check(!service.Contains(action), name + " absent from service");
                }
                var overview = (TableLayoutPanel)home.Controls[0];
                var grid = (TableLayoutPanel)overview.Controls[1];
                Check(grid.Controls.Count == 5, "five separate cards including camera");
                form.MinimumSize = Size.Empty;
                foreach (var width in new[] { 1280, 900 }) {
                    form.Width = Theme.ControlPx(form, width);
                    Application.DoEvents();
                    Check(grid.ColumnCount == (width == 1280 ? 2 : 1), "responsive columns at " + width);
                    var cards = grid.Controls.Cast<Control>().ToArray();
                    for (int i = 0; i < cards.Length; i++)
                        for (int j = i + 1; j < cards.Length; j++)
                            Check(!cards[i].Bounds.IntersectsWith(cards[j].Bounds), "cards must not overlap");
                    var row = Field(form, "serviceRow");
                    foreach (Control action in row.Controls)
                        Check(row.ClientRectangle.Contains(action.Bounds), "maintenance fits at " + width);
                    using (var g = form.CreateGraphics()) {
                        foreach (Control action in row.Controls)
                            Check(action.Width >= TextRenderer.MeasureText(g, action.Text, action.Font).Width + action.Padding.Horizontal,
                                "maintenance caption not clipped at " + width);
                    }
                    var header = Field(form, "pnlHead");
                    var headerRow = Field(form, "headRow");
                    var identity = Field(form, "headDevice");
                    var state = Field(form, "headState");
                    Check(!identity.Bounds.IntersectsWith(state.Bounds), "header blocks do not overlap at " + width);
                    Check(headerRow.ClientRectangle.Contains(state.Bounds), "status stays inside header at " + width);
                    foreach (Control chip in Field(form, "headVersions").Controls)
                        Check(chip.Parent.ClientRectangle.Contains(chip.Bounds), "metadata fits at " + width);
                    using (var image = new Bitmap(header.Width, header.Height)) {
                        header.DrawToBitmap(image, new Rectangle(Point.Empty, header.Size));
                        image.Save(Path.Combine(args[0], "header-" + width + ".png"));
                    }
                }
                Check(form.AutoScaleMode == AutoScaleMode.Dpi, "native DPI layout enabled");
                File.WriteAllText(Path.Combine(args[0], "dpi.txt"),
                    "DeviceDpi=" + form.DeviceDpi + "; Px100=" + Theme.Px(100) +
                    "; AutoScaleDimensions=" + form.AutoScaleDimensions + "; Font=" + form.Font.Size);
                File.WriteAllLines(Path.Combine(args[0], "header-layout.txt"),
                    new[] { "pnlHead", "headRow", "headDevice", "headVersions", "headState" }
                        .Select(name => name + ": " + Field(form, name).Bounds + "; preferred=" + Field(form, name).PreferredSize).ToArray());
                form.Width = Theme.ControlPx(form, 1280);
                Application.DoEvents();
                Snapshot(form, Path.Combine(args[0], "home.png"));
                var tabs = (TabControl)Field(form, "tabs");
                tabs.SelectedTab = (TabPage)Field(form, "tabSettings");
                Application.DoEvents();
                foreach (var combo in Descendants(tabs.SelectedTab).OfType<ComboBox>().Where(c => c.DrawMode == DrawMode.OwnerDrawFixed))
                    Check(combo.ItemHeight >= TextRenderer.MeasureText("Аg", combo.Font).Height, "periphery row fits font");
                Snapshot(form, Path.Combine(args[0], "settings.png"));
                tabs.SelectedTab = (TabPage)home;
                Application.DoEvents();
                overview.ScrollControlIntoView(Field(form, "serviceRow").Parent);
                Application.DoEvents();
                using (var image = new Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
                    image.Save(Path.Combine(args[0], "maintenance.png"));
                }
                using (var dialog = new frmInfo("Обслуживание", "Перезагрузить контроллер?", ContentAlignment.MiddleLeft, true)) {
                    dialog.Show();
                    Application.DoEvents();
                    Check(!Descendants(dialog).OfType<Panel>().Any(p => p.VerticalScroll.Visible), "short dialog has no scrollbar");
                    using (var image = new Bitmap(dialog.Width, dialog.Height)) {
                        dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                        image.Save(Path.Combine(args[0], "dialog.png"));
                    }
                    dialog.AcceptButton.PerformClick();
                    Check(dialog.DialogResult == DialogResult.OK, "primary dialog action works");
                }
                using (var dialog = new frmInfo("Изменения настроек", string.Join("\r\n", Enumerable.Repeat("Параметр контроллера: прежнее значение → новое значение", 70)), ContentAlignment.MiddleLeft, true)) {
                    dialog.Show();
                    Application.DoEvents();
                    Check(Screen.FromControl(dialog).WorkingArea.Contains(dialog.Bounds), "long dialog fits screen");
                    Check(Descendants(dialog).OfType<Panel>().Any(p => p.VerticalScroll.Visible), "long dialog can scroll");
                    foreach (var button in Descendants(dialog).OfType<Button>()) {
                        var bounds = dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle));
                        Check(button.Visible && dialog.ClientRectangle.Contains(bounds), "long dialog actions visible");
                    }
                    Snapshot(dialog, Path.Combine(args[0], "dialog-long.png"));
                    dialog.CancelButton.PerformClick();
                    Check(dialog.DialogResult == DialogResult.Cancel, "dialog cancel works");
                }
            }
            File.WriteAllText(Path.Combine(args[0], "result.txt"), "PASS: " + checks + " layout checks; no COM");
            return 0;
        } catch (Exception error) {
            File.WriteAllText(Path.Combine(args[0], "result.txt"), error.ToString());
            return 1;
        }
    }
}
