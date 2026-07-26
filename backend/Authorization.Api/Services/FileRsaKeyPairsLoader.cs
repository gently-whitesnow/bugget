using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Authorization.Models;

namespace Authorization;

/// <summary>
/// Класс для загрузки RSA ключей из файла.
/// </summary>
public static class FileRsaKeyPairsLoader
{
    /// <summary>
    /// Асинхронно загружает RSA ключи из файла в формате JSON.
    /// </summary>
    /// <param name="filePath">Путь к файлу с RSA ключами.</param>
    /// <returns>
    /// Возвращает результат с коллекцией RSA ключей или ошибку, если загрузка не удалась.
    /// </returns>
    /// <remarks>
    /// Файл должен содержать массив объектов <see cref="RsaKeyPair"/>,
    /// где каждый объект представляет пару RSA ключей (открытый и закрытый) в формате PEM.
    /// </remarks>
    public static async Task<IReadOnlyCollection<RsaKeyPair>> LoadRsaKeyPairsAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var rsaKeyPairs = JsonSerializer.Deserialize<RsaKeyPair[]>(json);
        if (rsaKeyPairs == null || rsaKeyPairs.Length == 0)
        {
            throw new Exception("No RSA key pairs found in the file.");
        }

        return rsaKeyPairs;
    }
}
