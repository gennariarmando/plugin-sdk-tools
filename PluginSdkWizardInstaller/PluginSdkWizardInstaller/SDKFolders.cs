using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace PluginSdkWizardInstaller
{
    public class SdkComponent
    {
        public string Name;
        public string EnvVarName;
        public bool Mandatory;
        public string CheckFile;
        public string Project;
        public string ProjectOutput;
        public string Target;
        public string TargetProperty;
        public string Info;

        static public List<SdkComponent> LoadFromFile(string filePath)
        {
            var result = new List<SdkComponent>();

            try
            {
                XDocument doc = XDocument.Load(filePath);
                
                int num;
                foreach (var c in doc.Descendants("component"))
                {
                    var component = new SdkComponent
                    {
                        Name = c.Element("name").Value // must be present
                    };

                    component.EnvVarName = c.Element("envVar")?.Value;
                    component.CheckFile = c.Element("checkFile")?.Value;

                    int.TryParse(c.Element("mandatory")?.Value, out num);
                    component.Mandatory = num != 0;

                    component.Project = c.Element("project")?.Value;
                    component.ProjectOutput = c.Element("projectOutput")?.Value;
                    component.Target = c.Element("target")?.Value;
                    component.TargetProperty = c.Element("targetProperty")?.Value;
                    component.Info = c.Element("info")?.Value;

                    result.Add(component);
                }
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"The file '{filePath}' was not found.", "Config loading error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Config loading error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return result;
        }

        public string ReadEnvPath()
        {
            return PathLogic.GetOsVariable(EnvVarName);
        }

        public void WriteEnvPath(string path)
        {
            PathLogic.SetOsVariable(EnvVarName, path);
        }

        public bool IsPathValid()
        {
            var path = ReadEnvPath();

            if (String.IsNullOrEmpty(path)) return !Mandatory;

            if (String.IsNullOrEmpty(CheckFile)) return true;

            return File.Exists(Path.Combine(path, CheckFile));
        }
    };
}
