using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Security;

namespace PluginSdkWizardInstaller
{
    class PathLogic
    {
        static public string GetOsVariable(string varName)
        {
            // Check user environment variables.
            {
                string userVar = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);

                if (userVar != null)
                    return userVar;
            }

            // Check system environment variables.
            {
                string globVar = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.Machine);

                if (globVar != null)
                    return globVar;
            }

            return ""; // not found
        }

        public static void SetOsVariable(string varName, string value)
        {
            if (String.IsNullOrWhiteSpace(value)) value = null; // unset the variable

            try
            {
                // we want to target the highest place at which the environment variable is already set at
                string sysValue = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.Machine);
                var target = (sysValue != null) ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;

                Environment.SetEnvironmentVariable(varName, value, target);
            }
            catch (SecurityException)
            {
                MessageBox.Show("Failed to set system env var \"" + varName + "\" (requires admin rights)", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static public string GetPluginSdkDir()
        {
            return GetOsVariable("PLUGIN_SDK_DIR");
        }

        // find MSBuild.exe from Visual Studio 2022 or later
        public static string GetVisualStudio2022MsBuildPath()
        {
            var vswhere = Environment.ExpandEnvironmentVariables(@"%programfiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"); // official fixed path

            if (!File.Exists(vswhere))
            {
                MessageBox.Show("vswhere.exe not found!\nVisual Studio not installed?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = vswhere,
                Arguments = @" -latest -prerelease -version [17,) -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe", // VS 2022 or later
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = psi };
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            process.Start();
            process.WaitForExit();
            Mouse.OverrideCursor = null;

            if (process.ExitCode != 0)
            {
                MessageBox.Show(String.Format("vswhere.exe exited with error code {0}!", process.ExitCode), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }

            string output = process.StandardOutput.ReadToEnd().Trim();

            if (String.IsNullOrEmpty(output))
            {
                MessageBox.Show(String.Format("Visual Studio 2022 or later with MSBuild not found.", process.ExitCode), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return "";
            }

            return output;
        }

        // by Blez
        // https://blez.wordpress.com/2013/02/18/get-file-shortcuts-target-with-c/
        public static string GetShortcutTarget(string file)
        {
            try
            {
                if (Path.GetExtension(file).ToLower() != ".lnk")
                {
                    throw new Exception("Supplied file must be a .LNK file");
                }

                FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read);
                using (BinaryReader fileReader = new BinaryReader(fileStream))
                {
                    fileStream.Seek(0x14, SeekOrigin.Begin);     // Seek to flags
                    uint flags = fileReader.ReadUInt32();        // Read flags
                    if ((flags & 1) == 1)
                    {                      // Bit 1 set means we have to
                                           // skip the shell item ID list
                        fileStream.Seek(0x4c, SeekOrigin.Begin); // Seek to the end of the header
                        uint offset = fileReader.ReadUInt16();   // Read the length of the Shell item ID list
                        fileStream.Seek(offset, SeekOrigin.Current); // Seek past it (to the file locator info)
                    }

                    long fileInfoStartsAt = fileStream.Position; // Store the offset where the file info
                                                                 // structure begins
                    uint totalStructLength = fileReader.ReadUInt32(); // read the length of the whole struct
                    fileStream.Seek(0xc, SeekOrigin.Current); // seek to offset to base pathname
                    uint fileOffset = fileReader.ReadUInt32(); // read offset to base pathname
                                                               // the offset is from the beginning of the file info struct (fileInfoStartsAt)
                    fileStream.Seek((fileInfoStartsAt + fileOffset), SeekOrigin.Begin); // Seek to beginning of
                                                                                        // base pathname (target)
                    long pathLength = (totalStructLength + fileInfoStartsAt) - fileStream.Position - 2; // read
                                                                                                        // the base pathname. I don't need the 2 terminating nulls.
                    char[] linkTarget = fileReader.ReadChars((int)pathLength); // should be unicode safe
                    var link = new string(linkTarget);

                    int begin = link.IndexOf("\0\0");
                    if (begin > -1)
                    {
                        int end = link.IndexOf("\\\\", begin + 2) + 2;
                        end = link.IndexOf('\0', end) + 1;

                        string firstPart = link.Substring(0, begin);
                        string secondPart = link.Substring(end);

                        return firstPart + secondPart;
                    }
                    else
                    {
                        return link;
                    }
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
