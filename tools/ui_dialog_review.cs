// Доработка: Кряжев А., 2026. Просмотр настоящих форм без создания COM-порта.
using System;
using System.Collections;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using NextConfig;

internal static class UiReview {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args) {
        try { Run(args); }
        catch(Exception ex) { System.IO.File.WriteAllText(System.IO.Path.Combine(Application.StartupPath,"ui-review-error.txt"),ex.ToString()); }
    }
    static void Run(string[] args) {
        if (Array.IndexOf(args, "--demo") < 0) throw new InvalidOperationException("Только --demo");
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Form form;
        switch (args[0]) {
            case "pin": case "pin-error":
                form = new frmPIN(BitConverter.ToInt32(System.Text.Encoding.UTF8.GetBytes("4321"), 0));
                if (args[0] == "pin-error") form.Shown += (s,e) => ((Button)Field(form,"Button1")).PerformClick();
                break;
            case "text":
                form = new frmText("Имя контроллера", "Наименование", "Контроллер у главного входа"); break;
            case "select":
                var items = new ArrayList();
                items.Add(new frmSelecter.mItem { ID=1, Name="UG95/UG915" });
                items.Add(new frmSelecter.mItem { ID=2, Name="SLM336" });
                items.Add(new frmSelecter.mItem { ID=3, Name="M66" });
                form = new frmSelecter("Тип модуля связи", items, 2, 14, false); break;
            case "changes":
                form = new frmInfo("Сохранение настроек", "", ContentAlignment.MiddleLeft, true);
                Theme.ConfigChanges((frmInfo)form, "В контроллере изменится:\r\n\r\n• Скорость 485: 115200 → 57600\r\n• Устройство 2: Нет → RFID тип 1\r\n• Адрес устройства 2: 0 → 7");
                ((Button)Field(form,"Button1")).Text = "Сохранить";
                break;
            case "long":
                form = new frmInfo("Сохранение настроек", "", ContentAlignment.MiddleLeft, true);
                string changes="В контроллере изменится:\r\n";
                for(int i=1;i<=8;i++) changes += "\r\n• Устройство " + i + " адрес: 0 → " + i + "\r\n• Устройство " + i + " тип: Нет → RFID тип 1";
                Theme.ConfigChanges((frmInfo)form, changes); break;
            case "confirm":
                form = new frmInfo("Очистка памяти", "Все идентификаторы будут удалены из контроллера. Восстановить их можно повторной загрузкой с сервера.", ContentAlignment.MiddleLeft, true);
                ((Button)Field(form,"Button1")).Text="Очистить память";
                Theme.DangerSolid((Button)Field(form,"Button1")); break;
            case "error":
                form = new frmInfo("Нет ответа от контроллера", "Не удалось считать настройки. Проверьте USB-подключение и повторите попытку."); break;
            case "fw": case "fw-error": case "fw-progress":
                form = new frmFW(args[0] == "fw-error" ? "missing.bin" : args[1], (clOpros)null);
                form.Shown += (s,e) => {
                    ((Button)Field(form,"Button1")).Enabled=false;
                    ((Button)Field(form,"btnCheck")).Enabled=false;
                    if(args[0] == "fw-progress") {
                        ((Label)Field(form,"lblState")).Text="Передача данных · демонстрация";
                        ((Label)Field(form,"Label1")).Text="36 804 из 73 608 байт · 50 %";
                        ((Meter)Field(form,"ProgressBar1")).Value=50;
                    }
                }; break;
            default: form = new frmInfo("Настройки сохранены", "Новые параметры переданы контроллеру."); break;
        }
        form.StartPosition = FormStartPosition.CenterScreen;
        Application.Run(form);
    }
    static object Field(Form form,string name) {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var property=form.GetType().GetProperty(name,flags);
        return property != null ? property.GetValue(form,null) : form.GetType().GetField(name,flags).GetValue(form);
    }
}
