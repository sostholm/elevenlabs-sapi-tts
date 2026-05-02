using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using ElevenLabsTtsEngine.Config;
using ElevenLabsTtsEngine.Installer;

namespace ElevenLabsTtsInstaller
{
    internal static class Program
    {
        private static readonly string InstallDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ElevenLabs SAPI TTS");

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return 2;
                }

                var command = args[0].ToLowerInvariant();
                if (command == "install")
                {
                    RequireAdministrator();
                    return Install(args.Skip(1).ToArray());
                }

                if (command == "refresh")
                {
                    RequireAdministrator();
                    return Refresh();
                }

                if (command == "register-voice")
                {
                    RequireAdministrator();
                    return RegisterVoice(args.Skip(1).ToArray());
                }

                if (command == "uninstall")
                {
                    RequireAdministrator();
                    return Uninstall();
                }

                PrintUsage();
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static int Install(string[] args)
        {
            var config = ConfigManager.LoadOrCreate();
            if (!string.IsNullOrWhiteSpace(GetOption(args, "--api-key")))
            {
                throw new InvalidOperationException("--api-key is no longer supported because command-line secrets are exposed to process listings and shell history.");
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                config.ApiKey = PromptForSecret("ElevenLabs API key: ");
            }

            if (string.IsNullOrWhiteSpace(config.ApiKey))
            {
                throw new InvalidOperationException("An ElevenLabs API key is required.");
            }

            ConfigManager.Save(config);

            var engineDll = InstallRuntime(GetOption(args, "--dll"));
            RegisterCom(engineDll, unregister: false);

            var manualVoiceId = GetOption(args, "--voice-id");
            if (!string.IsNullOrWhiteSpace(manualVoiceId))
            {
                new VoiceRegistrar().RegisterManualVoice(
                    manualVoiceId,
                    GetOption(args, "--voice-name") ?? manualVoiceId,
                    GetOption(args, "--gender") ?? "Neutral",
                    GetOption(args, "--age") ?? "Adult");
            }
            else
            {
                new VoiceRegistrar().Refresh(config);
            }

            Console.WriteLine("Installed ElevenLabs SAPI voices.");
            return 0;
        }

        private static int Refresh()
        {
            var config = ConfigManager.LoadOrCreate();
            new VoiceRegistrar().Refresh(config);
            Console.WriteLine("Refreshed ElevenLabs SAPI voices.");
            return 0;
        }

        private static int Uninstall()
        {
            new VoiceRegistrar().UnregisterAll();

            var engineDll = Path.Combine(InstallDirectory, "ElevenLabsTtsEngine.dll");
            if (File.Exists(engineDll))
            {
                RegisterCom(engineDll, unregister: true);
            }

            ConfigManager.DeleteConfig();
            DeleteInstallDirectory();
            Console.WriteLine("Uninstalled ElevenLabs SAPI voices.");
            return 0;
        }

        private static int RegisterVoice(string[] args)
        {
            var voiceId = GetOption(args, "--voice-id");
            var voiceName = GetOption(args, "--voice-name") ?? voiceId;
            new VoiceRegistrar().RegisterManualVoice(
                voiceId,
                voiceName,
                GetOption(args, "--gender") ?? "Neutral",
                GetOption(args, "--age") ?? "Adult");
            Console.WriteLine($"Registered ElevenLabs voice token: {voiceName}");
            return 0;
        }

        private static void RegisterCom(string engineDll, bool unregister)
        {
            if (!File.Exists(engineDll))
            {
                throw new FileNotFoundException("Engine DLL not found.", engineDll);
            }

            foreach (var regasm in GetRegAsmPaths())
            {
                if (!File.Exists(regasm))
                {
                    continue;
                }

                var arguments = unregister
                    ? $"/u \"{engineDll}\""
                    : $"\"{engineDll}\" /codebase";

                var start = new ProcessStartInfo(regasm, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(start))
                {
                    process.WaitForExit();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException($"{regasm} failed with exit code {process.ExitCode}.\r\n{output}\r\n{error}");
                    }

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        Console.WriteLine(output.Trim());
                    }
                }
            }
        }

        private static string InstallRuntime(string engineDllOverride)
        {
            Directory.CreateDirectory(InstallDirectory);

            var sourceDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var sourceEngineDll = !string.IsNullOrWhiteSpace(engineDllOverride)
                ? Path.GetFullPath(engineDllOverride)
                : Path.Combine(sourceDirectory, "ElevenLabsTtsEngine.dll");

            if (!File.Exists(sourceEngineDll))
            {
                throw new FileNotFoundException("Engine DLL not found.", sourceEngineDll);
            }

            var files = new List<string>
            {
                Path.Combine(sourceDirectory, "ElevenLabsTtsInstaller.exe"),
                Path.Combine(sourceDirectory, "ElevenLabsTtsInstaller.exe.config"),
                Path.Combine(sourceDirectory, "Newtonsoft.Json.dll"),
                sourceEngineDll
            };

            foreach (var file in files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var destination = Path.Combine(InstallDirectory, Path.GetFileName(file));
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, destination, overwrite: true);
            }

            return Path.Combine(InstallDirectory, "ElevenLabsTtsEngine.dll");
        }

        private static void DeleteInstallDirectory()
        {
            try
            {
                if (!Directory.Exists(InstallDirectory))
                {
                    return;
                }

                foreach (var file in Directory.GetFiles(InstallDirectory))
                {
                    if (!string.Equals(file, System.Reflection.Assembly.GetExecutingAssembly().Location, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(file);
                    }
                }

                if (!Directory.EnumerateFileSystemEntries(InstallDirectory).Any())
                {
                    Directory.Delete(InstallDirectory, false);
                }
            }
            catch (IOException)
            {
                Console.Error.WriteLine("Install directory could not be fully removed because a file is still in use.");
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine("Install directory could not be fully removed because access was denied.");
            }
        }

        private static string[] GetRegAsmPaths()
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return new[]
            {
                Path.Combine(windows, @"Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"),
                Path.Combine(windows, @"Microsoft.NET\Framework\v4.0.30319\RegAsm.exe")
            };
        }

        private static string GetOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string PromptForSecret(string prompt)
        {
            Console.Write(prompt);
            var secret = string.Empty;

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return secret;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (secret.Length > 0)
                    {
                        secret = secret.Substring(0, secret.Length - 1);
                    }

                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                {
                    secret += key.KeyChar;
                    Console.Write("*");
                }
            }
        }

        private static void RequireAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new InvalidOperationException("This command must be run from an elevated Administrator console.");
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  ElevenLabsTtsInstaller install [--dll path-to-engine-dll]");
            Console.WriteLine("  ElevenLabsTtsInstaller install [--voice-id id --voice-name name]");
            Console.WriteLine("  ElevenLabsTtsInstaller refresh");
            Console.WriteLine("  ElevenLabsTtsInstaller register-voice --voice-id id [--voice-name name] [--gender value] [--age value]");
            Console.WriteLine("  ElevenLabsTtsInstaller uninstall");
        }
    }
}
