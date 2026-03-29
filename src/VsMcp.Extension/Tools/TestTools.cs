using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class TestTools
    {
        private static string _lastTrxPath;

        private enum CppTestFramework
        {
            Unknown,
            GoogleTest,
            BoostTest,
            CppUnitTest
        }

        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "test_discover",
                    "Discover all tests in the solution or a specific project. Returns a list of test names.",
                    SchemaBuilder.Create()
                        .AddString("project", "Project name to discover tests in (optional, discovers all if omitted)")
                        .Build()),
                args => TestDiscoverAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "test_run",
                    "Run tests and get results. Supports filtering by test name/category. Returns passed/failed/skipped counts and failure details.",
                    SchemaBuilder.Create()
                        .AddString("project", "Project name to run tests in (optional, runs all if omitted)")
                        .AddString("filter", "Test filter expression (e.g. 'FullyQualifiedName~MyTest', 'TestCategory=Unit')")
                        .AddInteger("timeout", "Timeout in seconds (default: 120)")
                        .Build()),
                args => TestRunAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "test_results",
                    "Get detailed results from the last test run (or a specific TRX file). Shows each test's outcome, duration, and error details.",
                    SchemaBuilder.Create()
                        .AddString("trxPath", "Path to a TRX file (optional, uses last test run result if omitted)")
                        .Build()),
                args => TestResultsAsync(args));
        }

        private static async Task<McpToolResult> TestDiscoverAsync(VsServiceAccessor accessor, JObject args)
        {
            var projectName = args.Value<string>("project");

            string solutionPath = null;
            string solutionDir = null;
            string projectPath = null;
            var cppProjects = new List<CppProjectInfo>();
            bool hasDotNetProjects = false;
            bool isCMakeFolder = false;
            string cmakeFolderPath = null;

            await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                    return;

                solutionPath = dte.Solution.FullName;
                solutionDir = Path.GetDirectoryName(solutionPath);

                // Detect CMake folder mode (no .sln extension = folder open)
                if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    // Folder mode - check if it's a CMake project
                    var cmakeFile = Path.Combine(solutionPath, "CMakeLists.txt");
                    if (File.Exists(cmakeFile))
                    {
                        isCMakeFolder = true;
                        cmakeFolderPath = solutionPath;
                        return;
                    }
                }

                string configName = "Debug";
                string platformName = "x64";
                try
                {
                    var sc = dte.Solution.SolutionBuild?.ActiveConfiguration;
                    if (sc != null)
                        configName = sc.Name ?? "Debug";
                }
                catch { }

                foreach (EnvDTE.Project project in dte.Solution.Projects)
                {
                    try
                    {
                        var fileName = project.FileName;
                        if (string.IsNullOrEmpty(fileName)) continue;

                        // Filter by project name if specified
                        if (!string.IsNullOrEmpty(projectName))
                        {
                            if (!string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                                continue;

                            projectPath = fileName;
                        }

                        if (IsCppProject(fileName))
                        {
                            // Try to get platform from active configuration
                            try
                            {
                                var config = project.ConfigurationManager?.ActiveConfiguration;
                                if (config != null)
                                {
                                    configName = config.ConfigurationName ?? configName;
                                    platformName = config.PlatformName ?? platformName;
                                }
                            }
                            catch { }

                            cppProjects.Add(new CppProjectInfo
                            {
                                Name = project.Name,
                                FilePath = fileName,
                                ConfigName = configName,
                                PlatformName = platformName
                            });
                        }
                        else if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                        {
                            hasDotNetProjects = true;
                        }
                    }
                    catch { }
                }
            });

            if (string.IsNullOrEmpty(solutionPath))
                return McpToolResult.Error("No solution is currently open");

            // CMake folder mode
            if (isCMakeFolder && !string.IsNullOrEmpty(cmakeFolderPath))
            {
                return await Task.Run(() =>
                {
                    var allTests = new List<object>();
                    DiscoverCMakeTests(cmakeFolderPath, allTests);
                    var target = Path.GetFileName(cmakeFolderPath);
                    return McpToolResult.Success(new
                    {
                        testCount = allTests.Count,
                        tests = allTests,
                        target
                    });
                });
            }

            if (!string.IsNullOrEmpty(projectName) && string.IsNullOrEmpty(projectPath) && cppProjects.Count == 0)
                return McpToolResult.Error($"Project '{projectName}' not found");

            return await Task.Run(() =>
            {
                var allTests = new List<object>();

                // 1. Try dotnet test for .NET projects
                if (hasDotNetProjects || (string.IsNullOrEmpty(projectName) && cppProjects.Count == 0))
                {
                    var dotnetTarget = projectPath ?? solutionPath;
                    if (!IsCppProject(dotnetTarget))
                    {
                        var dotnetTests = DiscoverDotNetTests(dotnetTarget);
                        foreach (var t in dotnetTests)
                            allTests.Add(new { name = t, framework = "dotnet" });
                    }
                }

                // 2. Discover C++ tests
                foreach (var cpp in cppProjects)
                {
                    var framework = DetectCppTestFramework(cpp.FilePath);
                    if (framework == CppTestFramework.Unknown) continue;

                    var binaryPath = FindCppOutputBinary(cpp.FilePath, solutionDir, cpp.ConfigName, cpp.PlatformName);
                    if (string.IsNullOrEmpty(binaryPath)) continue;

                    var frameworkName = framework.ToString();
                    var tests = DiscoverCppTests(binaryPath, framework);
                    foreach (var t in tests)
                        allTests.Add(new { name = t, framework = frameworkName, project = cpp.Name });
                }

                var target = !string.IsNullOrEmpty(projectName) ? projectName : Path.GetFileName(solutionPath);
                return McpToolResult.Success(new
                {
                    testCount = allTests.Count,
                    tests = allTests,
                    target
                });
            });
        }

        private static async Task<McpToolResult> TestRunAsync(VsServiceAccessor accessor, JObject args)
        {
            var projectName = args.Value<string>("project");
            var filter = args.Value<string>("filter");
            var timeout = args.Value<int?>("timeout") ?? 120;

            string solutionPath = null;
            string solutionDir = null;
            string projectPath = null;
            var cppProjects = new List<CppProjectInfo>();
            bool hasDotNetProjects = false;
            bool isCMakeFolder = false;
            string cmakeFolderPath = null;

            await accessor.RunOnUIThreadAsync(() =>
            {
                var dte = Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory
                    .Run(() => accessor.GetDteAsync());

                if (dte.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                    return;

                solutionPath = dte.Solution.FullName;
                solutionDir = Path.GetDirectoryName(solutionPath);

                if (!solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                {
                    var cmakeFile = Path.Combine(solutionPath, "CMakeLists.txt");
                    if (File.Exists(cmakeFile))
                    {
                        isCMakeFolder = true;
                        cmakeFolderPath = solutionPath;
                        return;
                    }
                }

                string configName = "Debug";
                string platformName = "x64";
                try
                {
                    var sc = dte.Solution.SolutionBuild?.ActiveConfiguration;
                    if (sc != null)
                        configName = sc.Name ?? "Debug";
                }
                catch { }

                foreach (EnvDTE.Project project in dte.Solution.Projects)
                {
                    try
                    {
                        var fileName = project.FileName;
                        if (string.IsNullOrEmpty(fileName)) continue;

                        if (!string.IsNullOrEmpty(projectName))
                        {
                            if (!string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                                continue;

                            projectPath = fileName;
                        }

                        if (IsCppProject(fileName))
                        {
                            try
                            {
                                var config = project.ConfigurationManager?.ActiveConfiguration;
                                if (config != null)
                                {
                                    configName = config.ConfigurationName ?? configName;
                                    platformName = config.PlatformName ?? platformName;
                                }
                            }
                            catch { }

                            cppProjects.Add(new CppProjectInfo
                            {
                                Name = project.Name,
                                FilePath = fileName,
                                ConfigName = configName,
                                PlatformName = platformName
                            });
                        }
                        else if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                                 fileName.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase))
                        {
                            hasDotNetProjects = true;
                        }
                    }
                    catch { }
                }
            });

            if (string.IsNullOrEmpty(solutionPath))
                return McpToolResult.Error("No solution is currently open");

            // CMake folder mode
            if (isCMakeFolder && !string.IsNullOrEmpty(cmakeFolderPath))
            {
                return await Task.Run(() =>
                {
                    var cmakeResult = RunCMakeTests(cmakeFolderPath, filter, timeout, Path.GetFileName(cmakeFolderPath));
                    var target = Path.GetFileName(cmakeFolderPath);
                    var success = cmakeResult.Failed == 0 && cmakeResult.Total > 0;
                    return McpToolResult.Success(new
                    {
                        success,
                        passed = cmakeResult.Passed,
                        failed = cmakeResult.Failed,
                        skipped = cmakeResult.Skipped,
                        total = cmakeResult.Total,
                        trxPath = (string)null,
                        failedTests = cmakeResult.FailedTests,
                        target,
                        message = success ? "All tests passed" : $"{cmakeResult.Failed} test(s) failed"
                    });
                });
            }

            if (!string.IsNullOrEmpty(projectName) && string.IsNullOrEmpty(projectPath) && cppProjects.Count == 0)
                return McpToolResult.Error($"Project '{projectName}' not found");

            return await Task.Run(() =>
            {
                int totalPassed = 0, totalFailed = 0, totalSkipped = 0, totalCount = 0;
                var allFailedTests = new List<object>();
                string trxPath = null;

                // 1. Run .NET tests
                if (hasDotNetProjects || (string.IsNullOrEmpty(projectName) && cppProjects.Count == 0))
                {
                    var dotnetTarget = projectPath ?? solutionPath;
                    if (!IsCppProject(dotnetTarget))
                    {
                        var result = RunDotNetTests(dotnetTarget, filter, timeout);
                        totalPassed += result.Passed;
                        totalFailed += result.Failed;
                        totalSkipped += result.Skipped;
                        totalCount += result.Total;
                        allFailedTests.AddRange(result.FailedTests);
                        trxPath = result.TrxPath;
                    }
                }

                // 2. Run C++ tests
                foreach (var cpp in cppProjects)
                {
                    var framework = DetectCppTestFramework(cpp.FilePath);
                    if (framework == CppTestFramework.Unknown) continue;

                    var binaryPath = FindCppOutputBinary(cpp.FilePath, solutionDir, cpp.ConfigName, cpp.PlatformName);
                    if (string.IsNullOrEmpty(binaryPath)) continue;

                    var result = RunCppTests(binaryPath, framework, filter, timeout, cpp.Name);
                    totalPassed += result.Passed;
                    totalFailed += result.Failed;
                    totalSkipped += result.Skipped;
                    totalCount += result.Total;
                    allFailedTests.AddRange(result.FailedTests);
                    if (trxPath == null && result.TrxPath != null)
                        trxPath = result.TrxPath;
                }

                if (trxPath != null)
                    _lastTrxPath = trxPath;

                var target = !string.IsNullOrEmpty(projectName) ? projectName : Path.GetFileName(solutionPath);
                var success = totalFailed == 0 && totalCount > 0;
                return McpToolResult.Success(new
                {
                    success,
                    passed = totalPassed,
                    failed = totalFailed,
                    skipped = totalSkipped,
                    total = totalCount,
                    trxPath,
                    failedTests = allFailedTests,
                    target,
                    message = success ? "All tests passed" : $"{totalFailed} test(s) failed"
                });
            });
        }

        private static Task<McpToolResult> TestResultsAsync(JObject args)
        {
            var trxPath = args.Value<string>("trxPath");
            if (string.IsNullOrEmpty(trxPath))
                trxPath = _lastTrxPath;

            if (string.IsNullOrEmpty(trxPath))
                return Task.FromResult(McpToolResult.Error("No TRX file available. Run tests first or specify a trxPath."));

            if (!File.Exists(trxPath))
                return Task.FromResult(McpToolResult.Error($"TRX file not found: {trxPath}"));

            try
            {
                var doc = XDocument.Load(trxPath);
                var ns = doc.Root.GetDefaultNamespace();
                var results = doc.Descendants(ns + "UnitTestResult").ToList();

                var testResults = new List<object>();
                foreach (var r in results)
                {
                    var outcome = r.Attribute("outcome")?.Value ?? "Unknown";
                    var errorMsg = r.Descendants(ns + "Message").FirstOrDefault()?.Value;
                    var stackTrace = r.Descendants(ns + "StackTrace").FirstOrDefault()?.Value;
                    var stdOut = r.Descendants(ns + "StdOut").FirstOrDefault()?.Value;

                    var entry = new Dictionary<string, object>
                    {
                        ["testName"] = r.Attribute("testName")?.Value,
                        ["outcome"] = outcome,
                        ["duration"] = r.Attribute("duration")?.Value
                    };

                    if (!string.IsNullOrEmpty(errorMsg))
                        entry["errorMessage"] = errorMsg;
                    if (!string.IsNullOrEmpty(stackTrace))
                        entry["stackTrace"] = stackTrace;
                    if (!string.IsNullOrEmpty(stdOut))
                        entry["stdOut"] = stdOut;

                    testResults.Add(entry);
                }

                var passed = testResults.Count(r => ((Dictionary<string, object>)r)["outcome"].ToString() == "Passed");
                var failed = testResults.Count(r => ((Dictionary<string, object>)r)["outcome"].ToString() == "Failed");
                var skipped = testResults.Count(r => ((Dictionary<string, object>)r)["outcome"].ToString() == "NotExecuted");

                return Task.FromResult(McpToolResult.Success(new
                {
                    trxPath,
                    totalTests = testResults.Count,
                    passed,
                    failed,
                    skipped,
                    results = testResults
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult(McpToolResult.Error($"Failed to parse TRX file: {ex.Message}"));
            }
        }

        #region .NET test helpers

        private static List<string> DiscoverDotNetTests(string target)
        {
            var arguments = $"test \"{target}\" --list-tests --verbosity quiet --no-build";
            var (exitCode, output) = RunDotnet(arguments, 60);

            var tests = new List<string>();
            var parsing = false;
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed == "The following Tests are available:")
                {
                    parsing = true;
                    continue;
                }
                if (parsing && !string.IsNullOrEmpty(trimmed))
                {
                    tests.Add(trimmed);
                }
            }
            return tests;
        }

        private static DotNetTestResult RunDotNetTests(string target, string filter, int timeout)
        {
            var trxDir = Path.Combine(Path.GetTempPath(), "VsMcp", "TestResults");
            Directory.CreateDirectory(trxDir);
            var trxFileName = $"result_{DateTime.Now:yyyyMMdd_HHmmss}.trx";
            var trxPath = Path.Combine(trxDir, trxFileName);

            var arguments = $"test \"{target}\" --logger \"trx;LogFileName={trxPath}\" --verbosity normal --no-build";
            if (!string.IsNullOrEmpty(filter))
                arguments += $" --filter \"{filter}\"";

            var (exitCode, output) = RunDotnet(arguments, timeout);

            int passed = 0, failed = 0, skipped = 0, total = 0;
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (TryParseCount(trimmed, "Passed:", out var p)) passed = p;
                if (TryParseCount(trimmed, "Failed:", out var f)) failed = f;
                if (TryParseCount(trimmed, "Skipped:", out var s)) skipped = s;
                if (TryParseCount(trimmed, "Total:", out var t)) total = t;
                if (total == 0 && TryParseCount(trimmed, "Total tests:", out var t2)) total = t2;
            }

            var failedTests = new List<object>();
            if (File.Exists(trxPath))
            {
                try
                {
                    var doc = XDocument.Load(trxPath);
                    var ns = doc.Root.GetDefaultNamespace();
                    foreach (var r in doc.Descendants(ns + "UnitTestResult"))
                    {
                        if (r.Attribute("outcome")?.Value == "Failed")
                        {
                            failedTests.Add(new
                            {
                                testName = r.Attribute("testName")?.Value,
                                duration = r.Attribute("duration")?.Value,
                                errorMessage = r.Descendants(ns + "Message").FirstOrDefault()?.Value ?? "",
                                stackTrace = r.Descendants(ns + "StackTrace").FirstOrDefault()?.Value ?? ""
                            });
                        }
                    }
                }
                catch { }
            }

            return new DotNetTestResult
            {
                Passed = passed,
                Failed = failed,
                Skipped = skipped,
                Total = total,
                TrxPath = File.Exists(trxPath) ? trxPath : null,
                FailedTests = failedTests
            };
        }

        #endregion

        #region C++ test helpers

        private static bool IsCppProject(string projectPath)
        {
            return projectPath?.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static CppTestFramework DetectCppTestFramework(string vcxprojPath)
        {
            try
            {
                var content = File.ReadAllText(vcxprojPath);
                if (content.IndexOf("CppUnitTestFramework", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("CppUnitTest.h", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("VS\\UnitTest\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CppTestFramework.CppUnitTest;
                if (content.IndexOf("gtest", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CppTestFramework.GoogleTest;
                if (content.IndexOf("boost_unit_test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    content.IndexOf("boost-test", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CppTestFramework.BoostTest;

                // Also check source files referenced in the vcxproj
                var projectDir = Path.GetDirectoryName(vcxprojPath);
                var doc = XDocument.Load(vcxprojPath);
                var ns = doc.Root.GetDefaultNamespace();
                foreach (var compile in doc.Descendants(ns + "ClCompile"))
                {
                    var include = compile.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;
                    var srcPath = Path.Combine(projectDir, include);
                    if (!File.Exists(srcPath)) continue;
                    try
                    {
                        var src = File.ReadAllText(srcPath);
                        if (src.Contains("gtest/gtest.h") || src.Contains("gtest.h"))
                            return CppTestFramework.GoogleTest;
                        if (src.Contains("boost/test/") || src.Contains("BOOST_AUTO_TEST"))
                            return CppTestFramework.BoostTest;
                        if (src.Contains("CppUnitTest.h") || src.Contains("TEST_CLASS"))
                            return CppTestFramework.CppUnitTest;
                    }
                    catch { }
                }
            }
            catch { }
            return CppTestFramework.Unknown;
        }

        private static string FindCppOutputBinary(string vcxprojPath, string solutionDir, string configName, string platformName)
        {
            var projectDir = Path.GetDirectoryName(vcxprojPath);
            var projectName = Path.GetFileNameWithoutExtension(vcxprojPath);

            // Detect output type from vcxproj
            bool isDll = false;
            try
            {
                var content = File.ReadAllText(vcxprojPath);
                isDll = content.IndexOf("<ConfigurationType>DynamicLibrary</ConfigurationType>", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { }

            var ext = isDll ? ".dll" : ".exe";
            var binaryName = projectName + ext;

            // Search in common output directories
            var candidates = new[]
            {
                Path.Combine(solutionDir, platformName, configName, binaryName),
                Path.Combine(projectDir, platformName, configName, binaryName),
                Path.Combine(solutionDir, "bin", platformName, configName, binaryName),
                Path.Combine(projectDir, "bin", platformName, configName, binaryName),
                // x64 is common shorthand
                Path.Combine(solutionDir, "x64", configName, binaryName),
                Path.Combine(projectDir, "x64", configName, binaryName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static List<string> DiscoverCppTests(string binaryPath, CppTestFramework framework)
        {
            var tests = new List<string>();

            switch (framework)
            {
                case CppTestFramework.GoogleTest:
                    DiscoverGoogleTests(binaryPath, tests);
                    break;
                case CppTestFramework.BoostTest:
                    DiscoverBoostTests(binaryPath, tests);
                    break;
                case CppTestFramework.CppUnitTest:
                    DiscoverCppUnitTests(binaryPath, tests);
                    break;
            }

            return tests;
        }

        private static void DiscoverCMakeTests(string folderPath, List<object> allTests)
        {
            // Find CMake build directories (VS uses out/build/<preset> convention)
            var buildDir = FindCMakeBuildDir(folderPath);
            if (string.IsNullOrEmpty(buildDir)) return;

            var (exitCode, output) = RunProcess("ctest", $"--test-dir \"{buildDir}\" -N", 30);
            if (string.IsNullOrEmpty(output)) return;

            var projectName = Path.GetFileName(folderPath);
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                // Parse "  Test #1: TestName" format
                if (trimmed.StartsWith("Test #"))
                {
                    var colonIdx = trimmed.IndexOf(':');
                    if (colonIdx > 0 && colonIdx + 1 < trimmed.Length)
                    {
                        var testName = trimmed.Substring(colonIdx + 1).Trim();
                        allTests.Add(new { name = testName, framework = "CTest", project = projectName });
                    }
                }
            }
        }

        private static string FindCMakeBuildDir(string folderPath)
        {
            // VS convention: out/build/<preset>
            var outBuildDir = Path.Combine(folderPath, "out", "build");
            if (Directory.Exists(outBuildDir))
            {
                // Use first available preset directory that has CTestTestfile.cmake
                try
                {
                    foreach (var dir in Directory.GetDirectories(outBuildDir))
                    {
                        if (File.Exists(Path.Combine(dir, "CTestTestfile.cmake")))
                            return dir;
                    }
                    // Fallback: use first directory
                    var dirs = Directory.GetDirectories(outBuildDir);
                    if (dirs.Length > 0) return dirs[0];
                }
                catch { }
            }

            // Also check build/ directly
            var buildDirect = Path.Combine(folderPath, "build");
            if (Directory.Exists(buildDirect) && File.Exists(Path.Combine(buildDirect, "CTestTestfile.cmake")))
                return buildDirect;

            return null;
        }

        private static CppTestRunResult RunCMakeTests(string folderPath, string filter, int timeout, string projectName)
        {
            var buildDir = FindCMakeBuildDir(folderPath);
            if (string.IsNullOrEmpty(buildDir))
                return new CppTestRunResult();

            var arguments = $"--test-dir \"{buildDir}\" --output-on-failure";
            if (!string.IsNullOrEmpty(filter))
                arguments += $" -R \"{filter}\"";

            var (exitCode, output) = RunProcess("ctest", arguments, timeout);

            var result = new CppTestRunResult();
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                // Parse "X% tests passed, Y tests failed out of Z"
                if (trimmed.Contains("tests passed") && trimmed.Contains("out of"))
                {
                    // "100% tests passed, 0 tests failed out of 3"
                    if (TryParseCount(trimmed, "tests failed out of", out var _))
                    {
                    }
                    // Extract failed count
                    var failedIdx = trimmed.IndexOf("tests failed", StringComparison.OrdinalIgnoreCase);
                    if (failedIdx > 0)
                    {
                        // Look backwards for the number
                        var beforeFailed = trimmed.Substring(0, failedIdx).Trim();
                        var parts = beforeFailed.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out var f))
                            result.Failed = f;
                    }
                    // Extract total
                    if (TryParseCount(trimmed, "out of", out var total))
                        result.Total = total;
                }
                // Parse individual test failures: "    Start  1: Addition" / "1/3 Test #1: Addition ..... Passed"
                if (trimmed.Contains("***Failed"))
                {
                    // "1/3 Test #1: Addition ............***Failed  0.01 sec"
                    var testStart = trimmed.IndexOf("Test #");
                    if (testStart >= 0)
                    {
                        var afterColon = trimmed.IndexOf(':', testStart + 6);
                        if (afterColon > 0)
                        {
                            var rest = trimmed.Substring(afterColon + 1).Trim();
                            var dotIdx = rest.IndexOf(" .");
                            var starIdx = rest.IndexOf("***");
                            var endIdx = dotIdx >= 0 ? dotIdx : (starIdx >= 0 ? starIdx : rest.Length);
                            var testName = rest.Substring(0, endIdx).Trim();
                            result.FailedTests.Add(new
                            {
                                testName,
                                duration = "",
                                errorMessage = "Test failed",
                                stackTrace = ""
                            });
                        }
                    }
                }
            }

            result.Passed = result.Total - result.Failed;
            if (result.Total == 0 && exitCode == 0)
            {
                // Couldn't parse but ctest succeeded
                result.Total = 1;
                result.Passed = 1;
            }

            return result;
        }

        private static void DiscoverGoogleTests(string binaryPath, List<string> tests)
        {
            var (exitCode, output) = RunProcess(binaryPath, "--gtest_list_tests", 30,
                Path.GetDirectoryName(binaryPath));
            if (exitCode != 0 && string.IsNullOrEmpty(output)) return;

            string currentSuite = null;
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                // Skip the "Running main()" line from gtest_main
                if (line.StartsWith("Running main()")) continue;

                if (!line.StartsWith(" ") && !line.StartsWith("\t") && line.TrimEnd().EndsWith("."))
                {
                    currentSuite = line.Trim().TrimEnd('.');
                }
                else if (currentSuite != null)
                {
                    var testName = line.Trim();
                    // Skip parameterized test comment lines
                    if (!string.IsNullOrEmpty(testName) && !testName.StartsWith("#"))
                        tests.Add($"{currentSuite}.{testName}");
                }
            }
        }

        private static void DiscoverBoostTests(string binaryPath, List<string> tests)
        {
            var (exitCode, output) = RunProcess(binaryPath, "--list_content", 30,
                Path.GetDirectoryName(binaryPath));
            if (exitCode != 0 && string.IsNullOrEmpty(output)) return;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var pathParts = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedEnd = line.TrimEnd();
                if (string.IsNullOrEmpty(trimmedEnd)) continue;

                // Calculate indentation level (4 spaces per level)
                var indent = trimmedEnd.Length - trimmedEnd.TrimStart().Length;
                var level = indent / 4;
                var name = trimmedEnd.Trim().TrimEnd('*').Trim();
                if (string.IsNullOrEmpty(name)) continue;

                // Adjust path to match current level
                while (pathParts.Count > level)
                    pathParts.RemoveAt(pathParts.Count - 1);
                pathParts.Add(name);

                // Check if next line has more indentation (= this is a suite, not a test case)
                bool isSuite = false;
                if (i + 1 < lines.Length)
                {
                    var nextLine = lines[i + 1].TrimEnd();
                    if (!string.IsNullOrEmpty(nextLine))
                    {
                        var nextIndent = nextLine.Length - nextLine.TrimStart().Length;
                        isSuite = nextIndent > indent;
                    }
                }

                // Only add leaf nodes (test cases, not suites)
                if (!isSuite)
                {
                    tests.Add(string.Join("/", pathParts));
                }
            }
        }

        private static void DiscoverCppUnitTests(string binaryPath, List<string> tests)
        {
            // First try vstest.console.exe
            var vstestPath = FindVsTestConsole();
            if (!string.IsNullOrEmpty(vstestPath))
            {
                var (exitCode, output) = RunProcess(vstestPath, $"\"{binaryPath}\" /ListTests", 30);
                if (!string.IsNullOrEmpty(output))
                {
                    var parsing = false;
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("The following Tests are available:") ||
                            trimmed.Contains("テストを使用できます") ||
                            trimmed.Contains("以下のテストを使用できます") ||
                            trimmed.Contains("siguiente") ||    // es: Las siguientes pruebas están disponibles
                            trimmed.Contains("tests suivants") || // fr: Les tests suivants sont disponibles
                            trimmed.Contains("folgenden Tests") || // de: Die folgenden Tests sind verfügbar
                            trimmed.Contains("test seguenti") || // it: I test seguenti sono disponibili
                            trimmed.Contains("testes a seguir") || // pt-BR
                            trimmed.Contains("следующие тесты") || // ru
                            trimmed.Contains("다음 테스트를") ||    // ko
                            trimmed.Contains("以下测试可用") ||     // zh-Hans
                            trimmed.Contains("以下測試可用"))       // zh-Hant
                        {
                            parsing = true;
                            continue;
                        }
                        if (parsing && !string.IsNullOrEmpty(trimmed))
                        {
                            tests.Add(trimmed);
                        }
                    }
                    if (tests.Count > 0) return;
                }
            }

            // Fallback: parse source files for TEST_CLASS/TEST_METHOD macros
            DiscoverCppUnitTestsFromSource(binaryPath, tests);
        }

        private static void DiscoverCppUnitTestsFromSource(string binaryPath, List<string> tests)
        {
            // binaryPath is the DLL, find the vcxproj by looking in the project directory
            var binaryDir = Path.GetDirectoryName(binaryPath);
            var projectName = Path.GetFileNameWithoutExtension(binaryPath);

            // Search for vcxproj in parent directories
            string vcxprojPath = null;
            var searchDir = binaryDir;
            for (int i = 0; i < 5; i++)
            {
                if (searchDir == null) break;
                var candidate = Path.Combine(searchDir, projectName + ".vcxproj");
                if (File.Exists(candidate)) { vcxprojPath = candidate; break; }
                // Also check subdirectory with project name
                candidate = Path.Combine(searchDir, projectName, projectName + ".vcxproj");
                if (File.Exists(candidate)) { vcxprojPath = candidate; break; }
                searchDir = Path.GetDirectoryName(searchDir);
            }

            if (vcxprojPath == null) return;
            var projectDir = Path.GetDirectoryName(vcxprojPath);

            // Parse vcxproj for source files
            try
            {
                var doc = XDocument.Load(vcxprojPath);
                var ns = doc.Root.GetDefaultNamespace();
                foreach (var compile in doc.Descendants(ns + "ClCompile"))
                {
                    var include = compile.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;
                    var srcPath = Path.Combine(projectDir, include);
                    if (!File.Exists(srcPath)) continue;

                    ParseCppUnitTestSource(srcPath, tests);
                }
            }
            catch { }
        }

        private static void ParseCppUnitTestSource(string srcPath, List<string> tests)
        {
            try
            {
                var content = File.ReadAllText(srcPath);
                string currentClass = null;

                foreach (var line in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();

                    // Match TEST_CLASS(ClassName)
                    if (trimmed.StartsWith("TEST_CLASS("))
                    {
                        var start = "TEST_CLASS(".Length;
                        var end = trimmed.IndexOf(')', start);
                        if (end > start)
                            currentClass = trimmed.Substring(start, end - start).Trim();
                    }
                    // Match TEST_METHOD(MethodName)
                    else if (trimmed.StartsWith("TEST_METHOD(") && currentClass != null)
                    {
                        var start = "TEST_METHOD(".Length;
                        var end = trimmed.IndexOf(')', start);
                        if (end > start)
                        {
                            var methodName = trimmed.Substring(start, end - start).Trim();
                            tests.Add($"{currentClass}::{methodName}");
                        }
                    }
                }
            }
            catch { }
        }

        private static CppTestRunResult RunCppTests(string binaryPath, CppTestFramework framework, string filter, int timeout, string projectName)
        {
            switch (framework)
            {
                case CppTestFramework.GoogleTest:
                    return RunGoogleTests(binaryPath, filter, timeout, projectName);
                case CppTestFramework.BoostTest:
                    return RunBoostTests(binaryPath, filter, timeout, projectName);
                case CppTestFramework.CppUnitTest:
                    return RunCppUnitTests(binaryPath, filter, timeout, projectName);
                default:
                    return new CppTestRunResult();
            }
        }

        private static CppTestRunResult RunGoogleTests(string binaryPath, string filter, int timeout, string projectName)
        {
            var resultDir = Path.Combine(Path.GetTempPath(), "VsMcp", "TestResults");
            Directory.CreateDirectory(resultDir);
            var xmlPath = Path.Combine(resultDir, $"gtest_{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}.xml");

            var arguments = $"--gtest_output=xml:\"{xmlPath}\"";
            if (!string.IsNullOrEmpty(filter))
                arguments += $" --gtest_filter={filter}";

            var (exitCode, output) = RunProcess(binaryPath, arguments, timeout,
                Path.GetDirectoryName(binaryPath));

            var result = new CppTestRunResult();

            // Parse the XML output
            if (File.Exists(xmlPath))
            {
                try
                {
                    var doc = XDocument.Load(xmlPath);
                    var testsuites = doc.Element("testsuites");
                    if (testsuites != null)
                    {
                        int.TryParse(testsuites.Attribute("tests")?.Value, out var total);
                        int.TryParse(testsuites.Attribute("failures")?.Value, out var failures);
                        int.TryParse(testsuites.Attribute("disabled")?.Value, out var disabled);
                        result.Total = total;
                        result.Failed = failures;
                        result.Skipped = disabled;
                        result.Passed = total - failures - disabled;

                        foreach (var testcase in doc.Descendants("testcase"))
                        {
                            var failure = testcase.Element("failure");
                            if (failure != null)
                            {
                                result.FailedTests.Add(new
                                {
                                    testName = $"{testcase.Attribute("classname")?.Value}.{testcase.Attribute("name")?.Value}",
                                    duration = testcase.Attribute("time")?.Value,
                                    errorMessage = failure.Attribute("message")?.Value ?? "",
                                    stackTrace = failure.Value ?? ""
                                });
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                // Fallback: parse stdout
                ParseGoogleTestOutput(output, result);
            }

            return result;
        }

        private static void ParseGoogleTestOutput(string output, CppTestRunResult result)
        {
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[  PASSED  ]"))
                {
                    if (TryParseCount(trimmed, "[  PASSED  ]", out var p))
                        result.Passed = p;
                }
                else if (trimmed.StartsWith("[  FAILED  ]"))
                {
                    if (TryParseCount(trimmed, "[  FAILED  ]", out var f))
                        result.Failed = f;
                }
            }
            result.Total = result.Passed + result.Failed;
        }

        private static CppTestRunResult RunBoostTests(string binaryPath, string filter, int timeout, string projectName)
        {
            var resultDir = Path.Combine(Path.GetTempPath(), "VsMcp", "TestResults");
            Directory.CreateDirectory(resultDir);
            var xmlPath = Path.Combine(resultDir, $"boost_{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}.xml");

            var arguments = $"--log_format=XML --log_level=all --log_sink=\"{xmlPath}\" --report_level=detailed";
            if (!string.IsNullOrEmpty(filter))
                arguments += $" --run_test={filter}";

            var (exitCode, output) = RunProcess(binaryPath, arguments, timeout,
                Path.GetDirectoryName(binaryPath));

            var result = new CppTestRunResult();

            // Parse Boost.Test report output
            foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (TryParseCount(trimmed, "test cases:", out var total))
                {
                    result.Total = total;
                    // Format: "X test cases out of Y passed"
                    // or "No errors detected" etc.
                }
                if (trimmed.Contains("passed") && trimmed.Contains("out of"))
                {
                    // "X out of Y assertions passed"
                }
                if (trimmed.Contains("*** No errors detected"))
                {
                    // All tests passed
                }
                if (trimmed.Contains("failures detected"))
                {
                    // Some tests failed
                }
            }

            // Parse XML for detailed results
            if (File.Exists(xmlPath))
            {
                try
                {
                    var content = File.ReadAllText(xmlPath);
                    // Wrap in root element if needed (Boost.Test XML may not have a single root)
                    if (!content.TrimStart().StartsWith("<?xml"))
                        content = "<?xml version=\"1.0\"?><root>" + content + "</root>";
                    else if (!content.Contains("<root>"))
                    {
                        var idx = content.IndexOf("?>");
                        if (idx > 0)
                            content = content.Substring(0, idx + 2) + "<root>" + content.Substring(idx + 2) + "</root>";
                    }

                    var doc = XDocument.Parse(content);
                    var testCases = doc.Descendants("TestCase").ToList();
                    int passed = 0, failed = 0;
                    foreach (var tc in testCases)
                    {
                        var resultAttr = tc.Attribute("result")?.Value;
                        if (resultAttr == "passed") passed++;
                        else if (resultAttr == "failed")
                        {
                            failed++;
                            var msg = tc.Descendants("Message").FirstOrDefault()?.Value ?? "";
                            result.FailedTests.Add(new
                            {
                                testName = tc.Attribute("name")?.Value ?? "",
                                duration = "",
                                errorMessage = msg,
                                stackTrace = ""
                            });
                        }
                    }
                    result.Passed = passed;
                    result.Failed = failed;
                    result.Total = passed + failed;
                }
                catch { }
            }

            // Fallback: if we couldn't parse results, use exit code
            if (result.Total == 0)
            {
                result.Total = 1;
                if (exitCode == 0) result.Passed = 1;
                else result.Failed = 1;
            }

            return result;
        }

        private static CppTestRunResult RunCppUnitTests(string binaryPath, string filter, int timeout, string projectName)
        {
            var vstestPath = FindVsTestConsole();
            if (string.IsNullOrEmpty(vstestPath))
                return new CppTestRunResult();

            var resultDir = Path.Combine(Path.GetTempPath(), "VsMcp", "TestResults");
            Directory.CreateDirectory(resultDir);
            var trxPath = Path.Combine(resultDir, $"mstest_{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}.trx");

            var arguments = $"\"{binaryPath}\" /Logger:\"trx;LogFileName={trxPath}\"";
            if (!string.IsNullOrEmpty(filter))
                arguments += $" /TestCaseFilter:\"{filter}\"";

            var (exitCode, output) = RunProcess(vstestPath, arguments, timeout);

            var result = new CppTestRunResult { TrxPath = trxPath };

            if (File.Exists(trxPath))
            {
                try
                {
                    var doc = XDocument.Load(trxPath);
                    var ns = doc.Root.GetDefaultNamespace();
                    foreach (var r in doc.Descendants(ns + "UnitTestResult"))
                    {
                        var outcome = r.Attribute("outcome")?.Value ?? "Unknown";
                        result.Total++;
                        if (outcome == "Passed") result.Passed++;
                        else if (outcome == "Failed")
                        {
                            result.Failed++;
                            result.FailedTests.Add(new
                            {
                                testName = r.Attribute("testName")?.Value,
                                duration = r.Attribute("duration")?.Value,
                                errorMessage = r.Descendants(ns + "Message").FirstOrDefault()?.Value ?? "",
                                stackTrace = r.Descendants(ns + "StackTrace").FirstOrDefault()?.Value ?? ""
                            });
                        }
                        else result.Skipped++;
                    }
                }
                catch { }
            }

            return result;
        }

        private static string FindVsTestConsole()
        {
            // Get VS install directory from the running devenv.exe process
            try
            {
                var devenvPath = Process.GetCurrentProcess().MainModule.FileName;
                // devenvPath = "C:\...\Common7\IDE\devenv.exe"
                var ideDir = Path.GetDirectoryName(devenvPath);
                var vstestPath = Path.Combine(ideDir, "CommonExtensions", "Microsoft", "TestWindow", "vstest.console.exe");
                if (File.Exists(vstestPath))
                    return vstestPath;

                // Also check Extensions\TestPlatform
                vstestPath = Path.Combine(ideDir, "Extensions", "TestPlatform", "vstest.console.exe");
                if (File.Exists(vstestPath))
                    return vstestPath;
            }
            catch { }

            return null;
        }

        #endregion

        #region Process execution helpers

        private static bool TryParseCount(string line, string prefix, out int count)
        {
            count = 0;
            var idx = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var rest = line.Substring(idx + prefix.Length).Trim();
            var numStr = "";
            foreach (var c in rest)
            {
                if (char.IsDigit(c)) numStr += c;
                else break;
            }
            return int.TryParse(numStr, out count);
        }

        private static (int exitCode, string output) RunDotnet(string arguments, int timeoutSeconds)
        {
            return RunProcess("dotnet", arguments, timeoutSeconds);
        }

        private static (int exitCode, string output) RunProcess(string fileName, string arguments, int timeoutSeconds, string workingDirectory = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(workingDirectory))
                psi.WorkingDirectory = workingDirectory;

            using (var process = new Process { StartInfo = psi })
            {
                var outputBuilder = new System.Text.StringBuilder();
                process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    try { process.Kill(); } catch { }
                    return (-1, outputBuilder.ToString() + "\n[TIMEOUT] Process killed after " + timeoutSeconds + " seconds");
                }

                process.WaitForExit(); // Ensure async output is flushed
                return (process.ExitCode, outputBuilder.ToString());
            }
        }

        #endregion

        #region Data classes

        private class CppProjectInfo
        {
            public string Name { get; set; }
            public string FilePath { get; set; }
            public string ConfigName { get; set; }
            public string PlatformName { get; set; }
        }

        private class DotNetTestResult
        {
            public int Passed { get; set; }
            public int Failed { get; set; }
            public int Skipped { get; set; }
            public int Total { get; set; }
            public string TrxPath { get; set; }
            public List<object> FailedTests { get; set; } = new List<object>();
        }

        private class CppTestRunResult
        {
            public int Passed { get; set; }
            public int Failed { get; set; }
            public int Skipped { get; set; }
            public int Total { get; set; }
            public string TrxPath { get; set; }
            public List<object> FailedTests { get; set; } = new List<object>();
        }

        #endregion
    }
}
