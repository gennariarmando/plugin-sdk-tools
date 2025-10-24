using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PluginSdkWizardInstaller
{
    public partial class TargetPlatformControl : UserControl
    {
        public SdkComponent Data { get; set; }

        public bool IsChecked 
        {
            get { return checkBox.IsChecked == true; }
            set { checkBox.IsChecked = value; }
        }

        public bool DebugBinaryExists
        {
            get { return File.Exists(Path.Combine(PathLogic.GetPluginSdkDir(), "output", "lib", Data.ProjectOutput + "_d.lib")); }
        }

        public bool ReleaseBinaryExists
        {
            get { return File.Exists(Path.Combine(PathLogic.GetPluginSdkDir(), "output", "lib", Data.ProjectOutput + ".lib")); }
        }

        public event EventHandler ChoiceChanged;

        public TargetPlatformControl()
        {
            InitializeComponent();
            Update();
        }

        private BitmapImage GetIcon(string iconName)
        {
            return new BitmapImage(new Uri("/Icons/" + iconName, UriKind.RelativeOrAbsolute));
        }

        public void Update()
        {
            if (Data == null) return;

            checkBox.Content = Data.Name?.Replace("_", "__");

            bool hasDebug = DebugBinaryExists;
            bool hasRelease = ReleaseBinaryExists;

            if (hasDebug && hasRelease)
            {
                ToolTip = null;
                infoImage.Source = GetIcon("ok.png");
            }
            else if (!hasDebug && !hasRelease)
            {
                ToolTip = "Binaries for this platform has not been built";
                infoImage.Source = GetIcon("nothing.png");
            }
            else
            {
                infoImage.Source = GetIcon("warning.png");
                ToolTip = "Only " +
                    (hasDebug ? "Debug" : "Release") +
                    " configuration binary has been built";
            }
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ChoiceChanged != null) ChoiceChanged(this, EventArgs.Empty); // emit event
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            IsChecked = !IsChecked;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible) Update();
        }
    }
}
