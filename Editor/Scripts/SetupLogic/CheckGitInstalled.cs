using System;
using System.Diagnostics;
using UnityEditor;

namespace Reflectis.SetupEditor
{
    [InitializeOnLoad]
    public class CheckGitInstalled
    {
        private static bool hasChecked = false;
        static CheckGitInstalled()
        {
            if (!hasChecked)
            {
                CheckGitInstallation();
                hasChecked = true;
            }
        }

        private static void CheckGitInstallation()
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        //EditorUtility.DisplayDialog("Git Check", "Git is installed, you're ready to install other packages! Version: " + output, "OK");
                        //UnityEngine.Debug.LogError("Git is installed with version " + output);

                        //TODO show the window for dependencies packages and other of reflectis
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Git Check", "Git is not installed or not found. Please install it from https://git-scm.com/", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Git Check", "An error occurred while checking for Git:\n" + ex.Message, "OK");
            }
        }
    }
}
