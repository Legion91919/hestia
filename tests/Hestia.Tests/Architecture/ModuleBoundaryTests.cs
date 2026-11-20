using System.Reflection;
using System.Runtime.CompilerServices;

namespace Hestia.Architecture.Tests;

public class ModuleBoundaryTests
{
    private static readonly Assembly Tenancy = typeof(Hestia.Tenancy.TenantId).Assembly;
    private static readonly Assembly Investors = typeof(Hestia.Investors.InvestorUser).Assembly;
    private static readonly Assembly Documents = typeof(Hestia.Documents.Document).Assembly;
    private static readonly Assembly[] BusinessModules = [Investors, Documents];

    [Fact]
    public void Business_modules_never_reference_each_other()
    {
        foreach (var module in BusinessModules)
        {
            var references = module.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            foreach (var other in BusinessModules.Where(m => m != module))
            {
                Assert.DoesNotContain(references, r => r == other.GetName().Name);
            }
        }
    }

    [Fact]
    public void The_tenancy_kernel_depends_on_no_module()
    {
        var references = Tenancy.GetReferencedAssemblies().Select(a => a.Name!).ToList();
        Assert.DoesNotContain(references, r => r.StartsWith("Hestia.", StringComparison.Ordinal));
    }

    [Fact]
    public void Modules_only_open_their_internals_to_the_test_project()
    {
        foreach (var module in BusinessModules.Append(Tenancy))
        {
            var friends = module.GetCustomAttributes<InternalsVisibleToAttribute>().Select(a => a.AssemblyName).ToList();
            Assert.All(friends, f => Assert.Equal("Hestia.Tests", f));
        }
    }

    [Fact]
    public void Module_implementations_stay_internal()
    {
        foreach (var module in BusinessModules)
        {
            var publicClasses = module.GetExportedTypes().Where(t => t.IsClass && !t.IsAbstract && !IsContractShape(t)).Select(t => t.FullName!).ToList();
            Assert.Empty(publicClasses);
        }
    }

    /// <summary>Public classes allowed in a module: records (data contracts), static helpers, options and exceptions.</summary>
    private static bool IsContractShape(Type t) =>
        t.GetMethod("<Clone>$") is not null
        || (t.IsAbstract && t.IsSealed)
        || t.Name.EndsWith("Options", StringComparison.Ordinal)
        || typeof(Exception).IsAssignableFrom(t);
}
