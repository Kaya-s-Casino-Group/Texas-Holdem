// Standalone offline runner for the same NUnit tests used by Unity EditMode.
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class PokerEngineTestRunner
{
    private static int Main()
    {
        int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttributes(typeof(TestFixtureAttribute), true).Length > 0))
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0))
        {
            try { method.Invoke(Activator.CreateInstance(type), null); passed++; }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine("FAIL " + type.Name + "." + method.Name + ": " + (error.InnerException ?? error));
            }
        }
        Console.WriteLine("PokerEngine: " + passed + " passed, " + failed + " failed.");
        return failed == 0 && passed > 0 ? 0 : 1;
    }
}
