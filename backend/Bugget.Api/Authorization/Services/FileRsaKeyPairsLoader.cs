using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Bugget.Api.Authorization.Models;

namespace Bugget.Api.Authorization;

public static class FileRsaKeyPairsLoader
{
    /// <summary>
    /// Файл — JSON-массив <see cref="RsaKeyPair"/>: пары RSA-ключей (открытый и закрытый) в формате PEM.
    /// </summary>
    public static async Task<IReadOnlyCollection<RsaKeyPair>> LoadRsaKeyPairsAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var rsaKeyPairs = JsonSerializer.Deserialize<RsaKeyPair[]>(json);
        if (rsaKeyPairs == null || rsaKeyPairs.Length == 0)
        {
            throw new InvalidOperationException("No RSA key pairs found in the file.");
        }

        return rsaKeyPairs;
    }
}
