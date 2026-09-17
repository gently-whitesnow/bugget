using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bugget.Domain.Constants;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Transactions;
using DbUp.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bugget.Infrastructure.DbUp;

internal sealed class EmbeddedSqlScriptProvider(
    Assembly assembly,
    string resourceNamespace,
    Func<string, string> renameForJournal) : IScriptProvider
{
    private readonly string _prefix = resourceNamespace + ".";

    public IEnumerable<SqlScript> GetScripts(IConnectionManager connectionManager)
    {
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(_prefix, StringComparison.Ordinal)
                           && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException($"Embedded SQL resource {name} not found");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return new SqlScript(renameForJournal(name), reader.ReadToEnd());
            })
            .ToList();
    }
}
