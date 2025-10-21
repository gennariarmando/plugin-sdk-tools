using Ookii.Dialogs.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PluginSdkWizardInstaller
{
    public partial class SdkPathControl : UserControl
    {
        public SdkComponent Data { get; set; }
        public event EventHandler DataChanged;

        public enum StatusEnum { None, Ok, Warning, Error };
        public StatusEnum Status { get; private set; }

        private static double NameColumnWidth = 100.0;
        private static double VariableColumnWidth = 50.0;

        private bool initialized = false;
        private string readPath;

        public SdkPathControl()
        {
            InitializeComponent();
        }

        public void Update()
        {
            if (Data == null) return;

            if (UpdateInProgress) return;
            UpdateInProgress = true;

            nameLbl.Content = Data.Name?.Replace("_", "__");
            varNameLbl.Content = Data.EnvVarName?.Replace("_", "__");

            readPath = Data.ReadEnvPath();
            if (!initialized) pathTbx.Text = readPath;

            infoBtn.Visibility = String.IsNullOrEmpty(Data.Info) ? Visibility.Collapsed : Visibility.Visible;

            initialized = true;

            if (readPath != NormalizePath(pathTbx.Text))
            {
                SetStatus(StatusEnum.Error, "notSet", "Has pending changes");
                setBtn.IsEnabled = true;

                UpdateInProgress = false;
                return;
            }
            setBtn.IsEnabled = false;

            if (String.IsNullOrEmpty(pathTbx.Text))
            {
                if (Data.Mandatory)
                    SetStatus(StatusEnum.Error, "error", "Path not specified");
                else
                    SetStatus(StatusEnum.None, "nothing", null);

                UpdateInProgress = false;
                return;
            }

            if (!Directory.Exists(readPath))
            {
                if (Data.Mandatory)
                    SetStatus(StatusEnum.Error, "error", "Specified directory does not exists");
                else
                    SetStatus(StatusEnum.Warning, "warning", "Specified directory does not exists");

                UpdateInProgress = false;
                return;
            }

            if (!String.IsNullOrEmpty(Data.CheckFile) &&
                !File.Exists(Path.Combine(readPath, Data.CheckFile)))
            {
                if (Data.Mandatory)
                    SetStatus(StatusEnum.Error, "error", "Specified directory does not contain expected files");
                else
                    SetStatus(StatusEnum.Warning, "warning", "Specified directory does not contain expected files");

                UpdateInProgress = false;
                return;
            }

            if (Data.EnvVarName == "PLUGIN_SDK_DIR" && String.Compare(
                Path.GetFullPath(readPath).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(System.Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.InvariantCultureIgnoreCase) != 0)
            {
                SetStatus(StatusEnum.Ok, "warning", "Path does not match location of wizard app\nMultiple Plugin-SDKs installed?"); // non critical warning

                UpdateInProgress = false;
                return;
            }

            SetStatus(StatusEnum.Ok, "ok", null);
            UpdateInProgress = false;
        }
        private bool UpdateInProgress = false;

        public struct StatusInfo { public StatusEnum status; public BitmapImage icon; public string description; };
        public StatusInfo GetStatus()
        {
            var s = new StatusInfo
            {
                status = Status,
                icon = statusImg.Source as BitmapImage,
                description = ToolTip as string
            };
            return s;
        }

        public void SetPath(string path)
        {
            pathTbx.Text = path;
            ApplyChanges();
        }

        public void UpdateColumnsWidth()
        {
            var margin = new Thickness();
            margin.Left = 10.0; // margin and padding
            margin.Right = 10.0; // margin and padding

            var lbl = new Label();
            lbl.Margin = margin;

            // name column
            lbl.Content = Data.Name?.Replace("_", "__");
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            NameColumnWidth = Math.Max(NameColumnWidth, lbl.DesiredSize.Width);
            nameClmn.Width = new GridLength(NameColumnWidth);

            // name column
            lbl.Content = Data.EnvVarName?.Replace("_", "__");
            lbl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            VariableColumnWidth = Math.Max(VariableColumnWidth, lbl.DesiredSize.Width);
            varNameClmn.Width = new GridLength(VariableColumnWidth);
        }

        private void ApplyChanges()
        {
            pathTbx.Text = NormalizePath(pathTbx.Text);
            Data.WriteEnvPath(pathTbx.Text);
            Update();
        }

        public void SetStatus(StatusEnum status, string icon, string description = null)
        {
            var prevStatus = Status;
            var prevStatusImg = statusImg.Source;
            var prevTooltip = ToolTip;

            Status = status;
            statusImg.Source = LoadIcon(icon + ".png");
            ToolTip = description;

            if (DataChanged != null &&
                Status != prevStatus &&
                statusImg.Source != prevStatusImg &&
                ToolTip != prevTooltip)
            {
                DataChanged(this, EventArgs.Empty); // emit event
            }
        }

        private BitmapImage LoadIcon(string iconName)
        {
            return new BitmapImage(new Uri("/Icons/" + iconName, UriKind.RelativeOrAbsolute));
        }

        private string NormalizePath(string path)
        {
            char[] charsToTrim = { ' ', '\t', '\\', '/' };
            return path.Trim(charsToTrim);
        }

        private void self_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) Update();
        }

        private void pathTbx_TextChanged(object sender, TextChangedEventArgs e)
        {
            Update();
        }

        private void pathTbx_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyChanges();
        }

        private void detectBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void infoBtn_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(Data.Info);
        }

        private void browseBtn_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = String.Format("Select location: {0}", Data.EnvVarName);
            dialog.SelectedPath = Data.ReadEnvPath();
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog() == true && !String.IsNullOrEmpty(dialog.SelectedPath))
            {
                pathTbx.Text = dialog.SelectedPath;
                ApplyChanges();
            }
        }

        private void setBtn_Click(object sender, RoutedEventArgs e)
        {
            ApplyChanges();
        }
    }
}
