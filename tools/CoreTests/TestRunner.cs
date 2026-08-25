using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Game.Tests.Headless
{
    /// <summary>
    /// Minimal reflection-based NUnit runner.
    ///
    /// The canonical suite lives in Assets/_Project/Code/Tests/EditMode and runs under Unity's
    /// Test Runner. This executes those exact same source files headlessly so the rules layer
    /// can be developed without the Editor. It deliberately supports only the subset of NUnit
    /// the suite uses — [Test], [TestFixture], [SetUp], [TearDown] — and fails loudly on
    /// anything richer rather than silently skipping it.
    /// </summary>
    public static class TestRunner
    {
        private sealed class Failure
        {
            public string Test;
            public string Message;
            public string SourceFile;   // absolute path, or null when the stack carried no file info
            public int SourceLine;
        }

        public static int Main(string[] args)
        {
            // `dotnet run` can forward a bare "--" separator; treat that (and blanks) as no filter.
            string filter = args?
                .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a) && a != "--");

            var fixtures = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(IsFixture)
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

            var unsupported = FindUnsupportedAttributes(fixtures);
            if (unsupported.Count > 0)
            {
                string detail = "This runner does not implement: " + string.Join(", ", unsupported);
                Console.WriteLine(detail);
                Console.WriteLine("Run the suite in Unity's Test Runner, or keep the tests to the supported subset.");
                if (UnderGitHubActions)
                    Console.WriteLine("::error title=Unsupported NUnit attribute::" + EscapeData(detail));
                return 2;
            }

            int passed = 0, skipped = 0;
            var failures = new List<Failure>();
            var sw = Stopwatch.StartNew();

            foreach (var fixture in fixtures)
            {
                var tests = fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
                    .OrderBy(m => m.Name, StringComparer.Ordinal)
                    .ToList();

                if (tests.Count == 0) continue;

                var setUp = SingleMethod<SetUpAttribute>(fixture);
                var tearDown = SingleMethod<TearDownAttribute>(fixture);
                bool headerWritten = false;

                foreach (var test in tests)
                {
                    string name = fixture.Name + "." + test.Name;
                    if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (!headerWritten)
                    {
                        Console.WriteLine();
                        Console.WriteLine(fixture.Name);
                        headerWritten = true;
                    }

                    var failure = RunOne(fixture, test, setUp, tearDown);
                    if (failure == null)
                    {
                        passed++;
                        Console.WriteLine("  ok   " + test.Name);
                    }
                    else
                    {
                        failure.Test = name;
                        failures.Add(failure);
                        Console.WriteLine("  FAIL " + test.Name);
                    }
                }
            }

            sw.Stop();

            if (failures.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine(new string('-', 60));
                foreach (var f in failures)
                {
                    Console.WriteLine();
                    Console.WriteLine(f.Test);
                    foreach (var line in f.Message.Split('\n'))
                        Console.WriteLine("    " + line.TrimEnd());
                }
            }

            Console.WriteLine();
            Console.WriteLine(new string('-', 60));
            string tally = $"{passed} passed, {failures.Count} failed"
                + (skipped > 0 ? $", {skipped} filtered out" : string.Empty)
                + $"   ({sw.ElapsedMilliseconds} ms)";
            Console.WriteLine(tally);

            ReportToGitHubActions(failures, tally);

            return failures.Count > 0 ? 1 : 0;
        }

        private static Failure RunOne(Type fixture, MethodInfo test, MethodInfo setUp, MethodInfo tearDown)
        {
            object instance;
            try
            {
                instance = Activator.CreateInstance(fixture);
            }
            catch (Exception e)
            {
                return Fail("could not construct fixture: " + Unwrap(e), Unwrap(e));
            }

            try
            {
                setUp?.Invoke(instance, null);
                test.Invoke(instance, null);
                return null;
            }
            catch (Exception e)
            {
                var actual = Unwrap(e);
                return IsSuccess(actual) ? null : Fail(Describe(actual), actual);
            }
            finally
            {
                try { tearDown?.Invoke(instance, null); }
                catch { /* a failing teardown must not mask the real failure */ }
            }
        }

        private static Failure Fail(string message, Exception cause)
        {
            var (file, line) = Locate(cause);
            return new Failure { Message = message, SourceFile = file, SourceLine = line };
        }

        /// <summary>
        /// Pulls the failing source location out of the exception's stack trace. Debug builds carry
        /// portable PDBs, so frames read "… in /path/File.cs:line 42"; the innermost frame under
        /// Assets/ is the test (or test helper) itself, skipping past NUnit's own assertion frames,
        /// which ship without symbols.
        /// </summary>
        private static (string File, int Line) Locate(Exception e)
        {
            if (string.IsNullOrEmpty(e?.StackTrace)) return (null, 0);

            foreach (var frame in e.StackTrace.Split('\n'))
            {
                var m = Regex.Match(frame, @" in (.+):line (\d+)");
                if (!m.Success) continue;

                string file = m.Groups[1].Value.Replace('\\', '/');
                if (file.Contains("/Assets/"))
                    return (file, int.Parse(m.Groups[2].Value));
            }

            return (null, 0);
        }

        private static bool UnderGitHubActions =>
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

        /// <summary>
        /// Machine-readable output for CI (STORY-0.6 AC4). Under GitHub Actions this emits one
        /// ::error workflow command per failure — which Actions turns into a PR annotation on the
        /// failing test's source line — and appends a run summary to the job summary page.
        /// Anywhere else it does nothing, so local runs stay exactly as they were.
        /// </summary>
        private static void ReportToGitHubActions(List<Failure> failures, string tally)
        {
            if (!UnderGitHubActions) return;

            string workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");

            foreach (var f in failures)
            {
                string location = string.Empty;
                if (f.SourceFile != null)
                {
                    // Annotations attach to files by repo-relative path.
                    string path = f.SourceFile;
                    if (!string.IsNullOrEmpty(workspace))
                    {
                        string prefix = workspace.Replace('\\', '/').TrimEnd('/') + "/";
                        if (path.StartsWith(prefix, StringComparison.Ordinal))
                            path = path.Substring(prefix.Length);
                    }
                    location = $"file={EscapeProperty(path)},line={f.SourceLine},";
                }

                Console.WriteLine($"::error {location}title={EscapeProperty(f.Test)}::{EscapeData(f.Message)}");
            }

            string summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
            if (string.IsNullOrEmpty(summaryPath)) return;

            var sb = new StringBuilder();
            sb.AppendLine(failures.Count == 0 ? "### Core suite: green" : "### Core suite: FAILED");
            sb.AppendLine();
            sb.AppendLine(tally);
            foreach (var f in failures)
            {
                sb.AppendLine();
                sb.AppendLine($"**{f.Test}**");
                sb.AppendLine("```");
                sb.AppendLine(f.Message);
                sb.AppendLine("```");
            }
            System.IO.File.AppendAllText(summaryPath, sb.ToString());
        }

        /// <summary>Workflow-command escaping, per GitHub's rules for the message half.</summary>
        private static string EscapeData(string s) =>
            s.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A");

        /// <summary>Property values (file=, title=) additionally escape ':' and ','.</summary>
        private static string EscapeProperty(string s) =>
            EscapeData(s).Replace(":", "%3A").Replace(",", "%2C");

        private static Exception Unwrap(Exception e) =>
            e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;

        private static string Describe(Exception e)
        {
            // NUnit assertion failures already carry a formatted expected/actual message;
            // anything else is an unexpected throw and wants its type and stack.
            if (e.GetType().Name == "AssertionException") return e.Message.Trim();
            return e.GetType().Name + ": " + e.Message + "\n" + FirstFrames(e.StackTrace, 4);
        }

        /// <summary>Assert.Pass and Assert.Ignore report by throwing; neither is a failure.</summary>
        private static bool IsSuccess(Exception e)
        {
            string name = e.GetType().Name;
            return name == "SuccessException" || name == "IgnoreException";
        }

        private static string FirstFrames(string stack, int count)
        {
            if (string.IsNullOrEmpty(stack)) return string.Empty;
            return string.Join("\n", stack.Split('\n').Take(count).Select(s => s.TrimEnd()));
        }

        private static bool IsFixture(Type t)
        {
            if (t.IsAbstract || !t.IsClass) return false;
            if (t.GetCustomAttribute<TestFixtureAttribute>() != null) return true;
            return t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Any(m => m.GetCustomAttribute<TestAttribute>() != null);
        }

        private static MethodInfo SingleMethod<TAttr>(Type fixture) where TAttr : Attribute =>
            fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                   .FirstOrDefault(m => m.GetCustomAttribute<TAttr>() != null);

        /// <summary>
        /// Guards against a test using an NUnit feature this runner would otherwise ignore,
        /// which would report a green run that never executed the test.
        /// </summary>
        private static List<string> FindUnsupportedAttributes(IEnumerable<Type> fixtures)
        {
            var unsupported = new[]
            {
                "TestCaseAttribute", "TestCaseSourceAttribute", "ValuesAttribute",
                "OneTimeSetUpAttribute", "OneTimeTearDownAttribute", "RepeatAttribute",
                "UnityTestAttribute"
            };

            var found = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var fixture in fixtures)
                foreach (var m in fixture.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    foreach (var a in m.GetCustomAttributes())
                        if (unsupported.Contains(a.GetType().Name))
                            found.Add("[" + a.GetType().Name.Replace("Attribute", "") + "] on " + fixture.Name + "." + m.Name);

            return found.ToList();
        }
    }
}
