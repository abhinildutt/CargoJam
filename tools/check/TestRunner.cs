using System;
using System.Linq;
using System.Reflection;

// Minimal NUnit runner for tools/check: runs every [Test] method (with [SetUp]/[TearDown]) in this
// assembly, optionally filtered by a substring of "Class.Method". Exit code 1 on any failure.
static class TestRunner
{
    static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : "";
        int passed = 0, failed = 0;
        var types = typeof(TestRunner).Assembly.GetTypes().OrderBy(t => t.FullName);
        foreach (var type in types)
        {
            var tests = type.GetMethods().Where(m => Has(m, "TestAttribute")).OrderBy(m => m.Name).ToArray();
            if (tests.Length == 0) continue;
            var setUp = type.GetMethods().FirstOrDefault(m => Has(m, "SetUpAttribute"));
            var tearDown = type.GetMethods().FirstOrDefault(m => Has(m, "TearDownAttribute"));
            foreach (var test in tests)
            {
                string name = type.Name + "." + test.Name;
                if (filter.Length > 0 && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    var instance = Activator.CreateInstance(type);
                    setUp?.Invoke(instance, null);
                    try { test.Invoke(instance, null); }
                    finally { tearDown?.Invoke(instance, null); }
                    passed++;
                }
                catch (Exception e)
                {
                    var inner = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                    failed++;
                    Console.WriteLine($"  FAIL {name}: {inner.Message.Trim()}");
                }
            }
        }
        Console.WriteLine($"   {passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }

    static bool Has(MemberInfo m, string attributeName) =>
        m.GetCustomAttributes(true).Any(a => a.GetType().Name == attributeName);
}
