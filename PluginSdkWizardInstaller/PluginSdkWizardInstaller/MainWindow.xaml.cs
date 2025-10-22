using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Input;
using System.Linq;

namespace PluginSdkWizardInstaller
{
    public partial class MainWindow : Window
    {
        public static BitmapImage iconError;
        public static BitmapImage iconWarning;
        public static BitmapImage iconOk;
        public static BitmapImage iconNothing;
        public static BitmapImage iconNotSet;

        public static List<SdkComponent> components;

        private static BitmapImage GetIcon(string iconName)
        {
            return new BitmapImage(new Uri("/Icons/" + iconName, UriKind.RelativeOrAbsolute));
        }

        public MainWindow()
        {
            InitializeComponent();
            iconError = GetIcon("error.png");
            iconWarning = GetIcon("warning.png");
            iconOk = GetIcon("ok.png");
            iconNothing = GetIcon("nothing.png");
            iconNotSet = GetIcon("notset.png");

            components = SdkComponent.LoadFromFile("tools\\Plugin-SDK_Wizard_Config.xml");
            if (components.Count == 0)
            {
                System.Windows.Application.Current.Shutdown();
                return;
            }

            bool prevWasBuildable = false;
            foreach (var entry in components)
            {
                var binary = entry.ProjectOutput;
                var isBuildable = !String.IsNullOrEmpty(binary);

                // env variables
                if (!String.IsNullOrEmpty(entry.EnvVarName))
                {
                    if (isBuildable != prevWasBuildable) pathsStack.Children.Add(new Separator());

                    var pathEdit = new SdkPathControl { Data = entry };
                    pathEdit.Update();
                    pathEdit.UpdateColumnsWidth();
                    pathEdit.DataChanged += new EventHandler(delegate (Object o, EventArgs a) { Update(); });
                    pathsStack.Children.Add(pathEdit);

                    prevWasBuildable = isBuildable;
                }

                // plugin build configurations
                if (isBuildable)
                {
                    buildConfigurationStack.Children.Add(new Separator());

                    var config = new BuildConfControl { Data = entry };
                    config.ChoiceChanged += new EventHandler(delegate (Object o, EventArgs a) { UpdatePanelBuild(); });
                    buildConfigurationStack.Children.Add(config);
                }

                // new plugin target games
                if (!String.IsNullOrEmpty(entry.Target))
                {
                    var c = new TargetPlatformControl
                    {
                        Margin = new Thickness(3, 6, 3, 6),
                        Data = entry
                    };
                    c.ChoiceChanged += new EventHandler(delegate (object o, EventArgs e) { UpdatePanelCreatePlugin(); });
                    createPluginTargetContainer.Children.Add(c);
                }

                // new plugin extensions
                if (!String.IsNullOrEmpty(entry.TargetProperty))
                {
                    var c = new CheckBox
                    {
                        Margin = new Thickness(3, 6, 3, 6),
                        Content = entry.Name,
                        ToolTip = entry.Info,
                        Tag = entry
                    };
                    createPluginExtensionContainer.Children.Add(c);
                }
            }
            buildConfigurationStack.Children.Add(new Separator());

            // recalculate column widths
            foreach (UIElement e in pathsStack.Children)
            {
                var c = e as SdkPathControl;
                if (c != null) c.UpdateColumnsWidth();
            }
        }

        private bool WindowInitialized = false;
        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible && !WindowInitialized)
            {
                if (String.IsNullOrEmpty(PathLogic.GetPluginSdkDir()))
                {
                    AutoDetectPaths();
                }

                Update(true);

                WindowInitialized = true;
            }
        }

        private void Window_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (WindowInitialized) Update();
        }

        public void AutoDetectPaths()
        {
            bool changeCursor = Mouse.OverrideCursor == null;
            if (changeCursor) Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            var targets = PathLogic.ListUserShortcutTargets();

            // recalculate column widths
            foreach (UIElement e in pathsStack.Children)
            {
                var c = e as SdkPathControl;
                if (c == null) continue;

                if (!String.IsNullOrWhiteSpace(c.Data.EnvVarName) && String.IsNullOrWhiteSpace(c.Data.ReadEnvPath())) // not set yet
                {
                    if (c.Data.EnvVarName == "PLUGIN_SDK_DIR")
                    {
                        c.SetPath(System.Environment.CurrentDirectory); // this wizard app is supposed to be in PSDK's root dir)
                        continue;
                    }

                    if (!String.IsNullOrWhiteSpace(c.Data.CheckFile))
                    {
                        var pattern = c.Data.CheckFile.ToLower();
                        foreach (var path in targets)
                        {
                            if (path.ToLower().EndsWith(pattern))
                            {
                                // make sure we not matched just suffix of the filename
                                var ch = path.Length > pattern.Length ? path[path.Length - pattern.Length - 1] : Path.DirectorySeparatorChar;
                                if (ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar)
                                {
                                    var dir = path.Substring(0, path.Length - pattern.Length);
                                    c.SetPath(dir);
                                }
                            }
                        }
                    }
                }
            }

            if (changeCursor) Mouse.OverrideCursor = null;
        }

        // update GUI state
        public void Update(bool folding = false)
        {
            if (UpdateInProgress) return;
            UpdateInProgress = true;

            if (folding) stepGroup_envVars.IsExpanded = false;
            stepGroup_envVarsIcon.Source = null;
            stepGroup_envVarsIcon.ToolTip = null;

            if (folding) stepGroup_generateSolution.IsExpanded = false;
            stepGroup_generateSolution.IsEnabled = false;
            stepGroup_generateSolutionIcon.Source = null;
            stepGroup_generateSolutionIcon.ToolTip = null;

            if (folding) stepGroup_build.IsExpanded = false;
            stepGroup_build.IsEnabled = false;
            stepGroup_buildIcon.Source = null;
            stepGroup_buildIcon.ToolTip = null;

            if (folding) stepGroup_createPlugin.IsExpanded = false;
            stepGroup_createPlugin.IsEnabled = false;
            stepGroup_createPluginIcon.Source = null;
            stepGroup_createPluginIcon.ToolTip = null;

            // step 1: env paths config
            if (!UpdatePanelEnvVariables(folding))
            {
                UpdateInProgress = false;
                return;
            }

            // step 2: generate Plugin solution
            stepGroup_generateSolution.IsEnabled = true;
            if (!UpdatePanelGenerateSolution(folding))
            {
                UpdateInProgress = false;
                return;
            }

            // step 3: build plugin binaries
            stepGroup_build.IsEnabled = true;
            if (!UpdatePanelBuild(folding))
            {
                if (folding) stepGroup_generateSolution.IsExpanded = true;
                UpdateInProgress = false;
                return;
            }

            // step 4: new user plugin project
            stepGroup_createPlugin.IsEnabled = true;
            if (!UpdatePanelCreatePlugin(folding))
            {
                UpdateInProgress = false;
                return;
            }

            UpdateInProgress = false;
        }
        private bool UpdateInProgress = false;

        private bool UpdatePanelEnvVariables(bool folding = false)
        {
            SdkPathControl.StatusEnum status = SdkPathControl.StatusEnum.None;
            bool hasGtaDir = false;
            foreach (UIElement e in pathsStack.Children)
            {
                var c = e as SdkPathControl;
                if (c != null)
                {
                    c.Update();
                    var s = c.GetStatus();
                    if (s.status > status)
                    {
                        status = s.status;
                        stepGroup_envVarsIcon.Source = s.icon;
                        stepGroup_envVarsIcon.ToolTip = s.description;
                    }

                    if (!String.IsNullOrEmpty(c.Data.Project) && s.status == SdkPathControl.StatusEnum.Ok) hasGtaDir = true;
                }
            }
            if (status == SdkPathControl.StatusEnum.Error)
            {
                if (folding) stepGroup_envVars.IsExpanded = true;
                return false;
            }

            if (!hasGtaDir)
            {
                if (folding) stepGroup_envVars.IsExpanded = true;
                stepGroup_envVarsIcon.Source = iconWarning;
                stepGroup_envVarsIcon.ToolTip = "Not a single game directory was specified";

                foreach (UIElement e in pathsStack.Children)
                {
                    var c = e as SdkPathControl;
                    if (c != null && !String.IsNullOrEmpty(c.Data.Project))
                    {
                        var s = c.GetStatus();
                        if (s.status == SdkPathControl.StatusEnum.None) c.SetStatus(SdkPathControl.StatusEnum.Ok, "warning", "Not set"); // actually not ok
                    }
                }
                return false;
            }

            return true;
        }

        private bool UpdatePanelGenerateSolution(bool folding = false)
        {
            if (File.Exists(Path.Combine(PathLogic.GetPluginSdkDir(), "plugin.sln")))
            {
                stepGroup_generateSolutionIcon.Source = iconOk;
            }
            else
            {
                if (folding) stepGroup_generateSolution.IsExpanded = true;
                stepGroup_generateSolutionIcon.Source = iconError;
                stepGroup_generateSolutionIcon.ToolTip = "Plugin SDK project solution not found";
                return false;
            }

            return true;
        }

        private void btnGenerateSlnVS_Click(object sender, RoutedEventArgs e)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.WorkingDirectory = Path.Combine(PathLogic.GetPluginSdkDir(), "tools", "generate");
            psi.FileName = Path.Combine(psi.WorkingDirectory, "Visual Studio.bat");

            if (File.Exists(psi.FileName))
            {
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                IsEnabled = false;
                UpdateLayout();

                var process = new Process { StartInfo = psi };
                process.Start();
                process.WaitForExit();

                Mouse.OverrideCursor = null;
                IsEnabled = true; // freeze main window
                Update(true);
            }
            else
                MessageBox.Show(String.Format("Can't find '{0}'", psi.FileName));
        }

        private bool UpdatePanelBuild(bool folding = false)
        {
            int selected = 0;
            int built = 0;
            foreach (var child in buildConfigurationStack.Children)
            {
                var c = child as BuildConfControl;
                if (c != null)
                {
                    c.Update();
                    if (c.DebugChecked) selected++;
                    if (c.ReleaseChecked) selected++;
                    if (c.DebugBinaryExists) built++;
                    if (c.ReleaseBinaryExists) built++;
                }
            }

            if (built > 0)
            {
                if (folding) stepGroup_build.IsExpanded = false;
                stepGroup_buildIcon.Source = iconOk;
                stepGroup_buildIcon.ToolTip = null;
            }
            else
            {
                if (folding) stepGroup_build.IsExpanded = true;
                stepGroup_buildIcon.Source = iconError;
                stepGroup_buildIcon.ToolTip = "No binary has been builded yet";
            }

            stepGroup_buildButtonsGrid.IsEnabled = selected > 0;
            stepGroup_buildVsBtnLbl.Content = $"({selected})";

            return built > 0;
        }

        private static bool buildProjectConfiguration(string msbuild, string workdir, string solution, string project, string configuration)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "CMD.EXE",
                WorkingDirectory = PathLogic.GetPluginSdkDir(),
                Arguments = String.Format("/v /c " +
                    "@ECHO off & " +
                    "TITLE GTA Plugin-SDK: building {2} ({3}) & " +
                    "\"{0}\" {1} /t:{2} /property:Configuration={3} /m & ", msbuild, solution, project, configuration) +
                    "IF !ERRORLEVEL! NEQ 0 pause"
            };

            var process = new Process { StartInfo = psi };
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            process.Start();
            process.WaitForExit();
            Mouse.OverrideCursor = null;

            return process.ExitCode == 0;
        }

        private void stepGroup_buildVsBtnClick(object sender, RoutedEventArgs e)
        {
            var msbuild = PathLogic.GetVisualStudio2022MsBuildPath();
            var sdkDir = PathLogic.GetPluginSdkDir();

            if (String.IsNullOrEmpty(msbuild) || String.IsNullOrEmpty(sdkDir))
            {
                return;
            }

            IsEnabled = false;
            foreach (var child in buildConfigurationStack.Children)
            {
                var c = child as BuildConfControl;
                if (c != null)
                {
                    if (c.DebugChecked)
                    {
                        if (!buildProjectConfiguration(msbuild, sdkDir, "plugin.sln", c.Data.Project, "zDebug"))
                        {
                            IsEnabled = true;
                            Update();
                            return;
                        }

                        c.DebugChecked = false;
                        Update();
                        UpdateLayout();
                    }

                    if (c.ReleaseChecked)
                    {
                        if (!buildProjectConfiguration(msbuild, sdkDir, "plugin.sln", c.Data.Project, "Release"))
                        {
                            IsEnabled = true;
                            Update();
                            return;
                        }

                        c.ReleaseChecked = false;
                        Update();
                        UpdateLayout();
                    }
                }
            }

            Update();
            IsEnabled = true;
        }

        private bool UpdatePanelCreatePlugin(bool folding = false)
        {
            if (folding) stepGroup_createPlugin.IsExpanded = true;
            
            if (!stepGroup_createPlugin.IsVisible) return false;

            stepGroup_createPluginButtonsGrid.IsEnabled = false;

            foreach (UIElement child in createPluginTargetContainer.Children)
            {
                var c = child as TargetPlatformControl;
                if (c != null) c.Update();
            }

            string name = pluginNameTbx.Text.Trim();
            if (String.IsNullOrWhiteSpace(name))
            {
                pluginNameStatusImg.Source = iconError;
                pluginNameGrp.ToolTip = stepGroup_createPluginButtonsGrid.ToolTip = "No name specified";
                return false;
            }

            if (name.Any(ch => Path.GetInvalidFileNameChars().Contains(ch) || Char.IsWhiteSpace(ch)))
            {
                pluginNameStatusImg.Source = iconError;
                pluginNameGrp.ToolTip = stepGroup_createPluginButtonsGrid.ToolTip = "Name contains forbidden character(s)";
                return false;
            }

            var a = Path.Combine(PathLogic.GetPluginSdkDir(), "tools\\myplugin-gen\\generated", name, name + ".sln");
            if (File.Exists(Path.Combine(PathLogic.GetPluginSdkDir(), "tools\\myplugin-gen\\generated", name, name + ".sln")))
            {
                pluginNameStatusImg.Source = iconError;
                pluginNameGrp.ToolTip = stepGroup_createPluginButtonsGrid.ToolTip = "Plugin project with specified name already exists";
                return false;
            }

            pluginNameStatusImg.Source = iconOk;
            pluginNameGrp.ToolTip = stepGroup_createPluginButtonsGrid.ToolTip = null;

            int targetCount = 0;
            foreach (UIElement child in createPluginTargetContainer.Children)
            {
                var c = child as TargetPlatformControl;
                if (c != null && c.IsChecked == true) targetCount++;
            }
            
            if (targetCount == 0) stepGroup_createPluginButtonsGrid.ToolTip = "No target game has been selected";
            stepGroup_createPluginButtonsGrid.IsEnabled = targetCount > 0;
            return true;
        }

        private static bool generateNewPlugin(string dirPath, string name, List<string> targets)
        {
            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            string trgStr = "";
            foreach (var t in targets)
            {
                if (!String.IsNullOrEmpty(trgStr)) trgStr += " ";
                trgStr += $"--{t}";
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "CMD.EXE",
                WorkingDirectory = Path.Combine(PathLogic.GetPluginSdkDir(), "tools\\premake"),
                Arguments = "/v /c " +
                    "@ECHO off & " +
                    "TITLE GTA Plugin-SDK: generating new plugin & " +
                    $"premake5.exe --file=premake5.lua newplugin --dir=\"{dirPath}\" --name=\"{name}\" {trgStr} & " +
                    "IF !ERRORLEVEL! NEQ 0 pause"
            };

            var process = new Process { StartInfo = psi };
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            process.Start();
            process.WaitForExit();
            Mouse.OverrideCursor = null;

            return process.ExitCode == 0;
        }

        private void stepGroup_createPluginVsBtnClick(object sender, RoutedEventArgs e)
        {
            string name = pluginNameTbx.Text.Trim();
            string projectDir = Path.Combine(PathLogic.GetPluginSdkDir(), "tools\\myplugin-gen\\generated", name);

            var targets = new List<string>();
            foreach (UIElement child in createPluginTargetContainer.Children)
            {
                var c = child as TargetPlatformControl;
                if (c != null && c.IsChecked == true)
                {
                    targets.Add(c.Data.Target);
                }
            }

            // extensions (provided in same way as targets)
            foreach (UIElement child in createPluginExtensionContainer.Children)
            {
                var c = child as CheckBox;
                var data = c?.Tag as SdkComponent;
                if (c != null && c.IsChecked == true && data != null)
                {
                    targets.Add(data.TargetProperty);
                }
            }

            if (generateNewPlugin(projectDir, name, targets))
            {
                Process.Start(projectDir); // open directory in Explorer
            }

            UpdatePanelCreatePlugin();
        }

        private void pluginNameTbx_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePanelCreatePlugin();
        }
    }
}
