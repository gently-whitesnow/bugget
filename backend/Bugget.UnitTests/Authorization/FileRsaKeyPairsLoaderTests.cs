using Bugget.Api.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Bugget.UnitTests.Authorization;

public class FileRsaKeyPairsLoaderTests
{
    private const string _validJsonFilePath = "data/key_pairs/valid_key_pairs.json";
    private const string _invalidJsonFilePath = "data/key_pairs/invalid_key_pairs.json";

    [Fact]
    public async Task LoadRsaKeyPairsAsync_ValidFile_ReturnsKeyPairs()
    {
        // Act
        var result = await FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(_validJsonFilePath);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test-key-1", result.First().KeyId);
        Assert.IsType<RsaSecurityKey>(result.First().PrivateKey);
        Assert.Equal("test-key-1", result.First().PublicKey.KeyId);
        Assert.IsType<RsaSecurityKey>(result.First().PublicKey);
        Assert.Equal("test-key-1", result.First().PublicKey.KeyId);
    }

    [Fact]
    public async Task LoadRsaKeyPairsAsync_InvalidPemKeys_ReturnsFailure()
    {
        // Act
        var result = await FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(_invalidJsonFilePath);

        var privateKeyResult = result.First().PrivateKey;
        var errorMessage = Assert.Throws<ArgumentException>(() => _ = result.First().PublicKey);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("test-key-1", privateKeyResult.KeyId);
        Assert.IsType<RsaSecurityKey>(privateKeyResult);
        Assert.Contains("No supported key formats were found.", errorMessage.Message);
    }

    [Fact]
    public async Task LoadRsaKeyPairsAsync_NonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Guid.NewGuid().ToString();

        // Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => FileRsaKeyPairsLoader.LoadRsaKeyPairsAsync(nonExistentPath));
    }
}
