using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfSimpleInjector
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            cb32.IsChecked = Properties.Settings.Default.is32;
            cb64.IsChecked = Properties.Settings.Default.is64;
            cbov.IsChecked = Properties.Settings.Default.hidden;
            tbFilter.Text = Properties.Settings.Default.pName;
            string name = Properties.Settings.Default.dllName;
            name = name.Remove(0, name.LastIndexOf('\\') + 1);
            selectDllBtn.Content = name;
        }

        public static parsedProc SelectedProc;

        #region resize
        public class NativeMethods
        {
            public const int WM_NCHITTEST = 0x84;
            public const int HTCAPTION = 2;
            public const int HTLEFT = 10;
            public const int HTRIGHT = 11;
            public const int HTTOP = 12;
            public const int HTTOPLEFT = 13;
            public const int HTTOPRIGHT = 14;
            public const int HTBOTTOM = 15;
            public const int HTBOTTOMLEFT = 16;
            public const int HTBOTTOMRIGHT = 17;
        }
        public class MaskValues
        {
            public const int NONE = 1 << 0;
            public const int HITTOP = 1 << 1;
            public const int HITBOTTOM = 1 << 2;
            public const int HITLEFT = 1 << 3;
            public const int HITRIGHT = 1 << 4;
        }
        private static int GripSize = 15;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            HwndSource windowSource = HwndSource.FromHwnd(windowHandle);
            windowSource.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg,
            IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_NCHITTEST)
            {
                int x = lParam.ToInt32() << 16 >> 16, y = lParam.ToInt32() >> 16;
                Point pos = PointFromScreen(new Point(x, y));

                int bitMask = 0;

                if (pos.Y < GripSize)
                    bitMask |= MaskValues.HITTOP;

                bitMask |= (pos.Y < GripSize) ? MaskValues.HITTOP : MaskValues.NONE;
                bitMask |= (pos.Y > ActualHeight - GripSize) ? MaskValues.HITBOTTOM : MaskValues.NONE;
                bitMask |= (pos.X < GripSize) ? MaskValues.HITLEFT : MaskValues.NONE;
                bitMask |= (pos.X > ActualWidth - GripSize) ? MaskValues.HITRIGHT : MaskValues.NONE;

                if (bitMask == 0) return IntPtr.Zero;
                handled = true;
                if ((bitMask & MaskValues.HITTOP) != 0)
                {
                    if ((bitMask & MaskValues.HITRIGHT) != 0)
                        return (IntPtr)NativeMethods.HTTOPRIGHT;
                    if ((bitMask & MaskValues.HITLEFT) != 0)
                        return (IntPtr)NativeMethods.HTTOPLEFT;
                    return (IntPtr)NativeMethods.HTTOP;
                }
                if ((bitMask & MaskValues.HITBOTTOM) != 0)
                {
                    if ((bitMask & MaskValues.HITRIGHT) != 0)
                        return (IntPtr)NativeMethods.HTBOTTOMRIGHT;
                    if ((bitMask & MaskValues.HITLEFT) != 0)
                        return (IntPtr)NativeMethods.HTBOTTOMLEFT;
                    return (IntPtr)NativeMethods.HTBOTTOM;
                }
                if ((bitMask & MaskValues.HITRIGHT) != 0)
                    return (IntPtr)NativeMethods.HTRIGHT;
                if ((bitMask & MaskValues.HITLEFT) != 0)
                    return (IntPtr)NativeMethods.HTLEFT;
                handled = false;
            }
            return IntPtr.Zero;
        }
        #endregion
        #region top panel
        private bool mouseDown;
        private Point startMouseRelativePos;
        private void topPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            mouseDown = true;
            startMouseRelativePos = e.GetPosition(this);
        }

        private void topPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                mouseDown = false;

            if (!mouseDown) return;
            Point GetMousePos() => this.PointToScreen(Mouse.GetPosition(this));
            this.Left = GetMousePos().X - startMouseRelativePos.X;
            this.Top = GetMousePos().Y - startMouseRelativePos.Y;
            this.UpdateLayout();

        }

        private void topPanel_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            mouseDown = false;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(228);
        }
        #endregion

        private void createProcBtns()
        {
            if (stPanel == null) return;

            Properties.Settings.Default.is64 = cb64.IsChecked == true;
            Properties.Settings.Default.is32 = cb32.IsChecked == true;
            Properties.Settings.Default.hidden = cbov.IsChecked == true;
            Properties.Settings.Default.pName = tbFilter.Text;
            Properties.Settings.Default.Save();

            stPanel.Children.Clear();
            List<Process> filteredList = new List<Process>(Process.GetProcesses().OrderBy(p => p.ProcessName)).Where(x =>
            (x.MainWindowTitle.ToLower().Contains(tbFilter.Text.ToLower()) ||
            x.ProcessName.ToLower().Contains(tbFilter.Text.ToLower())) &&
            parsedProc.getAppBit(parsedProc.GetProcessFilename(x)) != 0 &&
            (parsedProc.getAppBit(parsedProc.GetProcessFilename(x)) == 32 && cb32.IsChecked == true ||
            parsedProc.getAppBit(parsedProc.GetProcessFilename(x)) == 64 && cb64.IsChecked == true) &&
            (x.MainWindowTitle.Length > 0 || cbov.IsChecked == true)
            ).ToList();

            foreach (Process process in filteredList)
            {
                RadioButton rb = new RadioButton();
                string procFileName = parsedProc.GetProcessFilename(process);
                int bit = parsedProc.getAppBit(procFileName);

                rb.Content = $"x{bit} pid{ process.Id}\t{process.MainWindowTitle} ({process.ProcessName})"; //  - 0x{proc.process.MainModule.BaseAddress.ToString("x8")}
                if (process.MainWindowTitle.Length < 1)
                    rb.Foreground = new SolidColorBrush(Color.FromArgb(100, 130, 130, 130));

                Image image = new Image();
                image.Source = parsedProc.ToImageSource(System.Drawing.Icon.ExtractAssociatedIcon(procFileName));
                image.Stretch = Stretch.Uniform;
                image.Width = rb.Height;
                image.Height = rb.Height;
                image.Margin = new Thickness(5, 0, 0, 0);
                image.HorizontalAlignment = HorizontalAlignment.Left;

                stPanel.Children.Add(image);

                rb.Margin = new Thickness(0, -rb.Height, 0, 2);
                
                rb.Checked += Rb_Checked;
                stPanel.Children.Add(rb);
            }
            return;
        }

        private void Rb_Checked(object sender, RoutedEventArgs e)
        {
            string name = (sender as RadioButton).Content.ToString();
            name = name.Remove(0, name.LastIndexOf('(') + 1);
            name = name.Remove(name.LastIndexOf(')'));
            Process[] proc = Process.GetProcessesByName(name);
            if (proc.Length > 0)
                SelectedProc = new parsedProc(proc[0]);
        }

        private void buttonProcess_Click(object sender, RoutedEventArgs e)
        {
            createProcBtns();
        }

        private void tbFilter_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                createProcBtns();
        }

        private void selectDllBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Dll (*.dll)|*.dll";
            if (openFileDialog.ShowDialog() == false) return;
            string anme = openFileDialog.FileName;
            Properties.Settings.Default.dllName = anme;
            Properties.Settings.Default.Save();
            anme = anme.Remove(0, anme.LastIndexOf('\\') + 1);
            selectDllBtn.Content = anme;
        }

        private void btnInject_Click(object sender, RoutedEventArgs e)
        {
            string dir = Directory.GetCurrentDirectory();
            string injectorPath;
            if (SelectedProc.bit == 32)
                injectorPath = $"{dir}\\Arg Jnjector x86.exe";
            else if (SelectedProc.bit == 64)
                injectorPath = $"{dir}\\Arg Jnjector x86.exe";
            else return;
            string dllPath = Properties.Settings.Default.dllName;
            string procName = $"{SelectedProc.name}.exe";
            string[] exitCode = new string[] {
                "Last injection: Module not found",
                "Last injection: Successful",
                "Last injection: Dll not found",
                "Last injection: Process not found"
            };
            if (!File.Exists(injectorPath))
            {
                //textboxTitle.Text = exitCode[0];
                return;
            }
            if (!File.Exists(dllPath))
            {
                //textboxTitle.Text = exitCode[2];
                return;
            }

            Process proc = new Process();
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.FileName = injectorPath;
            proc.StartInfo.Arguments = string.Format(@"""{0}"" ""{1}""", dllPath, procName);
            proc.Start();
            proc.WaitForExit();
            //textboxTitle.Text = exitCode[proc.ExitCode];

            if (proc.ExitCode == 1) return;

            //IntPtr csgohWnd = IntPtr.Zero;
            //foreach (Process pList in Process.GetProcesses())
            //{
            //    if (pList.MainWindowTitle.Contains("csgo.exe"))
            //    {
            //        csgohWnd = pList.MainWindowHandle;
            //    }
            //}
            //if (csgohWnd != null && csgohWnd != IntPtr.Zero)
            //{
            //    AutomationElement element = AutomationElement.FromHandle(csgohWnd);
            //    if (element != null)
            //        element.SetFocus();
            //}
        }
    }
    public enum BinaryType : uint
    {
        SCS_32BIT_BINARY = 0,   // A 32-bit Windows-based application
        SCS_DOS_BINARY = 1,     // An MS-DOS � based application
        SCS_WOW_BINARY = 2,     // A 16-bit Windows-based application
        SCS_PIF_BINARY = 3,     // A PIF file that executes an MS-DOS � based application
        SCS_POSIX_BINARY = 4,   // A POSIX � based application
        SCS_OS216_BINARY = 5,   // A 16-bit OS/2-based application
        SCS_64BIT_BINARY = 6,   // A 64-bit Windows-based application.
    }
    public class parsedProc
    {
        public Process process;
        public string windowTitle;
        public string name;
        public int bit = 0;
        public bool visible = false;
        public System.Drawing.Icon ico = null;
        public ImageSource imageSrc = null;

        [DllImport("kernel32.dll")]
        static extern bool GetBinaryType(string lpApplicationName, out BinaryType lpBinaryType);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);
        public parsedProc(Process prc)
        {
            process = prc;
            windowTitle = prc.MainWindowTitle;
            name = prc.ProcessName;
            string procFileName = GetProcessFilename(process);

            bit = getAppBit(procFileName);
            if (bit == 0) return;
            visible = prc.MainWindowTitle.Length > 0;
            ico = System.Drawing.Icon.ExtractAssociatedIcon(procFileName);
            imageSrc = ToImageSource(ico);

        }
        public static int getAppBit(string procName)
        {
            int appBit = 0;
            if (procName == null || procName == string.Empty) return appBit;
            BinaryType bt;
            GetBinaryType(procName, out bt);
            if (bt == BinaryType.SCS_32BIT_BINARY)
                appBit = 32;
            else if (bt == BinaryType.SCS_64BIT_BINARY)
                appBit = 64;

            return appBit;
        }
        public static ImageSource ToImageSource(System.Drawing.Icon icon)
        {
            ImageSource imageSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        [Flags]
        private enum ProcessAccessFlags : uint
        {
            QueryLimitedInformation = 0x00001000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryFullProcessImageName(
              [In] IntPtr hProcess,
              [In] int dwFlags,
              [Out] StringBuilder lpExeName,
              ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
         ProcessAccessFlags processAccess,
         bool bInheritHandle,
         int processId);

        public static String GetProcessFilename(Process p)
        {
            int capacity = 2000;
            StringBuilder builder = new StringBuilder(capacity);
            IntPtr ptr = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, p.Id);
            if (!QueryFullProcessImageName(ptr, 0, builder, ref capacity))
            {
                return String.Empty;
            }

            return builder.ToString();
        }
    }
}
