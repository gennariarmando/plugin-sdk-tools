using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PluginSdkWizardInstaller
{
    public partial class BuildConfControl : UserControl
    {
        public SdkComponent Data { get; set; }

        public string FilepathDebug { get; private set; }
        public string FilepathRelease { get; private set; }

        public bool DebugChecked 
        {
            get { return debugCheckbox.IsChecked == true; }
            set { debugCheckbox.IsChecked = value; }
        }

        public bool ReleaseChecked
        {
            get { return releaseCheckbox.IsChecked == true; }
            set { releaseCheckbox.IsChecked = value; }
        }

        public event EventHandler ChoiceChanged;

        public bool DebugBinaryExists 
        {
            get { return File.Exists(FilepathDebug); }
        }

        public bool ReleaseBinaryExists
        {
            get { return File.Exists(FilepathRelease); }
        }

        public BuildConfControl()
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

            nameLbl.Content = Data.Name?.Replace("_", "__");

            FilepathDebug = System.IO.Path.Combine(PathLogic.GetPluginSdkDir(), "output", "lib", Data.ProjectOutput + "_d.lib");
            FilepathRelease = System.IO.Path.Combine(PathLogic.GetPluginSdkDir(), "output", "lib", Data.ProjectOutput + ".lib");

            bool hasDebug = DebugBinaryExists;
            debugImage.Source = GetIcon(hasDebug ? "ok.png" : "nothing.png");
            debugCheckbox.Content = hasDebug ? "Rebuild" : "Build";

            bool hasRelease = ReleaseBinaryExists;
            releaseImage.Source = GetIcon(hasRelease ? "ok.png" : "nothing.png");
            releaseCheckbox.Content = hasRelease ? "Rebuild" : "Build";
        }

        private void CheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ChoiceChanged != null) ChoiceChanged(this, EventArgs.Empty); // emit event
        }

        private void Debug_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DebugChecked = !DebugChecked;
        }

        private void Release_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ReleaseChecked = !ReleaseChecked;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Update();
        }
    }
}
