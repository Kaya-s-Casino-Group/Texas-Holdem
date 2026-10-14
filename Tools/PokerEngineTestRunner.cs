// Standalone offline runner for the same NUnit tests used by Unity EditMode.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class PokerEngineTestRunner
{
    private static int Main(string[] args)
    {
        int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => args.Length == 0 || args.Contains(t.Name)))
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0 ||
            m.GetCustomAttributes(typeof(TestCaseAttribute), true).Length > 0))
        {
            var cases = method.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().Select(c => c.Arguments).ToArray();
            if (cases.Length == 0) cases = new[] { new object[0] };
            foreach (var arguments in cases)
            {
                try { method.Invoke(Activator.CreateInstance(type), arguments); passed++; }
                catch (Exception error)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL " + type.Name + "." + method.Name + "(" + string.Join(",", arguments) + "): " + (error.InnerException ?? error));
                }
            }
        }
        Console.WriteLine("PokerEngine: " + passed + " passed, " + failed + " failed.");
        return failed == 0 && passed > 0 ? 0 : 1;
    }
}
