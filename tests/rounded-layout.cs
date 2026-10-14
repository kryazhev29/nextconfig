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
    [STAThread] static int Main(string[] args) {
        try {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var poller = (clOpros)FormatterServices.GetUninitializedObject(typeof(clOpros));
            using (var form = new Form2(poller, "offline", "000000000000000", 0)) {
                form.Load -= (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, typeof(Form2).GetMethod("Form2_Load", Flags));
                typeof(Form2).GetField("bootOffered", Flags).SetValue(form, true);
                typeof(Form2).GetField("linkLoading", Flags).SetValue(form, true);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
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
                }
                Check(form.AutoScaleMode == AutoScaleMode.Dpi, "native DPI layout enabled");
                File.WriteAllText(Path.Combine(args[0], "dpi.txt"),
                    "DeviceDpi=" + form.DeviceDpi + "; Px100=" + Theme.Px(100) +
                    "; AutoScaleDimensions=" + form.AutoScaleDimensions + "; Font=" + form.Font.Size);
                form.Width = Theme.ControlPx(form, 1280);
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
                    using (var image = new Bitmap(dialog.Width, dialog.Height)) {
                        dialog.DrawToBitmap(image, new Rectangle(Point.Empty, dialog.Size));
                        image.Save(Path.Combine(args[0], "dialog.png"));
                    }
                    dialog.Close();
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
