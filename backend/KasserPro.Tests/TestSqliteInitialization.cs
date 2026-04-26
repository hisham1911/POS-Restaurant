using System.Runtime.CompilerServices;

namespace KasserPro.Tests;

public static class TestSqliteInitialization
{
    [ModuleInitializer]
    public static void Initialize()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());
    }
}
